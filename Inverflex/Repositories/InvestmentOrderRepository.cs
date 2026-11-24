using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class InvestmentOrderRepository(InvestmentDbContext db) : IInvestmentOrderRepository
{
    public Task<InvestmentOrder?> GetByIdAsync(string id, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<InvestmentOrder>> GetByClientIdAsync(int clientId, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().Where(x => x.ClientId == clientId).ToListAsync(ct);

    public Task<List<InvestmentOrder>> GetPendingForApprovalAsync(CancellationToken ct = default) =>
        db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.PendienteAprobacion).ToListAsync(ct);

    public Task<List<InvestmentOrder>> QueryAsync(
        int? clientId = null,
        string? instrumentId = null,
        OrderStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrEmpty(instrumentId))
            query = query.Where(x => x.InstrumentId == instrumentId);
        if (status.HasValue)
            query = query.Where(x => x.Status == status);
        query = FilterDateRange(query, from, to);
        var ordered = db.Database.IsSqlite()
            ? query.OrderByDescending(x => x.CreatedAtUtcTicks)
            : query.OrderByDescending(x => x.CreatedAt);
        return ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        int? clientId = null,
        string? instrumentId = null,
        OrderStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrEmpty(instrumentId))
            query = query.Where(x => x.InstrumentId == instrumentId);
        if (status.HasValue)
            query = query.Where(x => x.Status == status);
        query = FilterDateRange(query, from, to);
        return query.CountAsync(ct);
    }

    private IQueryable<InvestmentOrder> FilterDateRange(IQueryable<InvestmentOrder> query, DateTimeOffset? from, DateTimeOffset? to)
    {
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

    public async Task AddAsync(InvestmentOrder order, CancellationToken ct = default)
    {
        db.Orders.Add(order);
        await SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(InvestmentOrder order, CancellationToken ct = default)
    {
        db.Orders.Update(order);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order is not null)
        {
            db.Orders.Remove(order);
            await SaveChangesAsync(ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
