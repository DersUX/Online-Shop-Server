using System.Security.Cryptography;
using OnlineShopServer.Models;

namespace OnlineShopServer.Security;

public sealed class AccountCredentials
{
    private readonly byte[] salt;
    private readonly byte[] passwordHash;
    public PlayerAccount Player { get; }

    public AccountCredentials(PlayerAccount player, string password)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentException.ThrowIfNullOrEmpty(password);
        Player = player;
        salt = RandomNumberGenerator.GetBytes(16);
        passwordHash = PasswordHasher.Hash(password, salt);
    }

    internal bool VerifyPassword(string password) => PasswordHasher.Verify(password, salt, passwordHash);
}
