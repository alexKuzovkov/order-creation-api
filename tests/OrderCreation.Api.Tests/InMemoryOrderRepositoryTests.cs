using Xunit;
using OrderCreation.Api.Domain;
using OrderCreation.Api.Infrastructure;

namespace OrderCreation.Api.Tests;

public sealed class InMemoryOrderRepositoryTests
{
    [Fact]
    public async Task GetOrAddAsync_ConcurrentSameKey_StoresExactlyOneOrder()
    {
        var repository = new InMemoryOrderRepository();
        var userId = Guid.NewGuid();
        const string clientOrderId = "same-client-order";

        var candidates = Enumerable.Range(0, 32)
            .Select(_ => Order.Create(
                userId,
                clientOrderId,
                "BTCUSD",
                100m,
                1m,
                DateTimeOffset.UtcNow))
            .ToArray();

        var results = await Task.WhenAll(candidates.Select(order =>
            repository.GetOrAddAsync(order, CancellationToken.None)));

        Assert.Single(results.Where(result => result.IsCreated));
        Assert.Single(results.Select(result => result.Order.Id).Distinct());
    }

    [Fact]
    public async Task FindByIdAsync_DoesNotExposeAnotherUsersOrder()
    {
        var repository = new InMemoryOrderRepository();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var order = Order.Create(
            ownerId,
            "client-order-001",
            "BTCUSD",
            100m,
            1m,
            DateTimeOffset.UtcNow);

        await repository.GetOrAddAsync(order, CancellationToken.None);

        var result = await repository.FindByIdAsync(
            otherUserId,
            order.Id,
            CancellationToken.None);

        Assert.Null(result);
    }
}
