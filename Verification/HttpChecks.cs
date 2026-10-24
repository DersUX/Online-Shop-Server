using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static Verification.Check;

namespace Verification;

internal sealed class HttpChecks(HttpClient client)
{
    private async Task<JsonElement> Send(HttpMethod method, string path, object? body, string? token, int status,
        string? errorCode = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = body as HttpContent ?? JsonContent.Create(body);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert((int)response.StatusCode == status, $"{path}: expected {status}, got {(int)response.StatusCode}: {text}");
        if (status == 204)
            return default;
        Assert(response.Content.Headers.ContentType?.MediaType == "application/json", $"{path}: expected JSON");
        using var document = JsonDocument.Parse(text);
        if (errorCode is not null)
        {
            Assert(document.RootElement.GetProperty("code").GetString() == errorCode, $"{path}: expected {errorCode}");
            Assert(!string.IsNullOrWhiteSpace(document.RootElement.GetProperty("message").GetString()),
                $"{path}: missing error message");
        }
        if (status == 401 && errorCode == "unauthorized")
            Assert(response.Headers.WwwAuthenticate.Any(value => value.Scheme == "Bearer"), "Missing Bearer challenge");
        return document.RootElement.Clone();
    }

    private async Task<string> Login(string login, string password)
    {
        var result = await Send(HttpMethod.Post, "/auth/login", new { login, password }, null, 200);
        Assert(result.EnumerateObject().Count() == 3 && !result.TryGetProperty("player", out _),
            "Login must return only sessionToken, serverTime and expiresAt");
        var serverTime = result.GetProperty("serverTime").GetDateTimeOffset();
        var expiresAt = result.GetProperty("expiresAt").GetDateTimeOffset();
        Assert(serverTime.Offset == TimeSpan.Zero && expiresAt.Offset == TimeSpan.Zero, "Login times must be UTC");
        Assert(expiresAt - serverTime == TimeSpan.FromHours(1), "Login session duration must be one hour");
        return result.GetProperty("sessionToken").GetString()!;
    }

    public async Task Authorization()
    {
        await Send(HttpMethod.Get, "/items", null, null, 401, "unauthorized");
        await Send(HttpMethod.Get, "/player", null, "invalid", 401, "unauthorized");
        await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "unauthorized" }, null, 401, "unauthorized");
        await Send(HttpMethod.Post, "/auth/login", new { login = "player", password = "wrong" }, null, 401, "invalid_credentials");
        await Send(HttpMethod.Post, "/auth/login", new { login = "", password = "" }, null, 400, "invalid_request");
        foreach (var body in new object[]
        {
            new { password = "player123" }, new { login = "player" },
            new { login = (string?)null, password = "player123" },
            new { login = "player", password = (string?)null },
            new { login = " ", password = "x" },
            new { login = new string('x', 65), password = "x" },
            new { login = "player", password = new string('x', 257) }
        })
        {
            var error = await Send(HttpMethod.Post, "/auth/login", body, null, 400);
            Assert(error.GetProperty("code").GetString() == "invalid_request", "Wrong login validation error");
        }
        foreach (var body in new[]
        {
            new { login = "x", password = "x" },
            new { login = new string('x', 64), password = new string('x', 256) },
            new { login = "Player", password = "player123" },
            new { login = " player ", password = "player123" },
            new { login = "player", password = " " }
        })
            await Send(HttpMethod.Post, "/auth/login", body, null, 401, "invalid_credentials");
    }

    public async Task HttpErrors()
    {
        var token = await Login("player", "player123");
        await Send(HttpMethod.Get, "/missing", null, token, 404, "not_found");
        await Send(HttpMethod.Put, "/items", null, token, 405, "method_not_allowed");
        foreach (var path in new[] { "/auth/login", "/purchases" })
        {
            await Send(HttpMethod.Post, path, new StringContent("{}"), token, 415, "unsupported_media_type");
            foreach (var json in new[] { "{", "null" })
                await Send(HttpMethod.Post, path,
                    new StringContent(json, System.Text.Encoding.UTF8, "application/json"), token, 400, "invalid_request");
            await Send(HttpMethod.Post, path, null, token, 400, "invalid_request");
        }
    }

    public async Task LoginRateLimit()
    {
        var token = await Login("player", "player123");
        for (var i = 1; i < 30; i++)
            await Send(HttpMethod.Post, "/auth/login", new { }, null, 400, "invalid_request");
        await Send(HttpMethod.Post, "/auth/login", new { login = "player", password = "player123" },
            null, 429, "too_many_requests");
        await Send(HttpMethod.Get, "/player", null, token, 200);
        await Send(HttpMethod.Post, "/auth/logout", null, token, 204);
    }

    public async Task Catalog()
    {
        var token = await Login("player", "player123");
        var catalog = await Send(HttpMethod.Get, "/items", null, token, 200);
        Assert(catalog.GetProperty("goods").GetArrayLength() == 1000, "Catalog changed");
        var goods = catalog.GetProperty("goods").EnumerateArray().OrderBy(x => x.GetProperty("id").GetInt32()).ToArray();
        Assert(goods[0].GetProperty("header").GetString() == "Меч", "Goods contract changed");
        var expectedPrices = new[] { 100, 150, 50, 80, 120, 200, 70 };
        var expectedTypes = new[] { 7, 6, 2, 3, 1, 4, 5 };
        for (var i = 0; i < goods.Length; i++)
        {
            Assert(goods[i].GetProperty("id").GetInt32() == i + 1, "Missing or duplicate item ID");
            Assert(goods[i].GetProperty("typeId").GetInt32() == expectedTypes[i % expectedTypes.Length],
                "Wrong numeric item type");
            Assert(goods[i].GetProperty("price").GetInt32() == expectedPrices[i % 7] + i / 7 * 25, "Wrong catalog price");
            Assert(!goods[i].TryGetProperty("level", out _), "Item must not contain level");
        }
    }

    public async Task PurchaseValidation()
    {
        var token = await Login("player", "player123");
        await Send(HttpMethod.Post, "/purchases", new { goodId = 0, requestId = "" }, token, 400);
        foreach (var body in new object[]
        {
            new { goodId = -1, requestId = "negative" }, new { requestId = "missing-good" },
            new { goodId = 1 }, new { goodId = 1, requestId = (string?)null },
            new { goodId = 1, requestId = " " }, new { goodId = 1, requestId = new string('x', 129) }
        })
        {
            var error = await Send(HttpMethod.Post, "/purchases", body, token, 400);
            Assert(error.GetProperty("code").GetString() == "invalid_request", "Wrong purchase validation error");
        }
        foreach (var requestId in new[] { "x", new string('x', 128), "First", " first " })
            await Send(HttpMethod.Post, "/purchases", new { goodId = 1001, requestId }, token, 404, "good_not_found");
        var retry = await Send(HttpMethod.Post, "/purchases", new { goodId = 3, requestId = "x" }, token, 200);
        Assert(retry.GetProperty("balance").GetInt32() == 950, "Missing good reserved requestId or changed balance");
    }

    public async Task PurchaseRetries()
    {
        var token = await Login("player", "player123");
        var purchase = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "first" }, token, 200);
        Assert(purchase.GetProperty("balance").GetInt32() == 900, "Wrong balance");
        Assert(purchase.GetProperty("quantity").GetInt32() == 1, "Wrong initial quantity");
        var repeated = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "first" }, token, 200);
        Assert(purchase.ToString() == repeated.ToString(), "Retry response changed");
        await Send(HttpMethod.Post, "/purchases", new { goodId = 2, requestId = "first" }, token, 409, "request_id_conflict");
        var conflict = await Send(HttpMethod.Post, "/purchases", new { goodId = 1001, requestId = "first" }, token, 409);
        Assert(conflict.GetProperty("code").GetString() == "request_id_conflict", "Missing good hid request conflict");
        await Send(HttpMethod.Post, "/purchases", new { goodId = 1001, requestId = "missing" }, token, 404);
        await Send(HttpMethod.Post, "/purchases", new { goodId = 3, requestId = "missing" }, token, 200);
        await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "First" }, token, 200);
        await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = " first " }, token, 200);
        repeated = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "first" }, token, 200);
        Assert(purchase.ToString() == repeated.ToString(), "Later purchases changed the original receipt");
    }

    public async Task ConcurrentPurchases()
    {
        var token = await Login("player", "player123");
        var purchase = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "first" }, token, 200);
        var secondToken = await Login("player", "player123");
        var me = await Send(HttpMethod.Get, "/player", null, secondToken, 200);
        Assert(me.GetProperty("balance").GetInt32() == 900, "Login reset balance");
        var parallelRetries = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "parallel-retry" }, secondToken, 200)));
        Assert(parallelRetries.All(result => result.GetProperty("quantity").GetInt32() == 2
            && result.GetProperty("balance").GetInt32() == 800), "Parallel retry changed receipt or quantity");
        me = await Send(HttpMethod.Get, "/player", null, token, 200);
        Assert(me.GetProperty("balance").GetInt32() == 800, "Parallel retry charged multiple times");
        var repeated = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "first" }, secondToken, 200);
        Assert(purchase.ToString() == repeated.ToString(), "Later purchases changed the original receipt");
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/purchases");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", i % 2 == 0 ? token : secondToken);
            request.Content = JsonContent.Create(new { goodId = 1, requestId = $"parallel-{i}" });
            using var response = await client.SendAsync(request);
            Assert(response.Content.Headers.ContentType?.MediaType == "application/json", "Purchase must return JSON");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var status = (int)response.StatusCode;
            var quantity = 0;
            if (status == 200)
            {
                quantity = body.RootElement.GetProperty("quantity").GetInt32();
                Assert(body.RootElement.GetProperty("requestId").GetString() == $"parallel-{i}"
                    && body.RootElement.GetProperty("goodId").GetInt32() == 1
                    && body.RootElement.GetProperty("price").GetInt32() == 100
                    && body.RootElement.GetProperty("balance").GetInt32() == 1000 - quantity * 100,
                    "Concurrent receipt does not match its purchase");
            }
            else
                Assert(status == 409 && body.RootElement.GetProperty("code").GetString() == "insufficient_funds",
                    "Unexpected concurrent purchase failure");
            var snapshot = await Send(HttpMethod.Get, "/player", null, token, 200);
            Assert(snapshot.GetProperty("balance").GetInt32() >= 0, "Concurrent snapshot has negative balance");
            return (Status: status, Quantity: quantity);
        }));
        Assert(results.Count(x => x.Status == 200) == 8 && results.Count(x => x.Status == 409) == 12,
            "Concurrent spending is not atomic");
        Assert(results.Where(x => x.Status == 200).Select(x => x.Quantity).Order()
            .SequenceEqual(Enumerable.Range(3, 8)), "Concurrent purchases lost or duplicated inventory items");
        me = await Send(HttpMethod.Get, "/player", null, token, 200);
        Assert(me.GetProperty("balance").GetInt32() == 0, "Invalid final balance");
        Assert(me.EnumerateObject().Count() == 3 && me.GetProperty("id").GetInt32() == 1
            && me.GetProperty("login").GetString() == "player" && !me.TryGetProperty("goods", out _),
            "Player must return only id, login and balance");
        var maxRequestId = new string('x', 128);
        await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = maxRequestId }, token, 409);
        repeated = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "first" }, token, 200);
        Assert(purchase.ToString() == repeated.ToString(), "Empty balance prevented successful replay");
    }

    public async Task Logout()
    {
        var token = await Login("player", "player123");
        var secondToken = await Login("player", "player123");
        await Send(HttpMethod.Post, "/auth/logout", null, token, 204);
        await Send(HttpMethod.Get, "/items", null, token, 401);
        await Send(HttpMethod.Get, "/player", null, secondToken, 200);
    }

    public async Task InsufficientFunds()
    {
        var token = await Login("poor", "poor123");
        var insufficient = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "poor" }, token, 409);
        Assert(insufficient.GetProperty("code").GetString() == "insufficient_funds", "Wrong error");
        var poor = await Send(HttpMethod.Get, "/player", null, token, 200);
        Assert(poor.GetProperty("balance").GetInt32() == 50 && poor.GetProperty("id").GetInt32() == 2
            && poor.GetProperty("login").GetString() == "poor" && !poor.TryGetProperty("goods", out _),
            "Failed purchase changed player");
        var retry = await Send(HttpMethod.Post, "/purchases", new { goodId = 3, requestId = "poor" }, token, 200);
        Assert(retry.GetProperty("balance").GetInt32() == 0 && retry.GetProperty("quantity").GetInt32() == 1,
            "Failed purchase reserved requestId or changed inventory");
    }

    public async Task AccountAndInventoryIsolation()
    {
        var first = await Login("player", "player123");
        var second = await Login("poor", "poor123");
        var sword = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "shared" }, first, 200);
        var potion = await Send(HttpMethod.Post, "/purchases", new { goodId = 3, requestId = "shared" }, second, 200);
        Assert(sword.GetProperty("balance").GetInt32() == 900 && potion.GetProperty("balance").GetInt32() == 0,
            "Request IDs are not isolated by account");
        var shield = await Send(HttpMethod.Post, "/purchases", new { goodId = 2, requestId = "shield" }, first, 200);
        var nextSword = await Send(HttpMethod.Post, "/purchases", new { goodId = 1, requestId = "sword" }, first, 200);
        Assert(shield.GetProperty("quantity").GetInt32() == 1 && nextSword.GetProperty("quantity").GetInt32() == 2
            && nextSword.GetProperty("balance").GetInt32() == 650, "Inventory quantities are not per good");
    }
}
