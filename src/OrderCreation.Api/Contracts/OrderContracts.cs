using System.ComponentModel.DataAnnotations;

namespace OrderCreation.Api.Contracts;

public sealed record CreateOrderRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9._/-]+$", ErrorMessage = "Symbol has an invalid format.")]
    public required string Symbol { get; init; }

    [Range(typeof(decimal), "0.00000001", "1000000000000",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(typeof(decimal), "0.00000001", "1000000000000",
        ParseLimitsInInvariantCulture = true)]
    public decimal Volume { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string ClientOrderId { get; init; }
}

public sealed record OrderResponse(
    Guid Id,
    string ClientOrderId,
    string Symbol,
    decimal Price,
    decimal Volume,
    string Status,
    DateTimeOffset CreatedAt);
