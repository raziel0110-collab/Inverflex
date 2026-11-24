using Inverflex.Model;

namespace Inverflex.DTOs;

public sealed record InstrumentCreateRequestDto(string Symbol, string Name, InstrumentType Type, string Sector, decimal ReferencePrice, string Currency = "USD");
public sealed record InstrumentListDto(string Id, string Symbol, string Name, InstrumentType Type, string Sector, decimal ReferencePrice, string Currency);
