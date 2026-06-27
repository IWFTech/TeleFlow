namespace TeleFlow.Annotations;
/// <summary>
/// Routes a Telegram message to a handler when it contains the specified command.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CommandAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a command route.
    /// </summary>
    /// <param name="command">Command name without the command prefix.</param>
    public CommandAttribute(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        Command = command;
    }

    /// <summary>
    /// Command name without the command prefix.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Command prefixes accepted by this route. Defaults to <c>/</c>.
    /// </summary>
    public string[] Prefixes { get; set; } = ["/"];

    /// <summary>
    /// Allows whitespace between the prefix and command name.
    /// </summary>
    public bool AllowSpaceAfterPrefix { get; set; }

    /// <summary>
    /// Matches the command name without case sensitivity.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}
