using Inverflex.Model;

namespace Inverflex.Repositories;

public interface IInstrumentRepository
{
    Task<Instrument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Instrument?> GetBySymbolAsync(string symbol, CancellationToken ct = default);
    Task<List<Instrument>> GetAllAsync(CancellationToken ct = default);
    Task<List<Instrument>> GetPageAsync(int page, int pageSize, InstrumentType? type = null, string? sector = null, CancellationToken ct = default);
    Task<bool> ExistsBySymbolAsync(string symbol, CancellationToken ct = default);
    Task AddAsync(Instrument instrument, CancellationToken ct = default);
    Task UpdateAsync(Instrument instrument, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
