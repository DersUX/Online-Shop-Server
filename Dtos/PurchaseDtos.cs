namespace OnlineShopServer.Dtos;

public sealed record PurchaseRequestDto(int GoodId, string? RequestId);
public sealed record PurchaseResponseDto(string RequestId, int GoodId, int Price, int Balance, int Quantity);
