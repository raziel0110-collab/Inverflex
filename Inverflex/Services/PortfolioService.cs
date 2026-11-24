using System.Text.Json;
using Inverflex.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Inverflex.Services;

public sealed record PortfolioPosition(string InstrumentId, string Symbol, decimal Quantity, decimal AverageCost, decimal CurrentPrice, decimal MarketValue, decimal GainLoss);
public sealed record PortfolioSummary(int ClientId, decimal TotalValue, decimal TotalCost, decimal GainLoss, decimal ReturnPercent, IReadOnlyList<PortfolioPosition> Positions, DateTimeOffset CalculatedAt);

public sealed class PortfolioService(InvestmentDbContext db, IMarketPriceService market, IDistributedCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<PortfolioSummary> GetAsync(int clientId, CancellationToken ct)
    {
        var key = $"portfolio:v1:{clientId}";
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null) return JsonSerializer.Deserialize<PortfolioSummary>(cached, JsonOptions)!;

        var portfolio = await db.Portfolios.AsNoTracking().Include(x => x.Positions)
            .SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
        if (portfolio is null) throw new KeyNotFoundException("Portafolio no encontrado.");
        var positions = new List<PortfolioPosition>();
        foreach (var position in portfolio.Positions)
        {
            var instrument = await db.Instruments.AsNoTracking().SingleAsync(x => x.Id == position.InstrumentId, ct);
            var price = await market.GetPriceAsync(instrument.Id, ct);
            positions.Add(new(instrument.Id, instrument.Symbol, position.Quantity, position.AverageCost, price,
                decimal.Round(position.Quantity * price, 2), decimal.Round(position.Quantity * (price - position.AverageCost), 2)));
        }
        var totalValue = positions.Sum(x => x.MarketValue);
        var totalCost = positions.Sum(x => x.Quantity * x.AverageCost);
        var summary = new PortfolioSummary(clientId, totalValue, totalCost, totalValue - totalCost,
            totalCost == 0 ? 0 : decimal.Round((totalValue - totalCost) / totalCost * 100, 2), positions, DateTimeOffset.UtcNow);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(summary, JsonOptions), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
        return summary;
    }

    public Task InvalidateAsync(int clientId, CancellationToken ct) => cache.RemoveAsync($"portfolio:v1:{clientId}", ct);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        return options;
    }
}
