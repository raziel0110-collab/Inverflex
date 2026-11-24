using System.Text.Json;
using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Inverflex.Services;

public interface IMarketPriceService
{
    Task<decimal> GetPriceAsync(string instrumentId, CancellationToken cancellationToken = default);
}

public sealed class MockMarketPriceService(InvestmentDbContext db) : IMarketPriceService
{
    public async Task<decimal> GetPriceAsync(string instrumentId, CancellationToken cancellationToken = default)
    {
        var instrument = await db.Instruments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == instrumentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Instrumento '{instrumentId}' no encontrado.");
        var variation = (decimal)(Random.Shared.NextDouble() * 0.10 - 0.05);
        return decimal.Round(instrument.ReferencePrice * (1 + variation), 2);
    }
}

public sealed record InstrumentPrice(string Id, string Symbol, string Name, InstrumentType Type, string Sector, string Currency, decimal Price);

public sealed class InstrumentCatalogService(InvestmentDbContext db, IMarketPriceService market, IDistributedCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<InstrumentPrice>> GetPageAsync(int page, int pageSize, InstrumentType? type, string? sector, CancellationToken ct)
    {
        var cacheKey = "catalog:v1";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        List<InstrumentPrice> all;
        if (cached is not null)
            all = JsonSerializer.Deserialize<List<InstrumentPrice>>(cached, JsonOptions) ?? [];
        else
        {
            var instruments = await db.Instruments.AsNoTracking().OrderBy(x => x.Symbol).ToListAsync(ct);
            all = [];
            foreach (var instrument in instruments)
                all.Add(new(instrument.Id, instrument.Symbol, instrument.Name, instrument.Type, instrument.Sector, instrument.Currency, await market.GetPriceAsync(instrument.Id, ct)));
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(all, JsonOptions), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) }, ct);
        }
        return all.Where(x => type is null || x.Type == type).Where(x => sector is null || x.Sector.Contains(sector, StringComparison.OrdinalIgnoreCase)).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
    }
}
