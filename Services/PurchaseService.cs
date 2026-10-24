using OnlineShopServer.Models;
using OnlineShopServer.Storage;

namespace OnlineShopServer.Services;

public sealed class PurchaseService(AccountStore accounts, GoodsCatalog catalog)
{
    public PurchaseResult Purchase(int playerId, int goodId, string requestId)
    {
        var player = accounts.GetPlayer(playerId);
        var good = catalog.FindById(goodId);
        return player.Purchase(goodId, requestId, good);
    }
}
