using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using OnlineShopServer;
using OnlineShopServer.Endpoints;
using OnlineShopServer.Security;
using OnlineShopServer.Services;
using OnlineShopServer.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5000");
builder.Services.AddSingleton(_ => DemoData.CreateCatalog());
builder.Services.AddSingleton(_ => DemoData.CreateAccounts());
builder.Services.AddSingleton<AuthenticationService>();
builder.Services.AddSingleton<PurchaseService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // shortcut: one shared limit for the demo; use per-client policies for a multi-user deployment.
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();

app.UseApiExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    var code = status switch
    {
        400 => "invalid_request",
        404 => "not_found",
        405 => "method_not_allowed",
        415 => "unsupported_media_type",
        429 => "too_many_requests",
        _ => "http_error"
    };
    await context.HttpContext.Response.WriteAsJsonAsync(
        new OnlineShopServer.Dtos.ErrorDto(code, $"HTTP {status}"));
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGoodsEndpoints();
app.MapAuthEndpoints();
app.MapPlayerEndpoints();
app.MapPurchaseEndpoints();

app.Run();
