using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OnlineShopServer.Endpoints;
using static Verification.Check;

namespace Verification;

internal static class ExceptionHandlingChecks
{
    public static async Task UnexpectedFailure()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.MapGet("/failure", (Func<IResult>)(() => throw new InvalidOperationException("private failure details")));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
            using var response = await client.GetAsync("/failure");
            var text = await response.Content.ReadAsStringAsync();
            Assert(response.StatusCode == HttpStatusCode.InternalServerError, "Unexpected failure must return 500");
            Assert(response.Content.Headers.ContentType?.MediaType == "application/json", "500 must return JSON");
            using var body = JsonDocument.Parse(text);
            Assert(body.RootElement.GetProperty("code").GetString() == "internal_error"
                && !string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()), "Invalid 500 contract");
            Assert(!text.Contains("private failure details", StringComparison.Ordinal)
                && !text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal), "500 leaked exception details");
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
