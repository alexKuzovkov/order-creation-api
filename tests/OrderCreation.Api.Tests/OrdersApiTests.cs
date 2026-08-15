using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderCreation.Api.Contracts;

namespace OrderCreation.Api.Tests;

public sealed class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public OrdersApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAndGetOrder_ReturnsCreatedThenOk()
    {
        using var client = CreateClient(Guid.NewGuid(), includeCreatePermission: true);
        var request = NewRequest("api-order-001", price: 100m);

        var createResponse = await client.PostAsJsonAsync("/api/v1/orders", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Create response did not contain an order.");
        Assert.Equal("BTCUSD", created.Symbol);
        var location = createResponse.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Created response did not contain a Location header.");
        Assert.EndsWith($"/api/v1/orders/{created.Id}", location);

        var getResponse = await client.GetAsync($"/api/v1/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task RetrySameRequest_ReturnsOkWithSameOrderId()
    {
        using var client = CreateClient(Guid.NewGuid(), includeCreatePermission: true);
        var request = NewRequest("api-order-retry", price: 100m);

        var firstResponse = await client.PostAsJsonAsync("/api/v1/orders", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions)
            ?? throw new InvalidOperationException("First create response did not contain an order.");

        var retryResponse = await client.PostAsJsonAsync("/api/v1/orders", request);
        var retry = await retryResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Retry response did not contain an order.");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(first.Id, retry.Id);
    }

    [Fact]
    public async Task SameClientOrderIdDifferentPayload_ReturnsConflict()
    {
        using var client = CreateClient(Guid.NewGuid(), includeCreatePermission: true);

        await client.PostAsJsonAsync(
            "/api/v1/orders",
            NewRequest("api-order-conflict", price: 100m));

        var conflict = await client.PostAsJsonAsync(
            "/api/v1/orders",
            NewRequest("api-order-conflict", price: 101m));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/orders",
            NewRequest("api-order-unauthorized", price: 100m));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithoutPermission_ReturnsForbidden()
    {
        using var client = CreateClient(Guid.NewGuid(), includeCreatePermission: false);

        var response = await client.PostAsJsonAsync(
            "/api/v1/orders",
            NewRequest("api-order-forbidden", price: 100m));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_AsAnotherUser_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        using var owner = CreateClient(ownerId, includeCreatePermission: true);

        var createResponse = await owner.PostAsJsonAsync(
            "/api/v1/orders",
            NewRequest("api-order-private", price: 100m));
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Create response did not contain an order.");

        using var anotherUser = CreateClient(Guid.NewGuid(), includeCreatePermission: false);
        var response = await anotherUser.GetAsync($"/api/v1/orders/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(Guid userId, bool includeCreatePermission)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        if (includeCreatePermission)
            client.DefaultRequestHeaders.Add("X-Permissions", "orders:create");
        return client;
    }

    private static CreateOrderRequest NewRequest(string clientOrderId, decimal price) => new()
    {
        ClientOrderId = clientOrderId,
        Symbol = "BTCUSD",
        Price = price,
        Volume = 2m
    };
}
