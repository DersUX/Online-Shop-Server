using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OnlineShopServer.Storage;

public sealed record PlayerSession(int PlayerId, DateTimeOffset ExpiresAt);

public sealed class SessionStore(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, PlayerSession> sessions = new();

    public (string Token, PlayerSession Session, DateTimeOffset ServerTime) Create(int playerId)
    {
        var serverTime = clock.GetUtcNow();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new PlayerSession(playerId, serverTime.AddHours(1));
        sessions[token] = session;
        return (token, session, serverTime);
    }

    public PlayerSession? Find(string token)
    {
        if (!sessions.TryGetValue(token, out var session))
            return null;
        if (session.ExpiresAt > clock.GetUtcNow())
            return session;
        sessions.TryRemove(token, out _);
        return null;
    }

    public void Remove(string token) => sessions.TryRemove(token, out _);

    public int RemoveExpired()
    {
        var now = clock.GetUtcNow();
        var removed = 0;
        foreach (var entry in sessions)
        {
            if (entry.Value.ExpiresAt <= now && sessions.TryRemove(entry.Key, out _))
                removed++;
        }
        return removed;
    }
}
