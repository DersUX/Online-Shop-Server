using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Verification;

internal sealed class TestServer : IAsyncDisposable
{
    private readonly Process process;
    private readonly StringBuilder output = new();
    private Uri? listeningAddress;
    public HttpClient Client { get; }

    public TestServer(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Run the already-built assembly directly; no nested SDK process is needed.
        foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "OnlineShopServer.dll"), "--urls", "http://127.0.0.1:0" })
            start.ArgumentList.Add(argument);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        // The assigned port is reported by the hosting lifetime logger.
        start.ArgumentList.Add("--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information");
        start.ArgumentList.Add("--Logging:Console:FormatterName=simple");
        start.ArgumentList.Add("--Logging:Console:FormatterOptions:ColorBehavior=Disabled");
        process = new Process { StartInfo = start };
        process.OutputDataReceived += CaptureOutput;
        process.ErrorDataReceived += CaptureOutput;
        if (!process.Start())
            throw new Exception("Could not start server");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    private void CaptureOutput(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is null)
            return;
        lock (output)
        {
            output.AppendLine(args.Data);
            const string prefix = "Now listening on: ";
            var index = args.Data.IndexOf(prefix, StringComparison.Ordinal);
            if (index >= 0 && Uri.TryCreate(args.Data[(index + prefix.Length)..].Trim(), UriKind.Absolute, out var address)
                && address.Scheme == Uri.UriSchemeHttp && address.IsLoopback && address.Port > 0)
                listeningAddress = address;
        }
    }

    private string ReadOutput()
    {
        lock (output)
            return output.ToString();
    }

    public async Task WaitUntilReady()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            while (!deadline.IsCancellationRequested)
            {
                if (process.HasExited)
                {
                    await process.WaitForExitAsync();
                    throw new InvalidOperationException($"Server exited with code {process.ExitCode}.\n{ReadOutput()}");
                }
                lock (output)
                    Client.BaseAddress ??= listeningAddress;
                if (Client.BaseAddress is null)
                {
                    await Task.Delay(100, deadline.Token);
                    continue;
                }
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                attempt.CancelAfter(TimeSpan.FromSeconds(1));
                try
                {
                    using var response = await Client.GetAsync("/items", attempt.Token);
                    if (response.StatusCode == HttpStatusCode.Unauthorized
                        && response.Content.Headers.ContentType?.MediaType == "application/json")
                    {
                        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(attempt.Token));
                        if (body.RootElement.TryGetProperty("code", out var code) && code.GetString() == "unauthorized")
                            return;
                    }
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (attempt.IsCancellationRequested) { }
                catch (JsonException) { }
                await Task.Delay(100, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }

        throw new TimeoutException($"Server did not become ready within 20 seconds.\n{ReadOutput()}");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        process.Dispose();
    }
}
