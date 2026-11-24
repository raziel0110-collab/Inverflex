using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class PriceAlertRepository(InvestmentDbContext db) : IPriceAlertRepository
{
    public Task<PriceAlert?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.PriceAlerts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<PriceAlert>> GetByClientIdAsync(int clientId, CancellationToken ct = default) =>
        db.PriceAlerts.AsNoTracking().Where(x => x.ClientId == clientId).ToListAsync(ct);

    public Task<List<PriceAlert>> GetTriggeredAlertsAsync(CancellationToken ct = default) =>
        db.PriceAlerts.AsNoTracking().Where(x => x.IsTriggered == false).ToListAsync(ct);

    public async Task AddAsync(PriceAlert alert, CancellationToken ct = default)
    {
        db.PriceAlerts.Add(alert);
        await SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PriceAlert alert, CancellationToken ct = default)
    {
        db.PriceAlerts.Update(alert);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var alert = await db.PriceAlerts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (alert is not null)
        {
            db.PriceAlerts.Remove(alert);
            await SaveChangesAsync(ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
