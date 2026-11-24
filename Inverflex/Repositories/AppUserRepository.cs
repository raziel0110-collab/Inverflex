using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class AppUserRepository(InvestmentDbContext db) : IAppUserRepository
{
    public Task<AppUser?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Username == username, ct);

    public Task<List<AppUser>> GetAllAsync(CancellationToken ct = default) =>
        db.Users.AsNoTracking().ToListAsync(ct);

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.AnyAsync(x => x.Username == username, ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is not null)
        {
            db.Users.Remove(user);
            await SaveChangesAsync(ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
