using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Repositories;

public sealed class InstrumentRepository(InvestmentDbContext db) : IInstrumentRepository
{
    public Task<Instrument?> GetByIdAsync(string id, CancellationToken ct = default) =>
        db.Instruments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Instrument?> GetBySymbolAsync(string symbol, CancellationToken ct = default) =>
        db.Instruments.AsNoTracking().FirstOrDefaultAsync(x => x.Symbol == symbol, ct);

    public Task<List<Instrument>> GetAllAsync(CancellationToken ct = default) =>
        db.Instruments.AsNoTracking().ToListAsync(ct);

    public Task<List<Instrument>> GetPageAsync(int page, int pageSize, InstrumentType? type = null, string? sector = null, CancellationToken ct = default)
    {
        var query = db.Instruments.AsNoTracking();
        if (type.HasValue)
            query = query.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(sector))
            query = query.Where(x => x.Sector == sector);
        return query.OrderBy(x => x.Symbol)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsBySymbolAsync(string symbol, CancellationToken ct = default) =>
        db.Instruments.AnyAsync(x => x.Symbol == symbol, ct);

    public async Task AddAsync(Instrument instrument, CancellationToken ct = default)
    {
        db.Instruments.Add(instrument);
        await SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Instrument instrument, CancellationToken ct = default)
    {
        db.Instruments.Update(instrument);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var instrument = await db.Instruments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (instrument is not null)
        {
            db.Instruments.Remove(instrument);
            await SaveChangesAsync(ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
