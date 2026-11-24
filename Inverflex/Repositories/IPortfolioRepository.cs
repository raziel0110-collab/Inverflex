using Inverflex.Model;

namespace Inverflex.Repositories;

public interface IPortfolioRepository
{
    Task<Portfolio?> GetByClientIdAsync(int clientId, CancellationToken ct = default);
    Task<Portfolio?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Portfolio portfolio, CancellationToken ct = default);
    Task UpdateAsync(Portfolio portfolio, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
