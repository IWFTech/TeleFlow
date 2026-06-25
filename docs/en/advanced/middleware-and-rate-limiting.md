# Middleware And Rate Limiting

TeleFlow update execution goes through middleware. Middleware is a good place for cross-cutting runtime behavior.

## Register Middleware

```csharp
builder.Services.AddUpdateMiddleware<MyMiddleware>();
```

Middleware implements `IUpdateMiddleware`.

Use middleware for:

- logging enrichment;
- tracing;
- tenant resolution;
- update-level guards;
- rate limiting;
- state initialization;
- exception policies.

Avoid middleware for handler-specific business logic.

## Rate Limiting

TeleFlow exposes `IUpdateRateLimiter` and default registration helpers:

```csharp
builder.Services.AddDefaultUpdateRateLimiting();
builder.Services.AddUpdateRateLimiter<MyRateLimiter>();
```

The default limiter is no-op. Custom limiters are application-specific.

## Ordering

Middleware order matters. Register middleware deliberately and keep the order documented when behavior depends on it.

For example:

1. logging or tracing;
2. tenant resolution;
3. rate limiting;
4. state;
5. dispatch.

Do not hide essential business rules in middleware ordering. If the rule is core application behavior, make it visible in services or filters.

## Enterprise Guidance

Middleware is a framework-level tool. Treat each middleware as infrastructure with tests and clear ownership.

Good middleware is boring:

- clear inputs;
- clear outputs;
- observable failures;
- no hidden handler selection;
- no unexpected Telegram side effects.
