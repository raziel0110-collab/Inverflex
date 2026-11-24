using Inverflex.Model;

namespace Inverflex.DTOs;

public sealed record AlertCreateRequestDto(string InstrumentId, AlertDirection Direction, decimal PercentChange, int? ClientId = null);
public sealed record AlertResponseDto(int Id, int ClientId, string InstrumentId, AlertDirection Direction, decimal BaselinePrice, decimal PercentChange, bool IsTriggered, DateTimeOffset? TriggeredAt);
