namespace OnlineShopServer.Models;

public sealed class PlayerAccount
{
    public const int MaxLoginLength = 64;
    public const int MaxRequestIdLength = 128;

    private readonly object syncRoot = new();
    private readonly Dictionary<int, int> inventory = new();
    private readonly Dictionary<string, PurchaseReceipt> purchases = new(StringComparer.Ordinal);
    private int balance;

    public int Id { get; }
    public string Login { get; }

    public PlayerAccount(int id, string login, int balance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        if (login.Length > MaxLoginLength)
            throw new ArgumentException("Login is too long", nameof(login));
        ArgumentOutOfRangeException.ThrowIfNegative(balance);
        Id = id;
        Login = login;
        this.balance = balance;
    }

    public PlayerSnapshot Snapshot()
    {
        lock (syncRoot)
            return new(Id, Login, balance);
    }

    public PurchaseResult Purchase(int goodId, string requestId, Good? good)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(goodId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (requestId.Length > MaxRequestIdLength)
            throw new ArgumentException("requestId is too long", nameof(requestId));

        lock (syncRoot)
        {
            if (purchases.TryGetValue(requestId, out var previous))
                return previous.GoodId == goodId
                    ? PurchaseResult.Success(previous)
                    : PurchaseResult.Failure(PurchaseStatus.RequestIdConflict);

            if (good is null)
                return PurchaseResult.Failure(PurchaseStatus.GoodNotFound);
            if (good.Id != goodId)
                throw new ArgumentException("Invalid catalog item", nameof(good));
            if (balance < good.Price)
                return PurchaseResult.Failure(PurchaseStatus.InsufficientFunds);

            var quantity = checked(inventory.GetValueOrDefault(good.Id) + 1);
            var receipt = new PurchaseReceipt(requestId, good.Id, good.Price, balance - good.Price, quantity);
            balance = receipt.Balance;
            inventory[good.Id] = quantity;
            purchases.Add(requestId, receipt);
            return PurchaseResult.Success(receipt);
        }
    }
}
