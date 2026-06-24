namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CommandAttribute : TeleFlowAttribute
{
    public CommandAttribute(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        Command = command;
    }

    public string Command { get; }

    public string[] Prefixes { get; set; } = ["/"];

    public bool AllowSpaceAfterPrefix { get; set; }

    public bool IgnoreCase { get; set; } = true;
}
