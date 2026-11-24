namespace Inverflex.DTOs;

public sealed record NotificationResponseDto(long Id, int ClientId, string Message, DateTimeOffset CreatedAt);
