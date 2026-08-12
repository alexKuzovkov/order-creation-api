using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderCreation.Api.Application;
using OrderCreation.Api.Domain;

namespace OrderCreation.Api.Infrastructure;

public static class AuthorizationPolicies
{
    public const string CanCreateOrders = nameof(CanCreateOrders);
}

public interface ICurrentUser
{
    Guid Id { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id => Guid.TryParse(
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new UnauthorizedAccessException("Authenticated user identifier is missing.");
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private static readonly ConcurrentDictionary<(Guid, string), Order> Orders = new();

    public Task<Order?> FindByClientOrderIdAsync(
        Guid userId, string clientOrderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Orders.TryGetValue((userId, clientOrderId), out var order);
        return Task.FromResult(order);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Orders.TryAdd((order.UserId, order.ClientOrderId), order))
            throw new DuplicateOrderException("An order with this ClientOrderId already exists.");
        return Task.CompletedTask;
    }
}

public sealed class OrderTradingValidator : IOrderTradingValidator
{
    public Task ValidateAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Здесь проверяются инструмент, торговая сессия, tick/lot size, баланс и лимиты.
        return Task.CompletedTask;
    }
}

public sealed class DuplicateOrderException(string message) : Exception(message);

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            UnauthorizedAccessException => (401, "Authentication required", exception.Message),
            DuplicateOrderException => (409, "Order already exists", exception.Message),
            DomainValidationException or TradingRuleViolationException =>
                (422, "Trading rule violation", exception.Message),
            _ => (500, "Internal server error", "An unexpected error occurred.")
        };

        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception, "Request failed with status {StatusCode}; TraceId: {TraceId}",
            status, context.TraceIdentifier);

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            }
        });
    }
}
