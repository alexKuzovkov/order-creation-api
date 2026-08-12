using OrderCreation.Api.Contracts;
using OrderCreation.Api.Domain;

namespace OrderCreation.Api.Application;

public sealed record CreateOrderCommand(
    Guid UserId, string ClientOrderId, string Symbol, decimal Price, decimal Volume);

public interface ICreateOrderService
{
    Task<OrderResponse> CreateOrderAsync(
        CreateOrderCommand command, CancellationToken cancellationToken);
}

public interface IOrderRepository
{
    Task<Order?> FindByClientOrderIdAsync(
        Guid userId, string clientOrderId, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);
}

public interface IOrderTradingValidator
{
    Task ValidateAsync(CreateOrderCommand command, CancellationToken cancellationToken);
}

public sealed class CreateOrderService(
    IOrderRepository repository,
    IOrderTradingValidator tradingValidator,
    TimeProvider timeProvider) : ICreateOrderService
{
    public async Task<OrderResponse> CreateOrderAsync(
        CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.FindByClientOrderIdAsync(
            command.UserId, command.ClientOrderId, cancellationToken);

        // Повтор запроса возвращает прежний результат, а не создает второй ордер.
        if (existing is not null) return Map(existing);

        await tradingValidator.ValidateAsync(command, cancellationToken);

        var order = Order.Create(
            command.UserId,
            command.ClientOrderId,
            command.Symbol.Trim().ToUpperInvariant(),
            command.Price,
            command.Volume,
            timeProvider.GetUtcNow());

        await repository.AddAsync(order, cancellationToken);
        return Map(order);
    }

    private static OrderResponse Map(Order order) => new(
        order.Id, order.ClientOrderId, order.Symbol, order.Price,
        order.Volume, order.Status, order.CreatedAt);
}

public sealed class TradingRuleViolationException(string message) : Exception(message);
