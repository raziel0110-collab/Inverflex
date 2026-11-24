using Inverflex.Model;

namespace Inverflex.DTOs;

public sealed record OrderCreateRequestDto(string InstrumentId, OrderSide Side, decimal Quantity, int? ClientId = null);
public sealed record OrderResponseDto(
    string Id,
    int ClientId,
    string InstrumentId,
    string Side,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int? ApprovedBy,
    int Version
);
public sealed record OrderListDto(
    string Id,
    int ClientId,
    string InstrumentId,
    string Side,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int? ApprovedBy,
    int Version
);
