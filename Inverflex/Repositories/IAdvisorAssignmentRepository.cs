using Inverflex.Model;

namespace Inverflex.Repositories;

public interface IAdvisorAssignmentRepository
{
    Task<AdvisorAssignment?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<AdvisorAssignment>> GetAllAsync(CancellationToken ct = default);
    Task<List<AdvisorAssignment>> GetByAdvisorIdAsync(int advisorId, CancellationToken ct = default);
    Task<List<AdvisorAssignment>> GetByClientIdAsync(int clientId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int advisorId, int clientId, CancellationToken ct = default);
    Task AddAsync(AdvisorAssignment assignment, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
