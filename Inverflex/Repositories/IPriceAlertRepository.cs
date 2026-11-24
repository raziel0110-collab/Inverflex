using Inverflex.Model;

namespace Inverflex.Repositories;

public interface IPriceAlertRepository
{
    Task<PriceAlert?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<PriceAlert>> GetByClientIdAsync(int clientId, CancellationToken ct = default);
    Task<List<PriceAlert>> GetTriggeredAlertsAsync(CancellationToken ct = default);
    Task AddAsync(PriceAlert alert, CancellationToken ct = default);
    Task UpdateAsync(PriceAlert alert, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
