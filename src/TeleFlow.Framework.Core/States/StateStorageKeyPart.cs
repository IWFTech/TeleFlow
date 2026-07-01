namespace TeleFlow.Framework.States;

/// <summary>
/// Names the logical storage record family derived from a state key so durable providers can
/// keep current state, state data, wizard history, and future locks isolated consistently.
/// </summary>
public enum StateStorageKeyPart
{
    State,
    Data,
    History,
    Lock
}
