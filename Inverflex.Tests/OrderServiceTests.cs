using Inverflex.Data;
using Inverflex.Model;
using Inverflex.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;

namespace Inverflex.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task PurchaseBelowThresholdIsExecutedAndAppliedToPortfolio()
    {
        await using var fixture = await Fixture.CreateAsync();

        var order = await fixture.Service.CreateAsync(fixture.ClientId, "AAPL", OrderSide.Compra, 499.99m, default);

        Assert.Equal(49_999m, order.TotalAmount);
        Assert.Equal(OrderStatus.Ejecutada, order.Status);
        Assert.NotNull(order.CompletedAt);
        var savedOrder = await fixture.Db.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Ejecutada, savedOrder.Status);
        var position = await fixture.Db.Positions.SingleAsync();
        Assert.Equal(499.99m, position.Quantity);
        Assert.Equal(100m, position.AverageCost);
        fixture.Cache.Verify(x => x.RemoveAsync($"portfolio:v1:{fixture.ClientId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurchaseAtThresholdRemainsPendingForApproval()
    {
        await using var fixture = await Fixture.CreateAsync();

        var order = await fixture.Service.CreateAsync(fixture.ClientId, "AAPL", OrderSide.Compra, 500m, default);

        Assert.Equal(50_000m, order.TotalAmount);
        Assert.Equal(OrderStatus.PendienteAprobacion, order.Status);
        Assert.Null(order.CompletedAt);
        Assert.Equal(OrderStatus.PendienteAprobacion, (await fixture.Db.Orders.SingleAsync()).Status);
        Assert.Empty(await fixture.Db.Positions.ToListAsync());
        fixture.Cache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AppUserIdsAreConsecutiveIntegersAndUnique()
    {
        await using var fixture = await Fixture.CreateAsync();
        var client = await fixture.Db.Users.SingleAsync();
        var nextUser = new AppUser { Name = "Segundo cliente", Username = "client2" };

        fixture.Db.Users.Add(nextUser);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(client.Id + 1, nextUser.Id);
        Assert.Contains(fixture.Db.Model.FindEntityType(typeof(AppUser))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Count == 1 && index.Properties[0].Name == nameof(AppUser.Id));

        fixture.Db.Entry(client).State = EntityState.Detached;
        fixture.Db.Users.Add(new AppUser { Id = client.Id, Name = "Duplicado", Username = "duplicate" });
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public InvestmentDbContext Db { get; }
        public Mock<IMarketPriceService> Market { get; } = new(MockBehavior.Strict);
        public Mock<IDistributedCache> Cache { get; } = new(MockBehavior.Strict);
        public OrderService Service { get; }
        public int ClientId { get; }

        private Fixture(SqliteConnection connection, InvestmentDbContext db, int clientId)
        {
            _connection = connection;
            Db = db;
            ClientId = clientId;
            Service = new OrderService(db, Market.Object, new PortfolioService(db, Market.Object, Cache.Object));
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new InvestmentDbContext(new DbContextOptionsBuilder<InvestmentDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var client = new AppUser { Name = "Cliente de prueba", Username = "client" };
            db.Users.Add(client);
            await db.SaveChangesAsync();
            db.Instruments.Add(new Instrument { Id = "AAPL", Symbol = "AAPL", Name = "Apple", Sector = "Tecnología" });
            db.Portfolios.Add(new Portfolio { ClientId = client.Id });
            await db.SaveChangesAsync();

            var fixture = new Fixture(connection, db, client.Id);
            fixture.Market.Setup(x => x.GetPriceAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync(100m);
            fixture.Cache.Setup(x => x.RemoveAsync($"portfolio:v1:{client.Id}", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
