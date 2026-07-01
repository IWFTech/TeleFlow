# Custom storage

TeleFlow state storage replaceable. Memory storage полезен для local development, examples и single-process bots. Production systems часто требуют Redis, PostgreSQL, SQL Server или другой durable store.

## Контракты storage

Core contracts:

- `IStateStore`: current state name;
- `IStateDataStore`: key-value data для current state key;
- `IStateDataSerializer`: serialization для state data values;
- `IStateHistoryStore`: wizard history;
- `IStateKeyFactory`: mapping update в structured state key;
- `IStateStorageKeyBuilder`: строит stable external storage keys из structured state keys.

## Memory storage

```csharp
builder.Services.AddMemoryStateStorage();
```

Это регистрирует все state contracts и state middleware.

## Замещай только то, чем владеешь

Storage contracts заменяй осознанно:

```csharp
builder.Services.AddStateStore<RedisStateStore>();
builder.Services.AddStateDataStore<RedisStateDataStore>();
builder.Services.AddStateHistoryStore<RedisStateHistoryStore>();
```

Если заменить только `IStateStore`, но оставить memory state data, приложение получит mixed durability. Обычно это плохая идея.

## Собственная state key factory

Default Telegram state key основан на Telegram context. Enterprise systems могут требовать tenant-aware или bot-aware keys:

```csharp
builder.Services.AddStateKeyFactory<TenantStateKeyFactory>();
```

Custom keys нужны для:

- multi-tenant bots;
- нескольких bot tokens с общим storage;
- business connection partitioning;
- custom worker или gateway topologies.

`IStateKeyFactory` должна описывать ownership и isolation. В неё не надо прятать Redis, SQL или document-store formatting rules.

## Собственный storage key builder

Durable providers часто нужен string key. Для этого используется `IStateStorageKeyBuilder`:

```csharp
builder.Services.AddStateStorageKeyBuilder<MyStateStorageKeyBuilder>();
```

Default builder создаёт stable escaped keys для каждой группы state records:

```text
teleflow:state:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
teleflow:data:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
teleflow:history:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
teleflow:lock:scope=telegram:subject=user%3A5:partition=chat%3A100:destiny=default
```

Memory storage не нуждается в этой conversion и продолжает использовать `StateKey` напрямую.

## Рекомендации для реализации storage

Storage implementations должны:

- respect `CancellationToken`;
- использовать deterministic keys;
- изолировать state, data, history и lock records;
- не swallow serialization errors;
- сохранять wizard history ordering;
- быть safe under concurrent updates от одного user, если deployment это допускает;
- отдавать operational metrics в application infrastructure.

TeleFlow даёт contracts. Инфраструктура приложения решает durability, locking и consistency semantics.
