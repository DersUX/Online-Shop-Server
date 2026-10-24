using OnlineShopServer.Dtos;
using OnlineShopServer.Models;
using OnlineShopServer.Security;
using OnlineShopServer.Services;
using OnlineShopServer.Storage;

namespace OnlineShopServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", (LoginRequestDto request, AuthenticationService authentication, SessionStore sessions) =>
        {
            if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password)
                || request.Login.Length > PlayerAccount.MaxLoginLength || request.Password.Length > 256)
                return Results.BadRequest(new ErrorDto("invalid_request", "Нужны логин и пароль допустимой длины"));
            var player = authentication.Authenticate(request.Login, request.Password);
            if (player is null)
                return Results.Json(new ErrorDto("invalid_credentials", "Неверный логин или пароль"), statusCode: 401);
            var session = sessions.Create(player.Id);
            return Results.Ok(new LoginResponseDto(session.Token, session.ServerTime, session.Session.ExpiresAt));
        }).RequireRateLimiting("login");

        app.MapPost("/auth/logout", (HttpRequest request, SessionStore sessions) =>
        {
            sessions.Remove(SessionAuthenticationHandler.ReadToken(request)!);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
