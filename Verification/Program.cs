using Verification;

var failures = 0;

async Task Run(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {name}\n{exception}");
    }
}

Task RunDomain(Action test)
{
    test();
    return Task.CompletedTask;
}

async Task RunHttp(Func<HttpChecks, Task> test)
{
    await using var server = new TestServer();
    await server.WaitUntilReady();
    await test(new HttpChecks(server.Client));
}

await Run("Model invariants", () => RunDomain(DomainChecks.ModelInvariants));
await Run("Domain purchases and retries", () => RunDomain(DomainChecks.Purchases));
await Run("Inventory and account isolation", () => RunDomain(DomainChecks.InventoryAndAccountIsolation));
await Run("Session lifecycle", () => RunDomain(DomainChecks.Sessions));
await Run("Expired session cleanup", () => RunDomain(DomainChecks.SessionCleanup));
await Run("Authentication", () => RunDomain(DomainChecks.Authentication));
await Run("Custom storage data", () => RunDomain(DomainChecks.CustomStorageData));
await Run("HTTP unexpected failure", ExceptionHandlingChecks.UnexpectedFailure);
await Run("Test server dynamic ports", TestServerChecks.DynamicPorts);
await Run("Test server startup diagnostics", TestServerChecks.StartupFailure);
await Run("HTTP authorization and login validation", () => RunHttp(checks => checks.Authorization()));
await Run("HTTP error contract", () => RunHttp(checks => checks.HttpErrors()));
await Run("HTTP login rate limit", () => RunHttp(checks => checks.LoginRateLimit()));
await Run("HTTP catalog contract and prices", () => RunHttp(checks => checks.Catalog()));
await Run("HTTP purchase validation", () => RunHttp(checks => checks.PurchaseValidation()));
await Run("HTTP purchase retries", () => RunHttp(checks => checks.PurchaseRetries()));
await Run("HTTP concurrent purchases", () => RunHttp(checks => checks.ConcurrentPurchases()));
await Run("HTTP logout", () => RunHttp(checks => checks.Logout()));
await Run("HTTP insufficient funds", () => RunHttp(checks => checks.InsufficientFunds()));
await Run("HTTP account and inventory isolation", () => RunHttp(checks => checks.AccountAndInventoryIsolation()));

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
