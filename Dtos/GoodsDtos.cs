using OnlineShopServer.Models;

namespace OnlineShopServer.Dtos;

public sealed record GoodDto(
    int Id,
    string Header,
    string Description,
    int Price,
    GoodType TypeId);

public sealed record GoodsResponseDto(IReadOnlyList<GoodDto> Goods);
