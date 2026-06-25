# Middleware и rate limiting

TeleFlow update execution проходит через middleware. Middleware подходит для cross-cutting runtime behavior.

## Регистрация middleware

```csharp
builder.Services.AddUpdateMiddleware<MyMiddleware>();
```

Middleware implements `IUpdateMiddleware`.

Используй middleware для:

- logging enrichment;
- tracing;
- tenant resolution;
- update-level guards;
- rate limiting;
- state initialization;
- exception policies.

Не используй middleware для handler-specific business logic.

## Rate limiting

TeleFlow exposes `IUpdateRateLimiter` и default registration helpers:

```csharp
builder.Services.AddDefaultUpdateRateLimiting();
builder.Services.AddUpdateRateLimiter<MyRateLimiter>();
```

Default limiter - no-op. Custom limiters application-specific.

## Порядок выполнения

Middleware order matters. Регистрируй middleware осознанно и документируй order, если behavior от него зависит.

Пример:

1. logging or tracing;
2. tenant resolution;
3. rate limiting;
4. state;
5. dispatch.

Не прячь essential business rules в middleware ordering. Если rule - core application behavior, делай его видимым в services или filters.

## Enterprise-рекомендации

Middleware - framework-level tool. Относись к каждому middleware как к infrastructure с tests и clear ownership.

Хороший middleware скучный:

- clear inputs;
- clear outputs;
- observable failures;
- no hidden handler selection;
- no unexpected Telegram side effects.
