using Microsoft.Extensions.Options;
using OrderCreation.Api.Contracts;
using OrderCreation.Api.Domain;

namespace OrderCreation.Api.Application;

public sealed record CreateOrderCommand(
    Guid UserId,
    string ClientOrderId,
    string Symbol,
    decimal Price,
    decimal Volume);

public sealed record CreateOrderResult(OrderResponse Order, bool IsCreated);

public sealed record StoreOrderResult(Order Order, bool IsCreated);

public interface IOrderService
{
    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken);

    Task<OrderResponse?> GetOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken);
}

public interface IOrderRepository
{
    Task<Order?> FindByClientOrderIdAsync(
        Guid userId,
        string clientOrderId,
        CancellationToken cancellationToken);

    Task<Order?> FindByIdAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<StoreOrderResult> GetOrAddAsync(
        Order order,
        CancellationToken cancellationToken);
}

public interface IOrderTradingValidator
{
    Task ValidateAsync(CreateOrderCommand command, CancellationToken cancellationToken);
}

public sealed class OrderService(
    IOrderRepository repository,
    IOrderTradingValidator tradingValidator,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedCommand = Normalize(command);

        var existing = await repository.FindByClientOrderIdAsync(
            normalizedCommand.UserId,
            normalizedCommand.ClientOrderId,
            cancellationToken);

        if (existing is not null)
            return ResolveExisting(existing, normalizedCommand);

        await tradingValidator.ValidateAsync(normalizedCommand, cancellationToken);

        var candidate = Order.Create(
            normalizedCommand.UserId,
            normalizedCommand.ClientOrderId,
            normalizedCommand.Symbol,
            normalizedCommand.Price,
            normalizedCommand.Volume,
            timeProvider.GetUtcNow());

        var stored = await repository.GetOrAddAsync(candidate, cancellationToken);
        if (!stored.IsCreated)
            return ResolveExisting(stored.Order, normalizedCommand);

        return new CreateOrderResult(Map(stored.Order), true);
    }

    public async Task<OrderResponse?> GetOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await repository.FindByIdAsync(userId, orderId, cancellationToken);
        return order is null ? null : Map(order);
    }

    private static CreateOrderCommand Normalize(CreateOrderCommand command) =>
        command with
        {
            ClientOrderId = command.ClientOrderId?.Trim() ?? string.Empty,
            Symbol = command.Symbol?.Trim().ToUpperInvariant() ?? string.Empty
        };

    private static CreateOrderResult ResolveExisting(
        Order existing,
        CreateOrderCommand command)
    {
        if (!existing.MatchesRequest(command.Symbol, command.Price, command.Volume))
        {
            throw new IdempotencyConflictException(
                "ClientOrderId is already associated with a different order payload.");
        }

        return new CreateOrderResult(Map(existing), false);
    }

    private static OrderResponse Map(Order order) => new(
        order.Id,
        order.ClientOrderId,
        order.Symbol,
        order.Price,
        order.Volume,
        order.State,
        order.CreatedAt);
}

public sealed class TradingRuleViolationException(string message) : Exception(message);
public sealed class IdempotencyConflictException(string message) : Exception(message);

public sealed class OrderTradingOptions
{
    public const string SectionName = "OrderTrading";
    public decimal MaxNotional { get; init; } = 10_000_000m;
}

public sealed class OrderTradingValidator(IOptions<OrderTradingOptions> options)
    : IOrderTradingValidator
{
    public Task ValidateAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var maxNotional = options.Value.MaxNotional;
        if (maxNotional <= 0)
            throw new InvalidOperationException("OrderTrading:MaxNotional must be greater than zero.");

        if (command.Volume > 0 && command.Price > maxNotional / command.Volume)
        {
            throw new TradingRuleViolationException(
                $"Order notional exceeds the configured limit of {maxNotional}.");
        }

        return Task.CompletedTask;
    }
}
