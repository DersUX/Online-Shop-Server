using OnlineShopServer.Models;
using OnlineShopServer.Security;

namespace OnlineShopServer.Storage;

public sealed class AccountStore
{
    private readonly AccountCredentials[] accounts;

    public AccountStore(IEnumerable<AccountCredentials> accounts)
    {
        this.accounts = accounts.ToArray();
        if (this.accounts.Select(account => account.Player.Id).Distinct().Count() != this.accounts.Length
            || this.accounts.Select(account => account.Player.Login).Distinct(StringComparer.Ordinal).Count() != this.accounts.Length)
            throw new ArgumentException("Account IDs and logins must be unique", nameof(accounts));
    }

    internal AccountCredentials? FindByLogin(string login) =>
        accounts.FirstOrDefault(account => account.Player.Login == login);

    public PlayerAccount GetPlayer(int id) => accounts.Single(account => account.Player.Id == id).Player;
}
