namespace TeleFlow.Core.States;

public readonly record struct State
{
    private State(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public static State Create(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new State(id);
    }

    public override string ToString()
    {
        return Id;
    }
}
