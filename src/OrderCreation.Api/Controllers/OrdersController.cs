using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCreation.Api.Application;
using OrderCreation.Api.Contracts;
using OrderCreation.Api.Infrastructure;

namespace OrderCreation.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController(
    IOrderService orderService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string GetOrderRouteName = "GetOrderById";

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanCreateOrders)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderResponse>> CreateOrderAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            currentUser.Id,
            request.ClientOrderId,
            request.Symbol,
            request.Price,
            request.Volume);

        var result = await orderService.CreateOrderAsync(command, cancellationToken);

        if (!result.IsCreated)
            return Ok(result.Order);

        return CreatedAtRoute(
            GetOrderRouteName,
            new { orderId = result.Order.Id },
            result.Order);
    }

    [HttpGet("{orderId:guid}", Name = GetOrderRouteName)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(
            currentUser.Id,
            orderId,
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }
}
