namespace OnlineShopServer.Models;

public sealed record PurchaseReceipt(string RequestId, int GoodId, int Price, int Balance, int Quantity);
