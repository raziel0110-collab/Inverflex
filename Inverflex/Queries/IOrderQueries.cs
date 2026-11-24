using Inverflex.Model;

namespace Inverflex.Queries;

public interface IOrderQueries
{
    Task<int> CountHistoryAsync(int? clientId, string? instrumentId, OrderStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);

    Task<IReadOnlyList<InvestmentOrder>> GetHistoryAsync(int? clientId, string? instrumentId, OrderStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<InvestmentOrder>> GetPendingByClientsAsync(IReadOnlyCollection<int> clientIds, CancellationToken ct = default);

    Task<IReadOnlyList<InvestmentOrder>> GetExecutedByClientsAsync(IReadOnlyCollection<int> clientIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
