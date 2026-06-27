namespace TeleFlow.Annotations;

/// <summary>
/// Determines how a text route compares incoming text with the configured value.
/// </summary>
public enum TextMatchMode
{
    /// <summary>
    /// Incoming text must equal the configured value.
    /// </summary>
    Equals,

    /// <summary>
    /// Incoming text must start with the configured value.
    /// </summary>
    StartsWith,

    /// <summary>
    /// Incoming text must contain the configured value.
    /// </summary>
    Contains
}
