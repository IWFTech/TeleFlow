namespace TeleFlow.Annotations;

/// <summary>
/// Defines whether a command route requires, accepts, or ignores a command prefix before matching the command body.
/// </summary>
public enum CommandPrefixMode
{
    /// <summary>
    /// The incoming message must start with one of the configured command prefixes.
    /// </summary>
    Required = 0,

    /// <summary>
    /// The incoming message may start with one of the configured command prefixes, but prefix-less text is accepted too.
    /// </summary>
    Optional = 1,

    /// <summary>
    /// The incoming message is matched as command body text and configured command prefixes are not accepted.
    /// </summary>
    NoPrefix = 2
}
