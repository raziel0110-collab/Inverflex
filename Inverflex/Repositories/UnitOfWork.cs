using Inverflex.Data;

namespace Inverflex.Repositories;

public sealed class UnitOfWork(InvestmentDbContext db) : IUnitOfWork
{
    private IAppUserRepository? _users;
    private IInstrumentRepository? _instruments;
    private IPortfolioRepository? _portfolios;
    private IInvestmentOrderRepository? _orders;
    private IPriceAlertRepository? _alerts;
    private IAdvisorAssignmentRepository? _assignments;
    private INotificationRepository? _notifications;

    public IAppUserRepository Users => _users ??= new AppUserRepository(db);
    public IInstrumentRepository Instruments => _instruments ??= new InstrumentRepository(db);
    public IPortfolioRepository Portfolios => _portfolios ??= new PortfolioRepository(db);
    public IInvestmentOrderRepository Orders => _orders ??= new InvestmentOrderRepository(db);
    public IPriceAlertRepository Alerts => _alerts ??= new PriceAlertRepository(db);
    public IAdvisorAssignmentRepository Assignments => _assignments ??= new AdvisorAssignmentRepository(db);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(db);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);

    public ValueTask DisposeAsync() =>
        db.DisposeAsync();
}
