using OnlineShopServer.Models;

namespace OnlineShopServer.Storage;

public sealed class GoodsCatalog
{
    private readonly Dictionary<int, Good> byId;
    public IReadOnlyList<Good> Goods { get; }

    public GoodsCatalog(IEnumerable<Good> goods)
    {
        var items = goods.ToArray();
        Goods = Array.AsReadOnly(items);
        byId = items.ToDictionary(good => good.Id);
    }

    public Good? FindById(int id) => byId.GetValueOrDefault(id);
}
