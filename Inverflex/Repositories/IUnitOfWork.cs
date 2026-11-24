namespace Inverflex.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
    IAppUserRepository Users { get; }
    IInstrumentRepository Instruments { get; }
    IPortfolioRepository Portfolios { get; }
    IInvestmentOrderRepository Orders { get; }
    IPriceAlertRepository Alerts { get; }
    IAdvisorAssignmentRepository Assignments { get; }
    INotificationRepository Notifications { get; }
    Task SaveChangesAsync(CancellationToken ct = default);
}
