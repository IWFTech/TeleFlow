namespace TeleFlow.Framework.States;

/// <summary>
/// Converts structured state keys into deterministic storage keys for providers that address
/// state records with strings, such as Redis, SQL rows, or document-store partition keys.
/// </summary>
public interface IStateStorageKeyBuilder
{
    string Build(StateKey key, StateStorageKeyPart part);
}
