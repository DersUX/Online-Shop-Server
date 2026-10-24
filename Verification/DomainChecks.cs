using OnlineShopServer;
using OnlineShopServer.Models;
using OnlineShopServer.Security;
using OnlineShopServer.Services;
using OnlineShopServer.Storage;
using static Verification.Check;

namespace Verification;

internal static class DomainChecks
{
    public static void CustomStorageData()
    {
        var good = new Good(42, "Test", "Custom item", 7, GoodType.Sword);
        Good[] source = [good];
        var catalog = new GoodsCatalog(source);
        source[0] = new Good(43, "Other", "Replacement", 10, GoodType.Shield);
        Assert(catalog.FindById(42) == good && catalog.Goods.Single() == good,
            "Catalog must copy the supplied collection");
        Assert(catalog.FindById(43) is null, "Unknown item must not be found");
        Throws<ArgumentException>(() => new GoodsCatalog([good, good]));

        var player = new PlayerAccount(42, "custom", 10);
        Throws<ArgumentNullException>(() => new AccountCredentials(null!, "password"));
        Throws<ArgumentException>(() => new AccountCredentials(player, ""));
        var credentials = new AccountCredentials(player, "custom-password");
        AccountCredentials[] entries = [credentials];
        var accounts = new AccountStore(entries);
        entries[0] = new AccountCredentials(new PlayerAccount(43, "other", 10), "other-password");
        var authentication = new AuthenticationService(accounts);
        Assert(authentication.Authenticate("custom", "custom-password") == player,
            "Custom account authentication failed");
        Assert(authentication.Authenticate("custom", "wrong") is null, "Wrong custom password accepted");
        Assert(accounts.GetPlayer(42) == player, "Account store must copy the supplied collection");
        Throws<ArgumentException>(() => new AccountStore([credentials, credentials]));
        Throws<ArgumentException>(() => new AccountStore([credentials,
            new AccountCredentials(new PlayerAccount(43, "custom", 10), "password")]));
        Throws<ArgumentException>(() => new AccountStore([credentials,
            new AccountCredentials(new PlayerAccount(42, "different", 10), "password")]));
        var result = new PurchaseService(accounts, catalog).Purchase(42, 42, "custom-purchase");
        Assert(result.Receipt is { Balance: 3, Quantity: 1, Price: 7 }, "Purchase ignored custom storage data");
    }

    public static void ModelInvariants()
    {
        foreach (var id in new[] { 0, -1 })
        {
            Throws<ArgumentOutOfRangeException>(() => new PlayerAccount(id, "test", 100));
            Throws<ArgumentOutOfRangeException>(() => new Good(id, "Sword", "Test", 100, GoodType.Sword));
        }
        foreach (var login in new[] { null, "", " ", new string('x', 65) })
            Throws<ArgumentException>(() => new PlayerAccount(1, login!, 100));
        Throws<ArgumentOutOfRangeException>(() => new PlayerAccount(1, "test", -1));
        Throws<ArgumentOutOfRangeException>(() => new Good(1, "Sword", "Test", -1, GoodType.Sword));
        Throws<ArgumentException>(() => new Good(1, " ", "Test", 1, GoodType.Sword));
        Throws<ArgumentException>(() => new Good(1, "Sword", null!, 1, GoodType.Sword));
        Throws<ArgumentOutOfRangeException>(() => new Good(1, "Sword", "Test", 1, (GoodType)99));
        _ = new PlayerAccount(1, new string('x', 64), 0);
        _ = new Good(1, "Free", "Test", 0, GoodType.Unknown);

        Throws<ArgumentNullException>(() => PurchaseResult.Success(null!));
        Throws<ArgumentOutOfRangeException>(() => PurchaseResult.Failure(PurchaseStatus.Success));
        Throws<ArgumentOutOfRangeException>(() => PurchaseResult.Failure((PurchaseStatus)99));
        var receipt = new PurchaseReceipt("test", 1, 100, 0, 1);
        var success = PurchaseResult.Success(receipt);
        Assert(success.Status == PurchaseStatus.Success && success.Receipt == receipt, "Invalid success result");
        foreach (var status in new[] { PurchaseStatus.RequestIdConflict, PurchaseStatus.GoodNotFound, PurchaseStatus.InsufficientFunds })
        {
            var failure = PurchaseResult.Failure(status);
            Assert(failure.Status == status && failure.Receipt is null, "Invalid failure result");
        }
    }

    public static void Purchases()
    {
        var account = new PlayerAccount(42, "test", 100);
        var good = new Good(1, "Sword", "Test", 100, GoodType.Sword);
        foreach (var requestId in new[] { null, "", " ", new string('x', 129) })
            Throws<ArgumentException>(() => account.Purchase(1, requestId!, good));
        Throws<ArgumentOutOfRangeException>(() => account.Purchase(0, "test", good));
        Throws<ArgumentException>(() => account.Purchase(2, "mismatched-id", good));
        Assert(account.Purchase(1, "first", null).Status == PurchaseStatus.GoodNotFound,
            "Missing good must reject a new purchase");
        Assert(account.Snapshot().Balance == 100, "Rejected purchase changed balance");

        var receipt = account.Purchase(1, "first", good).Receipt;
        Assert(receipt is { Balance: 0, Quantity: 1 }, "Domain purchase failed");
        Assert(account.Purchase(1, "first", null).Receipt == receipt,
            "Replay must precede missing good and balance checks");
        Assert(account.Purchase(2, "first", null).Status == PurchaseStatus.RequestIdConflict,
            "Request conflict must precede missing good checks");
    }

    public static void InventoryAndAccountIsolation()
    {
        var goods = DemoData.CreateCatalog();
        var first = new PlayerAccount(1, "first", 1000);
        var second = new PlayerAccount(2, "second", 1000);
        Assert(first.Purchase(1, "shared", goods.FindById(1)).Receipt is { Quantity: 1, Balance: 900 }, "First purchase failed");
        Assert(second.Purchase(2, "shared", goods.FindById(2)).Receipt is { Quantity: 1, Balance: 850 }, "Request IDs leaked across accounts");
        Assert(first.Purchase(2, "shield", goods.FindById(2)).Receipt is { Quantity: 1, Balance: 750 }, "Different goods share quantity");
        Assert(first.Purchase(1, "sword", goods.FindById(1)).Receipt is { Quantity: 2, Balance: 650 }, "Repeated good lost quantity");
        Assert(first.Purchase(2, "shield-again", goods.FindById(2)).Receipt is { Quantity: 2, Balance: 500 }, "Second good lost quantity");
        Assert(second.Snapshot().Balance == 850, "Accounts share balance");
    }

    public static void Sessions()
    {
        var clock = new TestClock();
        var sessions = new SessionStore(clock);
        var first = sessions.Create(1);
        var second = sessions.Create(1);
        Assert(first.Token != second.Token && first.Token.Length == 64, "Invalid session tokens");
        Assert(sessions.Find(first.Token)?.PlayerId == 1, "Session creation failed");
        sessions.Remove(first.Token);
        Assert(sessions.Find(first.Token) is null && sessions.Find(second.Token) is not null, "Logout affected another session");
        clock.Now = second.Session.ExpiresAt.AddTicks(-1);
        Assert(sessions.Find(second.Token) is not null, "Session expired too early");
        clock.Now = second.Session.ExpiresAt;
        Assert(sessions.Find(second.Token) is null, "Expired session accepted");
    }

    public static void Authentication()
    {
        var accounts = DemoData.CreateAccounts();
        var authentication = new AuthenticationService(accounts);
        Assert(authentication.Authenticate("player", "player123") == accounts.GetPlayer(1), "Wrong account returned");
        Assert(authentication.Authenticate("player", "wrong") is null, "Wrong password accepted");
        Assert(authentication.Authenticate("missing", "player123") is null, "Unknown account accepted");
        Assert(authentication.Authenticate("Player", "player123") is null, "Login lost case sensitivity");
    }

    public static void SessionCleanup()
    {
        var clock = new TestClock();
        var sessions = new SessionStore(clock);
        var first = sessions.Create(1);
        clock.Now = clock.Now.AddMinutes(30);
        var second = sessions.Create(2);
        clock.Now = first.Session.ExpiresAt.AddTicks(-1);
        Assert(sessions.RemoveExpired() == 0, "Cleanup removed an active session");
        clock.Now = first.Session.ExpiresAt;
        Assert(sessions.RemoveExpired() == 1, "Cleanup must remove expired sessions without token access");
        Assert(sessions.RemoveExpired() == 0, "Cleanup did not remove the entry");
        Assert(sessions.Find(first.Token) is null && sessions.Find(second.Token) is not null,
            "Cleanup affected the wrong session");
        clock.Now = second.Session.ExpiresAt;
        Assert(sessions.RemoveExpired() == 1, "Cleanup missed the remaining expired session");
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
