using Inverflex.Model;

namespace Inverflex.DTOs;

public sealed record AssignmentRequestDto(int AdvisorId, int ClientId);
public sealed record AssignmentResponseDto(int Id, int AdvisorId, int ClientId);
