using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OnlineShopServer.Dtos;
using OnlineShopServer.Storage;

namespace OnlineShopServer.Security;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, SessionStore sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";

    public static string? ReadToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header[7..].Trim() : null;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken(Request);
        var session = token is null ? null : sessions.Find(token);
        if (session is null)
            return Task.FromResult(AuthenticateResult.Fail("Недействительная или отсутствующая сессия"));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, session.PlayerId.ToString(CultureInfo.InvariantCulture))], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(new ErrorDto("unauthorized", "Требуется действующий токен сессии"));
    }
}
