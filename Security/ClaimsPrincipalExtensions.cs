using System.Globalization;
using System.Security.Claims;

namespace OnlineShopServer.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetPlayerId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
