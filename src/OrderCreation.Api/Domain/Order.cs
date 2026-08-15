namespace OrderCreation.Api.Domain;

public enum OrderState
{
    Active = 1,
    Completed = 2,
    Inactive = 3,
    Faulted = 4
}

public sealed class Order
{
    private Order(
        Guid id,
        Guid userId,
        string clientOrderId,
        string symbol,
        decimal price,
        decimal volume,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        ClientOrderId = clientOrderId;
        Symbol = symbol;
        Price = price;
        Volume = volume;
        State = OrderState.Active;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid UserId { get; }
    public string ClientOrderId { get; }
    public string Symbol { get; }
    public decimal Price { get; }
    public decimal Volume { get; }
    public OrderState State { get; }
    public DateTimeOffset CreatedAt { get; }

    public static Order Create(
        Guid userId,
        string clientOrderId,
        string symbol,
        decimal price,
        decimal volume,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
            throw new DomainValidationException("User is required.");
        if (string.IsNullOrWhiteSpace(clientOrderId))
            throw new DomainValidationException("ClientOrderId is required.");
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainValidationException("Symbol is required.");
        if (price <= 0)
            throw new DomainValidationException("Price must be greater than zero.");
        if (volume <= 0)
            throw new DomainValidationException("Volume must be greater than zero.");

        return new Order(
            Guid.NewGuid(),
            userId,
            clientOrderId,
            symbol,
            price,
            volume,
            createdAt);
    }

    public bool MatchesRequest(string symbol, decimal price, decimal volume) =>
        string.Equals(Symbol, symbol, StringComparison.Ordinal) &&
        Price == price &&
        Volume == volume;
}

public sealed class DomainValidationException(string message) : Exception(message);
