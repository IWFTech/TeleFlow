# Custom Storage

TeleFlow state storage is replaceable. Memory storage is useful for local development, examples, and single-process bots. Production systems often need Redis, PostgreSQL, SQL Server, or another durable store.

## Storage Contracts

Core contracts:

- `IStateStore`: current state name;
- `IStateDataStore`: key-value data for the current state key;
- `IStateDataSerializer`: serialization for state data values;
- `IStateHistoryStore`: wizard history;
- `IStateKeyFactory`: maps an update to a structured state key;
- `IStateStorageKeyBuilder`: builds stable external storage keys from structured state keys.

## Memory Storage

```csharp
builder.Services.AddMemoryStateStorage();
```

This registers all state contracts and state middleware.

## Replace Only What You Own

Replace storage contracts intentionally:

```csharp
builder.Services.AddStateStore<RedisStateStore>();
builder.Services.AddStateDataStore<RedisStateDataStore>();
builder.Services.AddStateHistoryStore<RedisStateHistoryStore>();
```

If you replace only `IStateStore` but keep memory state data, your app will have mixed durability. That is rarely what you want.

## Custom State Key Factory

The default Telegram state key is based on Telegram context. For enterprise systems you may need tenant-aware or bot-aware keys:

```csharp
builder.Services.AddStateKeyFactory<TenantStateKeyFactory>();
```

Use custom keys for:

- multi-tenant bots;
- several bot tokens sharing storage;
- business connection partitioning;
- custom worker or gateway topologies.

`IStateKeyFactory` should describe ownership and isolation. It should not contain Redis, SQL, or document-store formatting rules.

## Custom Storage Key Builder

Durable providers often need a string key. Use `IStateStorageKeyBuilder` for that conversion:

```csharp
builder.Services.AddStateStorageKeyBuilder<MyStateStorageKeyBuilder>();
```

The default builder produces stable escaped keys for each state record family:

```text
teleflow:state:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
teleflow:data:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
teleflow:history:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
teleflow:lock:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
```

Memory storage does not need this conversion and continues to use `StateKey` directly.

## Storage Implementation Guidance

Storage implementations should:

- respect `CancellationToken`;
- use deterministic keys;
- keep state, data, history, and lock records isolated;
- avoid swallowing serialization errors;
- preserve wizard history ordering;
- be safe under concurrent updates from the same user if your deployment allows it;
- expose operational metrics in the application infrastructure.

TeleFlow provides the contracts. Your infrastructure decides durability, locking, and consistency semantics.
