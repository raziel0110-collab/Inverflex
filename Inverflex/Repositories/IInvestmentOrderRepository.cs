using Inverflex.Model;

namespace Inverflex.Repositories;

public interface IInvestmentOrderRepository
{
    Task<InvestmentOrder?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<InvestmentOrder>> GetByClientIdAsync(int clientId, CancellationToken ct = default);
    Task<List<InvestmentOrder>> GetPendingForApprovalAsync(CancellationToken ct = default);
    Task<List<InvestmentOrder>> QueryAsync(
        int? clientId = null,
        string? instrumentId = null,
        OrderStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default
    );
    Task<int> CountAsync(
        int? clientId = null,
        string? instrumentId = null,
        OrderStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default
    );
    Task AddAsync(InvestmentOrder order, CancellationToken ct = default);
    Task UpdateAsync(InvestmentOrder order, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
