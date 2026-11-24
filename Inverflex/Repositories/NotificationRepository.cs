using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class NotificationRepository(InvestmentDbContext db) : INotificationRepository
{
    public Task<ClientNotification?> GetByIdAsync(long id, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<ClientNotification>> GetByClientIdAsync(int clientId, int limit = 100, CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking().Where(x => x.ClientId == clientId);
        var ordered = db.Database.IsSqlite()
            ? query.OrderByDescending(x => x.CreatedAtUtcTicks)
            : query.OrderByDescending(x => x.CreatedAt);
        return ordered.Take(limit).ToListAsync(ct);
    }

    public async Task AddAsync(ClientNotification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (notification is not null)
        {
            db.Notifications.Remove(notification);
            await SaveChangesAsync(ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
