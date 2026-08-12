namespace OrderCreation.Api.Domain;

public sealed class Order
{
    private Order(Guid id, Guid userId, string clientOrderId, string symbol,
        decimal price, decimal volume, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        ClientOrderId = clientOrderId;
        Symbol = symbol;
        Price = price;
        Volume = volume;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid UserId { get; }
    public string ClientOrderId { get; }
    public string Symbol { get; }
    public decimal Price { get; }
    public decimal Volume { get; }
    public string Status => "Created";
    public DateTimeOffset CreatedAt { get; }

    public static Order Create(Guid userId, string clientOrderId, string symbol,
        decimal price, decimal volume, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty) throw new DomainValidationException("User is required.");
        if (string.IsNullOrWhiteSpace(symbol)) throw new DomainValidationException("Symbol is required.");
        if (price <= 0) throw new DomainValidationException("Price must be greater than zero.");
        if (volume <= 0) throw new DomainValidationException("Volume must be greater than zero.");

        return new Order(Guid.NewGuid(), userId, clientOrderId, symbol, price, volume, createdAt);
    }
}

public sealed class DomainValidationException(string message) : Exception(message);
