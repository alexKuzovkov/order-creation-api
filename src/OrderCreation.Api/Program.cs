using OrderCreation.Api.Application;
using OrderCreation.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.CanCreateOrders,
        policy => policy.RequireAuthenticatedUser()
            .RequireClaim("permission", "orders:create"));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ICreateOrderService, CreateOrderService>();
builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddScoped<IOrderTradingValidator, OrderTradingValidator>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
