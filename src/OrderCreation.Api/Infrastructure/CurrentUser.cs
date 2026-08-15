using System.Security.Claims;

namespace OrderCreation.Api.Infrastructure;

public interface ICurrentUser
{
    Guid Id { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id => Guid.TryParse(
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id)
        ? id
        : throw new UnauthorizedAccessException(
            "Authenticated user identifier is missing or invalid.");
}
