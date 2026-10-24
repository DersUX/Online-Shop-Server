using System.Security.Claims;
using OnlineShopServer.Dtos;
using OnlineShopServer.Models;
using OnlineShopServer.Security;
using OnlineShopServer.Services;

namespace OnlineShopServer.Endpoints;

public static class PurchaseEndpoints
{
    public static void MapPurchaseEndpoints(this WebApplication app)
    {
        app.MapPost("/purchases", (PurchaseRequestDto request, ClaimsPrincipal user, PurchaseService purchases) =>
        {
            if (request.GoodId <= 0 || string.IsNullOrWhiteSpace(request.RequestId)
                || request.RequestId.Length > PlayerAccount.MaxRequestIdLength)
                return Results.BadRequest(new ErrorDto("invalid_request",
                    $"Нужны goodId > 0 и requestId длиной 1–{PlayerAccount.MaxRequestIdLength} символов"));

            var result = purchases.Purchase(user.GetPlayerId(), request.GoodId, request.RequestId);
            return ToHttpResult(result);
        }).RequireAuthorization();
    }

    private static IResult ToHttpResult(PurchaseResult result) => result.Status switch
    {
        PurchaseStatus.Success when result.Receipt is { } receipt => Results.Ok(
            new PurchaseResponseDto(receipt.RequestId, receipt.GoodId, receipt.Price, receipt.Balance, receipt.Quantity)),
        PurchaseStatus.RequestIdConflict => Results.Conflict(new ErrorDto("request_id_conflict", "requestId уже использован для другого товара")),
        PurchaseStatus.GoodNotFound => Results.NotFound(new ErrorDto("good_not_found", "Товар не найден")),
        PurchaseStatus.InsufficientFunds => Results.Conflict(new ErrorDto("insufficient_funds", "Недостаточно средств")),
        _ => throw new InvalidOperationException("Unexpected purchase result")
    };
}
