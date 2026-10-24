using static Verification.Check;

namespace Verification;

internal static class TestServerChecks
{
    public static async Task DynamicPorts()
    {
        await using var first = new TestServer();
        await using var second = new TestServer();
        await Task.WhenAll(first.WaitUntilReady(), second.WaitUntilReady());
        Assert(first.Client.BaseAddress is { Port: > 0 } && second.Client.BaseAddress is { Port: > 0 },
            "Servers must report their assigned ports");
        Assert(first.Client.BaseAddress != second.Client.BaseAddress, "Servers must use distinct ports");
    }

    public static async Task StartupFailure()
    {
        await using var server = new TestServer("--Kestrel:Endpoints:Invalid:Url=invalid-test-url");
        try
        {
            await server.WaitUntilReady();
        }
        catch (InvalidOperationException exception)
        {
            Assert(exception.Message.Contains("Server exited with code", StringComparison.Ordinal)
                && exception.Message.Contains("invalid-test-url", StringComparison.Ordinal),
                "Startup failure must include the exit code and server diagnostics");
            return;
        }
        throw new Exception("Invalid endpoint unexpectedly started");
    }
}
