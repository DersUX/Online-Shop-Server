using OnlineShopServer.Models;
using OnlineShopServer.Security;
using OnlineShopServer.Storage;

namespace OnlineShopServer.Services;

public sealed class AuthenticationService(AccountStore accounts)
{
    public PlayerAccount? Authenticate(string login, string password)
    {
        var entry = accounts.FindByLogin(login);
        // Hash unknown accounts too, so an unknown login does not skip the expensive work.
        var valid = entry is null
            ? PasswordHasher.Verify(password, new byte[16], new byte[32])
            : entry.VerifyPassword(password);
        return entry is not null && valid ? entry.Player : null;
    }
}
