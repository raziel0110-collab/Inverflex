using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class AdvisorAssignmentRepository(InvestmentDbContext db) : IAdvisorAssignmentRepository
{
    public Task<AdvisorAssignment?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.AdvisorAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<AdvisorAssignment>> GetAllAsync(CancellationToken ct = default) =>
        db.AdvisorAssignments.AsNoTracking().ToListAsync(ct);

    public Task<List<AdvisorAssignment>> GetByAdvisorIdAsync(int advisorId, CancellationToken ct = default) =>
        db.AdvisorAssignments.AsNoTracking().Where(x => x.AdvisorId == advisorId).ToListAsync(ct);

    public Task<List<AdvisorAssignment>> GetByClientIdAsync(int clientId, CancellationToken ct = default) =>
        db.AdvisorAssignments.AsNoTracking().Where(x => x.ClientId == clientId).ToListAsync(ct);

    public Task<bool> ExistsAsync(int advisorId, int clientId, CancellationToken ct = default) =>
        db.AdvisorAssignments.AnyAsync(x => x.AdvisorId == advisorId && x.ClientId == clientId, ct);

    public async Task AddAsync(AdvisorAssignment assignment, CancellationToken ct = default)
    {
        db.AdvisorAssignments.Add(assignment);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var assignment = await db.AdvisorAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (assignment is not null)
        {
            db.AdvisorAssignments.Remove(assignment);
            await SaveChangesAsync(ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
