using Xunit;
using Microsoft.Extensions.Options;
using OrderCreation.Api.Application;
using OrderCreation.Api.Infrastructure;

namespace OrderCreation.Api.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_RetryWithSamePayload_ReturnsExistingOrder()
    {
        var service = CreateService();
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "client-order-001",
            " btcusd ",
            100m,
            2m);

        var first = await service.CreateOrderAsync(command, CancellationToken.None);
        var retry = await service.CreateOrderAsync(command, CancellationToken.None);

        Assert.True(first.IsCreated);
        Assert.False(retry.IsCreated);
        Assert.Equal(first.Order.Id, retry.Order.Id);
        Assert.Equal("BTCUSD", retry.Order.Symbol);
    }

    [Fact]
    public async Task CreateOrderAsync_SameIdempotencyKeyDifferentPayload_ThrowsConflict()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        await service.CreateOrderAsync(
            new CreateOrderCommand(userId, "client-order-001", "BTCUSD", 100m, 2m),
            CancellationToken.None);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            service.CreateOrderAsync(
                new CreateOrderCommand(userId, "client-order-001", "BTCUSD", 101m, 2m),
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_ConcurrentRetries_ReturnSameOrder()
    {
        var service = CreateService();
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "client-order-concurrent",
            "ETHUSD",
            200m,
            1m);

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => service.CreateOrderAsync(command, CancellationToken.None)));

        Assert.Single(results.Where(result => result.IsCreated));
        Assert.Single(results.Select(result => result.Order.Id).Distinct());
    }

    [Fact]
    public async Task CreateOrderAsync_AboveMaxNotional_ThrowsTradingRuleViolation()
    {
        var service = CreateService(maxNotional: 1_000m);

        await Assert.ThrowsAsync<TradingRuleViolationException>(() =>
            service.CreateOrderAsync(
                new CreateOrderCommand(
                    Guid.NewGuid(),
                    "client-order-001",
                    "BTCUSD",
                    501m,
                    2m),
                CancellationToken.None));
    }

    private static OrderService CreateService(decimal maxNotional = 10_000_000m)
    {
        var repository = new InMemoryOrderRepository();
        var validator = new OrderTradingValidator(
            Options.Create(new OrderTradingOptions { MaxNotional = maxNotional }));

        return new OrderService(repository, validator, TimeProvider.System);
    }
}
