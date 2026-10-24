using OnlineShopServer.Storage;

namespace OnlineShopServer.Services;

public sealed class SessionCleanupService(SessionStore sessions, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            sessions.RemoveExpired();
    }
}
