namespace OnlineShopServer.Models;

public sealed class PurchaseResult
{
    public PurchaseStatus Status { get; }
    public PurchaseReceipt? Receipt { get; }

    private PurchaseResult(PurchaseStatus status, PurchaseReceipt? receipt)
    {
        Status = status;
        Receipt = receipt;
    }

    public static PurchaseResult Success(PurchaseReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new(PurchaseStatus.Success, receipt);
    }

    public static PurchaseResult Failure(PurchaseStatus status)
    {
        if (status is not (PurchaseStatus.RequestIdConflict or PurchaseStatus.GoodNotFound or PurchaseStatus.InsufficientFunds))
            throw new ArgumentOutOfRangeException(nameof(status));
        return new(status, null);
    }
}
