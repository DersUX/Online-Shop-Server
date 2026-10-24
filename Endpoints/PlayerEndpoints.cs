using System.Security.Claims;
using OnlineShopServer.Dtos;
using OnlineShopServer.Security;
using OnlineShopServer.Storage;

namespace OnlineShopServer.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapGet("/player", (ClaimsPrincipal user, AccountStore accounts) =>
        {
            var player = accounts.GetPlayer(user.GetPlayerId()).Snapshot();
            return Results.Ok(new PlayerDto(player.Id, player.Login, player.Balance));
        }).RequireAuthorization();
    }
}
