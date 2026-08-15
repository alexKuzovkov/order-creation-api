using System.Collections.Concurrent;
using OrderCreation.Api.Application;
using OrderCreation.Api.Domain;

namespace OrderCreation.Api.Infrastructure;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<(Guid UserId, string ClientOrderId), Order> _orders = new();

    public Task<Order?> FindByClientOrderIdAsync(
        Guid userId,
        string clientOrderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _orders.TryGetValue((userId, clientOrderId), out var order);
        return Task.FromResult(order);
    }

    public Task<Order?> FindByIdAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = _orders.Values.FirstOrDefault(
            candidate => candidate.UserId == userId && candidate.Id == orderId);

        return Task.FromResult(order);
    }

    public Task<StoreOrderResult> GetOrAddAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = (order.UserId, order.ClientOrderId);
        var stored = _orders.GetOrAdd(key, order);
        return Task.FromResult(new StoreOrderResult(stored, ReferenceEquals(stored, order)));
    }
}
