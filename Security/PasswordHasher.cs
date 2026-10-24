using System.Security.Cryptography;

namespace OnlineShopServer.Security;

public static class PasswordHasher
{
    public static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);

    public static bool Verify(string password, byte[] salt, byte[] expectedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(password, salt), expectedHash);
}
