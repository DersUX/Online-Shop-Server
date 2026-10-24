namespace OnlineShopServer.Models;

public sealed record Good
{
    public int Id { get; }
    public string Header { get; }
    public string Description { get; }
    public int Price { get; }
    public GoodType TypeId { get; }

    public Good(int id, string header, string description, int price, GoodType typeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        if (!Enum.IsDefined(typeId))
            throw new ArgumentOutOfRangeException(nameof(typeId));

        Id = id;
        Header = header;
        Description = description;
        Price = price;
        TypeId = typeId;
    }
}
