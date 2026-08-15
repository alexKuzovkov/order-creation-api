using Xunit;
using OrderCreation.Api.Domain;

namespace OrderCreation.Api.Tests;

public sealed class OrderTests
{
    [Fact]
    public void Create_WithValidValues_CreatesActiveOrder()
    {
        var now = DateTimeOffset.Parse("2026-08-15T10:00:00Z");

        var order = Order.Create(
            Guid.NewGuid(),
            "client-order-001",
            "BTCUSD",
            100m,
            2m,
            now);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(OrderState.Active, order.State);
        Assert.Equal("BTCUSD", order.Symbol);
        Assert.Equal(now, order.CreatedAt);
    }

    public static TheoryData<decimal, decimal> InvalidPriceOrVolume => new()
    {
        { 0m, 1m },
        { -1m, 1m },
        { 1m, 0m },
        { 1m, -1m }
    };

    [Theory]
    [MemberData(nameof(InvalidPriceOrVolume))]
    public void Create_WithNonPositivePriceOrVolume_Throws(decimal price, decimal volume)
    {
        Assert.Throws<DomainValidationException>(() => Order.Create(
            Guid.NewGuid(),
            "client-order-001",
            "BTCUSD",
            price,
            volume,
            DateTimeOffset.UtcNow));
    }
}
