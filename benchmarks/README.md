# TeleFlow Benchmarks

This directory contains reproducible, no-network benchmarks for TeleFlow runtime paths and comparable Telegram bot library surfaces.

The suite is intentionally separate from `TeleFlow.sln`. Benchmark dependencies must not leak into normal library builds, package graphs, or consumer projects.

## Scope

Current benchmark suites:

- TeleFlow vs Telegram.Bot update JSON deserialization.
- TeleFlow vs Telegram.Bot native `sendMessage` client call through fixed no-network transports.
- TeleFlow vs Telegram.Bot native `getUpdates` client call through fixed no-network transports.
- TeleFlow raw long polling vs a handwritten Telegram.Bot raw polling loop over the same fixed update batch.
- TeleFlow handler dispatch for command, callback prefix, and state routes.
- TeleFlow generated client method execution through a fixed in-memory `ITelegramTransport`.
- TeleFlow raw long polling diagnostics split into response deserialization, native `getUpdates`, streaming polling, and `RunAsync` polling.
- TeleFlow vs Telegrator command handler dispatch through public framework routing APIs.
- TeleFlow vs Telegrator callback handler dispatch through public framework routing APIs.

Planned benchmark suites:

- Generated handler registration and dispatch parity.
- Middleware pipeline cost with one, three, and ten middleware components.
- Callback payload serialization and deserialization.
- State data and wizard navigation scenarios.
- Telegrator state-like routing benchmarks after an equivalent public API path is pinned down.

## Fairness Rules

Benchmarks in this directory must follow these rules:

- Do not call Telegram over the network.
- Do not include real database, Redis, broker, DNS, TLS, or HTTP server latency in framework comparisons.
- Use the same update JSON fixtures for all libraries that can consume Telegram updates.
- Keep setup costs in `GlobalSetup` unless the scenario explicitly measures startup or registration.
- Measure one scenario per benchmark method.
- Prefer public APIs. Do not benchmark TeleFlow internals unless the benchmark name clearly says it is an internal diagnostic benchmark.
- Document every competitor adapter and the exact package version used.
- If a competitor cannot be benchmarked fairly yet, leave an explicit gap instead of adding a fake comparison.

## Running

Build the benchmark project:

```bash
dotnet build benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -c Release
```

Run all benchmarks:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*"
```

Run a smoke check:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*TeleFlowJsonBenchmarks*" --job Dry
```

Run a category:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*DispatchBenchmarks*"
```

Run the fair cross-library comparison set:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*Vs*"
```

Run a fast smoke check for the cross-library comparison set:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*Vs*" --job Dry
```

Run raw polling diagnostics:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*RawPollingDiagnosticsBenchmarks*"
```

Run a fast smoke check for raw polling diagnostics:

```bash
dotnet run -c Release --project benchmarks/TeleFlow.Benchmarks/TeleFlow.Benchmarks.csproj -- --filter "*RawPollingDiagnosticsBenchmarks*" --job Dry
```

When the command is run from the repository root, BenchmarkDotNet writes reports under:

```text
BenchmarkDotNet.Artifacts/
```

Do not commit generated benchmark reports unless a release note or article intentionally references a specific captured run.

## Competitor Status

| Library | Package | Status | Notes |
| --- | --- | --- | --- |
| TeleFlow | project references | Active | Measured through public APIs and local fixtures. |
| Telegram.Bot | `Telegram.Bot` `22.10.1` | Active baseline | Used for low-level native client, raw polling loop, and update model deserialization comparisons. |
| Telegrator | `Telegrator` `18.7.2` | Active partial | Command and callback dispatch are measured through public routing APIs. Setup verifies that benchmark handlers actually execute. State-like scenarios are not covered yet. |

## Cross-Library Matrix

Use `Scenarios/Vs` when publishing or discussing comparisons. These benchmarks compare the same layer to the same layer:

| Benchmark | TeleFlow path | Compared path | What it answers |
| --- | --- | --- | --- |
| `ClientSendMessageVsBenchmarks` | Native `ITelegramClient.SendMessageAsync` | Telegram.Bot `SendMessage` | Cost of a generated API method call without network I/O. |
| `ClientGetUpdatesVsBenchmarks` | Native `ITelegramClient.GetUpdatesAsync` | Telegram.Bot `GetUpdates` | Cost of a generated `getUpdates` call without network I/O. |
| `RawPollingBatchVsBenchmarks` | `ITelegramLongPollingClient.RunAsync` | Handwritten Telegram.Bot polling loop | Cost of raw polling control flow over the same update batch. |
| `FrameworkCommandDispatchVsBenchmarks` | TeleFlow dispatcher command route | Telegrator `UpdateRouter` command route | Framework command routing overhead. |
| `FrameworkCallbackDispatchVsBenchmarks` | TeleFlow dispatcher callback route | Telegrator callback route | Framework callback routing overhead. |
| `JsonDeserializeVsBenchmarks` | TeleFlow schema model deserialization | Telegram.Bot model deserialization | Update JSON parsing and model materialization cost. |

## TeleFlow Diagnostics

Use `Scenarios/TeleFlow/RawPollingDiagnosticsBenchmarks.cs` when raw polling needs investigation before changing runtime code. It intentionally stays on public TeleFlow APIs and measures these layers:

| Benchmark | Layer | What it answers |
| --- | --- | --- |
| `DeserializeGetUpdatesResponse` | `TelegramApiResponse<IReadOnlyList<Update>>` deserialization | Lower-bound parsing cost for the same `getUpdates` response body. |
| `NativeClientGetUpdatesBatch` | `ITelegramClient.GetUpdatesAsync` over fixed transport | Generated method, request executor, envelope parsing, and update model materialization cost. |
| `StreamingLongPollingBatch` | `ITelegramLongPollingClient.GetUpdatesAsync` with explicit acknowledgement | Streaming raw polling overhead on top of native `getUpdates`. |
| `RunAsyncLongPollingBatch` | `ITelegramLongPollingClient.RunAsync` with a no-op handler | Full raw polling callback loop overhead without framework dispatch. |

## Interpreting Results

These benchmarks are meant to answer narrow performance questions:

- How much does each library allocate to deserialize the same Telegram update fixture?
- How much overhead does framework dispatch add for common routes?
- How much does the generated/native client method path allocate before network I/O?
- How much overhead does raw polling control flow add after the Telegram API response is already available?

They are not a replacement for application load tests. A real bot also needs workload-specific tests for database access, cache behavior, external API calls, rate limits, backpressure, and deployment topology.

Do not present one table as proof that the whole framework is faster. Raw client, JSON, raw polling, and framework dispatch are separate layers. Public claims should name the measured layer and the exact competitor version.
