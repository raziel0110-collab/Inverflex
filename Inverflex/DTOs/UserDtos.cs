using Inverflex.Model;

namespace Inverflex.DTOs;

public sealed record UserListDto(int Id, string Name, string Username, string Role);
public sealed record UserCreateRequestDto(string Name, string Username, string Password, UserRole Role);
public sealed record UserCreateResponseDto(int Id, string Name, string Username, UserRole Role);
