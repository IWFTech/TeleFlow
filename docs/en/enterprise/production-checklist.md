# Production Checklist

Use this checklist before running a TeleFlow bot in production.

Related pages:

- [Configuration and secrets](../getting-started/configuration.md)
- [Deployment](deployment.md)
- [Custom storage](../advanced/custom-storage.md)

## Packages

- Use the narrow package set required by the app.
- Keep `TeleFlow.Generators` private with `PrivateAssets="all"`.
- Do not install raw transports and framework transports unless both are intentionally used.

## Registration

- Use generated assembly registration by default.
- Keep at least one startup test that exercises generated registration.
- Use direct registration for narrow tests.
- Avoid reflection registration unless documented.

## Transport

- Choose long polling or webhooks intentionally.
- Do not register more than one `IUpdateSource`.
- For webhooks, use HTTPS and secret token.
- For long polling, run under a host with restart policy.
- Document how pending updates should be handled after downtime.

## Storage

- Do not use memory state storage for multi-instance production.
- Define state key structure.
- Define TTL and cleanup.
- Test wizard back/reset behavior.
- Test process restart behavior.

## Handlers

- Keep handlers thin.
- Move business logic into application services.
- Pass `CancellationToken` to I/O.
- Keep template and regex routes readable.
- Avoid overly broad catch-all handlers.

## Callbacks

- Keep callback payloads compact.
- Do not put sensitive data in callback data.
- Answer callbacks manually or configure auto-answer intentionally.
- Test callback payload serialization.

## Errors

- Handle known business exceptions.
- Let unknown exceptions stay visible.
- Log exception type and handler.
- Test error handlers that affect users.

## Observability

- Enable structured logs.
- Track handler duration.
- Track Telegram request count and latency.
- Track storage latency.
- Alert on repeated handler failures.

## Security

- Store bot token outside source code.
- Rotate bot token intentionally.
- Use webhook secret token.
- Validate admin/user ids from configuration.
- Keep production logs free from secrets.
- Do not put credentials or privileged actions into callback data.

## CI

- Build all projects.
- Run tests.
- Run package smoke tests.
- Keep analyzer diagnostics visible.
- Check documentation links.
