using Inverflex.Data;
using Inverflex.Model;
using Inverflex.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inverflex.Tests;

public sealed class SqliteDateColumnMigrationTests
{
    [Fact]
    public async Task MigrationFormatsExistingDateColumnsAndPreservesChronologicalTicks()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE Orders (Id TEXT PRIMARY KEY, ClientId INTEGER NOT NULL, CreatedAt INTEGER NOT NULL, CompletedAt INTEGER NULL);"
            + "CREATE INDEX IX_Orders_ClientId_CreatedAt ON Orders (ClientId, CreatedAt);"
            + "CREATE TABLE PriceAlerts (Id INTEGER PRIMARY KEY, TriggeredAt INTEGER NULL);"
            + "CREATE TABLE Notifications (Id INTEGER PRIMARY KEY, CreatedAt INTEGER NOT NULL);";
        await create.ExecuteNonQueryAsync();

        var converter = new DateTimeOffsetToBinaryConverter();
        var encode = (Func<DateTimeOffset, long>)converter.ConvertToProviderExpression.Compile();
        var older = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero);
        var newer = new DateTimeOffset(2025, 1, 1, 0, 0, 2, TimeSpan.Zero);
        await InsertLegacyOrderAsync(connection, "older", encode(older), encode(older));
        await InsertLegacyOrderAsync(connection, "newer", encode(newer), null);
        await ExecuteAsync(connection, "INSERT INTO PriceAlerts (Id, TriggeredAt) VALUES (1, $value);", ("$value", encode(newer)));
        await ExecuteAsync(connection, "INSERT INTO Notifications (Id, CreatedAt) VALUES (1, $value);", ("$value", encode(older)));

        var db = CreateContext(connection);
        await SqliteDateFormatMigration.ApplyAsync(db);
        await SqliteDateFormatMigration.ApplyAsync(db);

        Assert.Equal("TEXT", await ColumnTypeAsync(connection, "Orders", "CreatedAt"));
        Assert.Equal("TEXT", await ColumnTypeAsync(connection, "Orders", "CompletedAt"));
        Assert.Equal("TEXT", await ColumnTypeAsync(connection, "PriceAlerts", "TriggeredAt"));
        Assert.Equal("TEXT", await ColumnTypeAsync(connection, "Notifications", "CreatedAt"));
        Assert.Equal("31-12-2024 23:59:58", await ScalarAsync(connection, "SELECT CreatedAt FROM Orders WHERE Id='older';"));
        Assert.Equal("01-01-2025 00:00:02", await ScalarAsync(connection, "SELECT CreatedAt FROM Orders WHERE Id='newer';"));
        Assert.Equal("31-12-2024 23:59:58", await ScalarAsync(connection, "SELECT CompletedAt FROM Orders WHERE Id='older';"));
        Assert.Equal("31-12-2024 23:59:58", await ScalarAsync(connection, "SELECT CreatedAt FROM Notifications WHERE Id=1;"));
        Assert.Equal("01-01-2025 00:00:02", await ScalarAsync(connection, "SELECT TriggeredAt FROM PriceAlerts WHERE Id=1;"));
        Assert.Equal("older,newer", await ScalarAsync(connection, "SELECT group_concat(Id, ',') FROM (SELECT Id FROM Orders ORDER BY CreatedAtUtcTicks);"));
        Assert.Null(await ScalarAsync(connection, "SELECT CompletedAt FROM Orders WHERE Id='newer';"));
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task NewOrdersStoreFormattedDatesAndUseTicksForFilteringAndOrdering()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        var older = new InvestmentOrder { ClientId = 1, CreatedAt = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero) };
        var newer = new InvestmentOrder { ClientId = 1, CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 2, TimeSpan.Zero) };
        db.Orders.AddRange(older, newer);
        await db.SaveChangesAsync();

        Assert.Equal("31-12-2024 23:59:58", await ScalarAsync(connection, "SELECT CreatedAt FROM Orders WHERE Id='" + older.Id + "';"));
        var orders = await new OrderQueries(db).GetHistoryAsync(1, null, null, older.CreatedAt, newer.CreatedAt, 1, 10);

        Assert.Equal(newer.Id, orders[0].Id);
        Assert.Equal(older.Id, orders[1].Id);
        Assert.Single(await new OrderQueries(db).GetHistoryAsync(1, null, null, newer.CreatedAt, newer.CreatedAt, 1, 10));
        Assert.True(older.CreatedAtUtcTicks < newer.CreatedAtUtcTicks);
    }

    private static InvestmentDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<InvestmentDbContext>().UseSqlite(connection).Options);

    private static async Task InsertLegacyOrderAsync(SqliteConnection connection, string id, long createdAt, long? completedAt)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Orders (Id, ClientId, CreatedAt, CompletedAt) VALUES ($id, 1, $createdAt, $completedAt);";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.Parameters.AddWithValue("$completedAt", completedAt is null ? DBNull.Value : completedAt.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, (string Name, object Value) parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string?> ColumnTypeAsync(SqliteConnection connection, string table, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            if (reader.GetString(1) == columnName)
                return reader.GetString(2);
        return null;
    }
}
