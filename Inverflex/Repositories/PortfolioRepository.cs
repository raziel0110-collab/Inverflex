using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class PortfolioRepository(InvestmentDbContext db) : IPortfolioRepository
{
    public Task<Portfolio?> GetByClientIdAsync(int clientId, CancellationToken ct = default) =>
        db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId, ct);

    public Task<Portfolio?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(Portfolio portfolio, CancellationToken ct = default)
    {
        db.Portfolios.Add(portfolio);
        await SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Portfolio portfolio, CancellationToken ct = default)
    {
        db.Portfolios.Update(portfolio);
        await SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
