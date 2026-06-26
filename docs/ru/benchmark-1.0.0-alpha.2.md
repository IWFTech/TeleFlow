# TeleFlow Reader-Probe Benchmark Report

Captured: 2026-06-26

## Scope

This is a local benchmark artifact for the reader-probe union converter change.

The report compares:

- baseline commit: `3a3860d` (`Update generated Telegram metadata manifest (#13)`)
- optimized commit: `ffa0fe0` (`chore(schema): regenerate union converters with reader probes`)

Benchmark command:

```powershell
dotnet run -c Release --project benchmarks\TeleFlow.Benchmarks\TeleFlow.Benchmarks.csproj -- --filter "*Vs*" --job medium
```

JSON deserialization was rerun separately for both baseline and optimized commits after the full run because the first full-run sequence showed visible machine drift. The JSON-specific old/new section below uses those rerun values.

Environment:

- BenchmarkDotNet: `0.15.8`
- OS: Windows 10 `10.0.19045.6456`
- CPU: Intel Xeon E5-2670 v3, 12 physical / 24 logical cores
- .NET SDK: `10.0.102`
- Runtime: `.NET 10.0.2`, X64 RyuJIT
- Job: `MediumRun`, `IterationCount=15`, `LaunchCount=2`, `WarmupCount=10`

## Current Cross-Library Results

These are the optimized commit results from `ffa0fe0`.

| Scenario | TeleFlow | Competitor | Result |
| --- | ---: | ---: | --- |
| Native `getUpdates` client | `9.017 us`, `4.96 KB` | Telegram.Bot: `9.027 us`, `7.15 KB` | TeleFlow is time-equivalent and allocates less. |
| Native `sendMessage` client | `6.857 us`, `3.60 KB` | Telegram.Bot: `6.880 us`, `5.07 KB` | TeleFlow is time-equivalent and allocates less. |
| Framework command dispatch | `1.015 us`, `1.13 KB` | Telegrator: `5.502 us`, `4.04 KB` | TeleFlow is about `5.4x` faster with lower allocations. |
| Framework callback dispatch | `0.812 us`, `1.06 KB` | Telegrator: `25.997 us`, `4.48 KB` | TeleFlow is about `32x` faster with lower allocations. |
| Raw polling batch | `28.40 us`, `12.05 KB` | Telegram.Bot: `20.13 us`, `12.34 KB` | Telegram.Bot is faster; TeleFlow allocates slightly less. |

## Current JSON Results

These are the optimized commit JSON rerun results.

| Fixture | TeleFlow | Telegram.Bot | Result |
| --- | ---: | ---: | --- |
| `MessageCommandStart` | `4.172 us`, `2.45 KB` | `3.879 us`, `2.09 KB` | TeleFlow is about `7.6%` slower. |
| `MessageStateText` | `2.585 us`, `2.17 KB` | `2.375 us`, `1.85 KB` | TeleFlow is about `8.8%` slower. |
| `CallbackTicketTake` | `6.234 us`, `3.19 KB` | `4.059 us`, `2.23 KB` | TeleFlow is about `53.6%` slower. |

## Reader-Probe Old/New Impact

This section compares TeleFlow baseline vs optimized commit. Positive speed means the optimized commit is faster.

| Scenario | Old TeleFlow | New TeleFlow | Speed change | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| JSON `MessageCommandStart` | `3.912 us`, `2.45 KB` | `4.172 us`, `2.45 KB` | `-6.6%` | `0.0%` |
| JSON `MessageStateText` | `2.874 us`, `2.17 KB` | `2.585 us`, `2.17 KB` | `+10.1%` | `0.0%` |
| JSON `CallbackTicketTake` | `8.051 us`, `3.26 KB` | `6.234 us`, `3.19 KB` | `+22.6%` | `+2.1%` |
| Native `sendMessage` client | `7.199 us`, `3.60 KB` | `6.857 us`, `3.60 KB` | `+4.8%` | `0.0%` |
| Native `getUpdates` client | `8.954 us`, `4.96 KB` | `9.017 us`, `4.96 KB` | `-0.7%` | `0.0%` |
| Framework callback dispatch | `0.810 us`, `1.06 KB` | `0.812 us`, `1.06 KB` | `-0.3%` | `0.0%` |
| Framework command dispatch | `0.905 us`, `1.13 KB` | `1.015 us`, `1.13 KB` | `-12.2%` | `0.0%` |
| Raw polling batch | `24.32 us`, `12.12 KB` | `28.40 us`, `12.05 KB` | `-16.8%` | `+0.6%` |

## Interpretation

The reader-probe change is strongest where it was expected to help: union-heavy callback deserialization.

The most important result:

- `CallbackTicketTake` improved from `8.051 us` to `6.234 us`.
- Allocation went from `3.26 KB` to `3.19 KB`.
- The generated converter no longer uses `JsonDocument.ParseValue` or `JsonElement.Deserialize` for union object cases.

The full old/new run should not be used as a regression claim for unrelated layers because the sequential benchmark run showed machine drift: competitor rows changed too, even though competitor code did not change. For publication, use the current cross-library tables and state the exact layer being measured.

## Follow-Up

Recommended next performance work:

1. Split raw polling into smaller diagnostic benchmarks:
   - only `getUpdates` request executor;
   - response envelope parsing;
   - polling loop over fixed batch;
   - polling loop plus no-op handler.
2. Keep framework dispatch unchanged for now. It is already clearly ahead of Telegrator.
3. Do not optimize JSON further until a diagnostic benchmark identifies the next concrete allocation or branch cost.
