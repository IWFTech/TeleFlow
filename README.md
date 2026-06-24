# TeleFlow

TeleFlow is a modern Telegram bot framework for .NET.

Use `TeleFlow.Telegram` for direct Telegram Bot API access through the low-level client. Add narrower runtime packages only when the application intentionally needs transports or the handler framework:

- `TeleFlow.Telegram.Client` for the same direct Telegram Bot API client through the explicit owner package.
- `TeleFlow.Telegram.LongPolling` for raw `getUpdates` consumption.
- `TeleFlow.Telegram.Webhooks` for raw ASP.NET Core webhook endpoints.
- `TeleFlow.Telegram.Framework.LongPolling` or `TeleFlow.Telegram.Framework.Webhooks` for handler-based applications with an explicit transport package.
- `TeleFlow.Storage.Memory` for in-memory state storage.

Most application code uses `using TeleFlow.Telegram;` even when package boundaries are split. Package and assembly names describe ownership and dependency scope; public namespaces stay concise for user code.

Detailed package selection guidance lives in the repository documentation under `docs/getting-started/packages.md`.
