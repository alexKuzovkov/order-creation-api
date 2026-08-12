using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCreation.Api.Application;
using OrderCreation.Api.Contracts;
using OrderCreation.Api.Infrastructure;

namespace OrderCreation.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize(Policy = AuthorizationPolicies.CanCreateOrders)]
public sealed class OrdersController(
    ICreateOrderService orderService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
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

        var order = await orderService.CreateOrderAsync(command, cancellationToken);

        return Created($"/api/v1/orders/{order.Id}", order);
    }
}
