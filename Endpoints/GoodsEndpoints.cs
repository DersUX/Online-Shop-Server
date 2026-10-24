using OnlineShopServer.Dtos;
using OnlineShopServer.Storage;

namespace OnlineShopServer.Endpoints;

public static class GoodsEndpoints
{
    public static void MapGoodsEndpoints(this WebApplication app)
    {
        app.MapGet("/items", (GoodsCatalog catalog) =>
            Results.Ok(new GoodsResponseDto(catalog.Goods.Select(good =>
                new GoodDto(good.Id, good.Header, good.Description, good.Price, good.TypeId)).ToArray())))
            .RequireAuthorization();
    }
}
