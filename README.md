# Создание ордера в ASP.NET Core: разбор и решение

Небольшой технический разбор упрощенного метода создания ордера. Цель решения — не усложнить пример, а показать безопасный, читаемый и поддерживаемый подход на ASP.NET Core.

## Что не так в исходном коде

1. **Пропущен `await`.** `CheckUserAccessAsync` возвращает `Task<bool>`, а не `bool`. Непосредственное исправление — `await CheckUserAccessAsync(...)`.
2. **Асинхронный action не имеет суффикса `Async`.** По .NET naming conventions метод должен называться `CreateOrderAsync`. То же правило применено к сервисам и репозиториям.
3. **Нельзя доверять `request.UserId`.** Клиент способен подставить чужой идентификатор. Пользователь определяется по проверенным claims, а право создания ордера — policy-based authorization через `[Authorize]`.
4. **Нет входной валидации.** DataAnnotations проверяют форму запроса, а `[ApiController]` автоматически возвращает `ValidationProblemDetails`. Торговые правила остаются в application/domain layer.
5. **Не защищены доменные инварианты.** Положительные цена и объем проверяются не только DTO, но и методом `Order.Create`, поскольку домен может вызываться не через HTTP.
6. **`DateTime.Now` зависит от сервера.** Используется `TimeProvider.GetUtcNow()`: время хранится в UTC, а код легко тестировать.
7. **Нет отмены запроса.** `CancellationToken` передается по всей асинхронной цепочке.
8. **Контроллер выполняет слишком много обязанностей.** Создание вынесено в `ICreateOrderService`; контроллер отвечает только за HTTP-контракт.
9. **Наружу возвращается entity.** Отдельный `OrderResponse` не связывает публичный API со способом хранения данных.
10. **Неверный статус успеха.** Создание ресурса возвращает `201 Created` и его адрес вместо `200 OK`.
11. **Локальный `catch (Exception)` классифицирует любой сбой как `400`.** Ошибки централизованно обрабатывает встроенный `IExceptionHandler`, ответы формируются как `ProblemDetails`.
12. **`Console.WriteLine` не подходит для production.** Используется структурированный `ILogger` с `TraceId`.
13. **Возможны дубликаты при повторе запроса.** `ClientOrderId` делает операцию идемпотентной. В реальной БД необходим уникальный индекс `(UserId, ClientOrderId)` — предварительный `SELECT` сам по себе не защищает от гонки.
14. **Сохранение и уведомление могут разойтись.** Если БД сохранит ордер, а уведомление упадет, клиент повторит запрос. В production ордер и Outbox message следует записывать одной транзакцией, а уведомление отправлять фоновым обработчиком.

## Исправленный endpoint

```csharp
[ApiController]
[Route("api/v1/orders")]
[Authorize(Policy = AuthorizationPolicies.CanCreateOrders)]
public sealed class OrdersController(
    ICreateOrderService orderService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
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
```

Контроллер намеренно тонкий: авторизацию выполняет ASP.NET Core, транспортную валидацию — `[ApiController]` и DataAnnotations, бизнес-операцию — сервис, обработку ошибок — `IExceptionHandler`.

## Валидация запроса

```csharp
public sealed record CreateOrderRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9._/-]+$")]
    public required string Symbol { get; init; }

    [Range(typeof(decimal), "0.00000001", "1000000000000",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(typeof(decimal), "0.00000001", "1000000000000",
        ParseLimitsInInvariantCulture = true)]
    public decimal Volume { get; init; }

    [Required, StringLength(100, MinimumLength = 8)]
    public required string ClientOrderId { get; init; }
}
```

`decimal` выбран для цены и объема, чтобы не получать двоичные ошибки округления `double`. DataAnnotations отвечают только за корректность HTTP-запроса. Проверки существования инструмента, торговой сессии, tick size, lot size, баланса и лимитов выполняет `IOrderTradingValidator`.

## Обработка ошибок

В `Program.cs` регистрируется штатный механизм ASP.NET Core:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

app.UseExceptionHandler();
```

`GlobalExceptionHandler : IExceptionHandler` преобразует известные ошибки в единый контракт:

| Ситуация | HTTP-код |
|---|---:|
| Невалидный DTO | 400 |
| Нет аутентификации | 401 |
| Недостаточно прав | 403 |
| Дубликат `ClientOrderId` | 409 |
| Нарушено торговое правило | 422 |
| Неожиданная ошибка | 500 |

Клиент получает безопасный `ProblemDetails` с `traceId`; исключение и stack trace остаются в структурированных логах.

## Production-замечания

Демонстрационный `InMemoryOrderRepository` нужен только для компактности примера. В реальной реализации:

- `Order` и `OutboxMessage` сохраняются одним `SaveChangesAsync` в одной транзакции;
- в БД создается уникальный индекс `(UserId, ClientOrderId)`;
- provider-specific `DbUpdateException` преобразуется инфраструктурным слоем в `DuplicateOrderException`;
- фоновый Outbox processor публикует событие с retry/timeout и идемпотентным consumer;
- право торговли конкретным инструментом и актуальные лимиты повторно проверяются непосредственно перед сохранением.

Такое разделение оставляет endpoint коротким, делает зависимости явными и не смешивает HTTP, доменную логику, хранение данных и интеграции.

## Запуск

```bash
dotnet run --project src/OrderCreation.Api
```

Проект использует только встроенные возможности ASP.NET Core и не требует сторонних NuGet-пакетов.
