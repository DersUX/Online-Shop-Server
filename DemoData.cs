using OnlineShopServer.Models;
using OnlineShopServer.Security;
using OnlineShopServer.Storage;

namespace OnlineShopServer;

public static class DemoData
{
    public static AccountStore CreateAccounts() => new(
    [
        new(new PlayerAccount(1, "player", 1000), "player123"),
        new(new PlayerAccount(2, "poor", 50), "poor123")
    ]);

    public static GoodsCatalog CreateCatalog()
    {
        Good[] templates =
        [
            new(1, "Меч", "Острый меч", 100, GoodType.Sword),
            new(2, "Щит", "Прочный деревянный щит", 150, GoodType.Shield),
            new(3, "Зелье здоровья", "Восстанавливает 50 единиц здоровья", 50, GoodType.Potion),
            new(4, "Шлем", "Лёгкий кожаный шлем", 80, GoodType.Helmet),
            new(5, "Лук", "Охотничий лук с набором стрел", 120, GoodType.Bow),
            new(6, "Броня", "Прочная кожаная броня", 200, GoodType.Armor),
            new(7, "Ботинки", "Удобные кожаные ботинки", 70, GoodType.Boots)
        ];

        var goods = Enumerable.Range(1, 1000).Select(id =>
        {
            var template = templates[(id - 1) % templates.Length];
            var tier = (id - 1) / templates.Length;
            return new Good(id, template.Header, template.Description, template.Price + tier * 25, template.TypeId);
        }).ToArray();
        Random.Shared.Shuffle(goods);
        return new GoodsCatalog(goods);
    }
}
