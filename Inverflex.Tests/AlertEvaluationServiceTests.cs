using Inverflex.Data;
using Inverflex.Model;
using Inverflex.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Inverflex.Tests;

public sealed class AlertEvaluationServiceTests
{
    [Fact]
    public async Task RisingAlertTriggersAndCreatesNotification()
    {
        await using var fixture = await Fixture.CreateAsync(AlertDirection.Sube, 100m, 5m);
        fixture.Market.Setup(x => x.GetPriceAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync(106m);
        var count = await new AlertEvaluationService(fixture.Db, fixture.Market.Object).EvaluateAsync(default);
        Assert.Equal(1, count);
        Assert.True((await fixture.Db.PriceAlerts.SingleAsync(x => x.Id == fixture.AlertId)).IsTriggered);
        Assert.Equal(1, await fixture.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task RisingAlertDoesNotTriggerBelowThreshold()
    {
        await using var fixture = await Fixture.CreateAsync(AlertDirection.Sube, 100m, 5m);
        fixture.Market.Setup(x => x.GetPriceAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync(104.99m);
        var count = await new AlertEvaluationService(fixture.Db, fixture.Market.Object).EvaluateAsync(default);
        Assert.Equal(0, count);
        Assert.False((await fixture.Db.PriceAlerts.SingleAsync()).IsTriggered);
        Assert.Empty(await fixture.Db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task FallingAlertTriggersAtOrBelowThreshold()
    {
        await using var fixture = await Fixture.CreateAsync(AlertDirection.Baja, 100m, 5m);
        fixture.Market.Setup(x => x.GetPriceAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync(95m);
        var count = await new AlertEvaluationService(fixture.Db, fixture.Market.Object).EvaluateAsync(default);
        Assert.Equal(1, count);
        Assert.True((await fixture.Db.PriceAlerts.SingleAsync()).IsTriggered);
    }

    [Fact]
    public async Task TriggeredAlertDoesNotNotifyAgainOnNextEvaluation()
    {
        await using var fixture = await Fixture.CreateAsync(AlertDirection.Sube, 100m, 5m);
        fixture.Market.Setup(x => x.GetPriceAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync(110m);
        var service = new AlertEvaluationService(fixture.Db, fixture.Market.Object);
        Assert.Equal(1, await service.EvaluateAsync(default));
        Assert.Equal(0, await service.EvaluateAsync(default));
        Assert.Equal(1, await fixture.Db.Notifications.CountAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public InvestmentDbContext Db { get; }
        public Mock<IMarketPriceService> Market { get; } = new(MockBehavior.Strict);
        public int AlertId { get; private set; }

        private Fixture(SqliteConnection connection, InvestmentDbContext db) { _connection = connection; Db = db; }

        public static async Task<Fixture> CreateAsync(AlertDirection direction, decimal baseline, decimal percent)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new InvestmentDbContext(new DbContextOptionsBuilder<InvestmentDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var alert = new PriceAlert { ClientId = 1, InstrumentId = "AAPL", Direction = direction, BaselinePrice = baseline, PercentChange = percent };
            db.PriceAlerts.Add(alert);
            await db.SaveChangesAsync();
            return new Fixture(connection, db) { AlertId = alert.Id };
        }

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
