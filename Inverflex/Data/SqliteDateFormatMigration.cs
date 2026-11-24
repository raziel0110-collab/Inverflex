using System.Data.Common;
using System.Globalization;
using Inverflex.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inverflex.Data;

public static class SqliteDateFormatMigration
{
    private sealed record DateColumn(string Table, string Name, string IdColumn, string TicksColumn, bool Nullable);

    private static readonly DateColumn[] DateColumns =
    [
        new("Orders", "CreatedAt", "Id", "CreatedAtUtcTicks", false),
        new("Orders", "CompletedAt", "Id", "CompletedAtUtcTicks", true),
        new("PriceAlerts", "TriggeredAt", "Id", "TriggeredAtUtcTicks", true),
        new("Notifications", "CreatedAt", "Id", "CreatedAtUtcTicks", false)
    ];

    public static async Task ApplyAsync(InvestmentDbContext db, CancellationToken ct = default)
    {
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite") return;

        var connection = db.Database.GetDbConnection();
        var shouldOpen = connection.State != System.Data.ConnectionState.Open;
        if (shouldOpen) await db.Database.OpenConnectionAsync(ct);

        try
        {
            var columnsToMigrate = new List<DateColumn>();
            foreach (var column in DateColumns)
            {
                var type = await GetColumnTypeAsync(connection, column.Table, column.Name, ct);
                if (string.IsNullOrEmpty(type)) continue;
                if (!type.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                    columnsToMigrate.Add(column);
            }

            if (columnsToMigrate.Count == 0) return;

            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;", ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var dbTransaction = transaction.GetDbTransaction();

            if (columnsToMigrate.Any(x => x.Table == "Orders" && x.Name == "CreatedAt"))
                await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_Orders_ClientId_CreatedAt;", ct);

            foreach (var column in columnsToMigrate)
                await MigrateColumnAsync(connection, dbTransaction, column, ct);

            if (columnsToMigrate.Any(x => x.Table == "Orders" && x.Name == "CreatedAt"))
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Orders_ClientId_CreatedAtUtcTicks ON Orders (ClientId, CreatedAtUtcTicks);", ct);

            await transaction.CommitAsync(ct);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);
            if (shouldOpen) await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<string?> GetColumnTypeAsync(DbConnection connection, string table, string columnName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({Quote(table)});";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return reader.IsDBNull(2) ? null : reader.GetString(2);
        }
        return null;
    }

    private static async Task MigrateColumnAsync(DbConnection connection, DbTransaction transaction, DateColumn column, CancellationToken ct)
    {
        var legacyColumn = $"__Legacy_{column.Name}";
        var required = column.Nullable ? "NULL" : "NOT NULL DEFAULT '01-01-0001 00:00:00'";
        var ticksRequired = column.Nullable ? "NULL" : "NOT NULL DEFAULT 0";

        await ExecuteAsync(connection, transaction,
            $"ALTER TABLE {Quote(column.Table)} RENAME COLUMN {Quote(column.Name)} TO {Quote(legacyColumn)};", ct);
        await ExecuteAsync(connection, transaction,
            $"ALTER TABLE {Quote(column.Table)} ADD COLUMN {Quote(column.Name)} TEXT {required};", ct);
        await ExecuteAsync(connection, transaction,
            $"ALTER TABLE {Quote(column.Table)} ADD COLUMN {Quote(column.TicksColumn)} INTEGER {ticksRequired};", ct);

        var rows = new List<(object Id, long? Value)>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = $"SELECT {Quote(column.IdColumn)}, {Quote(legacyColumn)} FROM {Quote(column.Table)} WHERE {Quote(legacyColumn)} IS NOT NULL;";
            await using var reader = await read.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                rows.Add((reader.GetValue(0), Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)));
        }

        var converter = new DateTimeOffsetToBinaryConverter();
        var decode = (Func<long, DateTimeOffset>)converter.ConvertFromProviderExpression.Compile();
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = $"UPDATE {Quote(column.Table)} SET {Quote(column.Name)}=@formatted, {Quote(column.TicksColumn)}=@ticks WHERE {Quote(column.IdColumn)}=@id;";
            var formatted = update.CreateParameter();
            formatted.ParameterName = "@formatted";
            update.Parameters.Add(formatted);
            var ticks = update.CreateParameter();
            ticks.ParameterName = "@ticks";
            update.Parameters.Add(ticks);
            var id = update.CreateParameter();
            id.ParameterName = "@id";
            update.Parameters.Add(id);

            foreach (var row in rows)
            {
                var utc = decode(row.Value!.Value).ToUniversalTime();
                formatted.Value = utc.ToString(UtcDateTimeOffsetJsonConverter.Format, CultureInfo.InvariantCulture);
                ticks.Value = InvestmentDbContext.ToUtcSecondTicks(utc);
                id.Value = row.Id;
                await update.ExecuteNonQueryAsync(ct);
            }
        }

        await ExecuteAsync(connection, transaction,
            $"ALTER TABLE {Quote(column.Table)} DROP COLUMN {Quote(legacyColumn)};", ct);
    }

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string Quote(string identifier) => $"\"{identifier}\"";
}
