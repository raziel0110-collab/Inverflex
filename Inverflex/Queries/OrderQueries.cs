using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Queries;

public sealed class OrderQueries(InvestmentDbContext db) : IOrderQueries
{
    public Task<int> CountHistoryAsync(int? clientId, string? instrumentId, OrderStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = FilterHistory(db.Orders.AsNoTracking(), clientId, instrumentId, status, from, to);
        return query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<InvestmentOrder>> GetHistoryAsync(int? clientId, string? instrumentId, OrderStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FilterHistory(db.Orders.AsNoTracking(), clientId, instrumentId, status, from, to);
        var ordered = db.Database.IsSqlite()
            ? query.OrderByDescending(x => x.CreatedAtUtcTicks)
            : query.OrderByDescending(x => x.CreatedAt);
        return await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InvestmentOrder>> GetPendingByClientsAsync(IReadOnlyCollection<int> clientIds, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatus.PendienteAprobacion && clientIds.Contains(x.ClientId));
        var ordered = db.Database.IsSqlite()
            ? query.OrderBy(x => x.CreatedAtUtcTicks)
            : query.OrderBy(x => x.CreatedAt);
        return await ordered.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InvestmentOrder>> GetExecutedByClientsAsync(IReadOnlyCollection<int> clientIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatus.Ejecutada && clientIds.Contains(x.ClientId));
        if (db.Database.IsSqlite())
        {
            var fromTicks = from.UtcDateTime.Ticks;
            var toTicks = to.UtcDateTime.Ticks;
            query = query.Where(x => x.CompletedAtUtcTicks >= fromTicks && x.CompletedAtUtcTicks <= toTicks);
        }
        else
            query = query.Where(x => x.CompletedAt >= from && x.CompletedAt <= to);
        return await query.ToListAsync(ct);
    }

    private IQueryable<InvestmentOrder> FilterHistory(IQueryable<InvestmentOrder> query, int? clientId, string? instrumentId, OrderStatus? status, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(instrumentId)) query = query.Where(x => x.InstrumentId == instrumentId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (db.Database.IsSqlite())
        {
            if (from.HasValue) query = query.Where(x => x.CreatedAtUtcTicks >= from.Value.UtcDateTime.Ticks);
            if (to.HasValue) query = query.Where(x => x.CreatedAtUtcTicks <= to.Value.UtcDateTime.Ticks);
        }
        else
        {
            if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
            if (to.HasValue) query = query.Where(x => x.CreatedAt <= to);
        }
        return query;
    }
}
