using Inverflex.Model;

namespace Inverflex.Repositories;

public interface INotificationRepository
{
    Task<ClientNotification?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<ClientNotification>> GetByClientIdAsync(int clientId, int limit = 100, CancellationToken ct = default);
    Task AddAsync(ClientNotification notification, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
