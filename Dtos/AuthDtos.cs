namespace OnlineShopServer.Dtos;

public sealed record LoginRequestDto(string? Login, string? Password);
public sealed record LoginResponseDto(string SessionToken, DateTimeOffset ServerTime, DateTimeOffset ExpiresAt);
