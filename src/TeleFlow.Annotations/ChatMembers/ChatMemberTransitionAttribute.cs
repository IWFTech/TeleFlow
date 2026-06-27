namespace TeleFlow.Annotations;
/// <summary>
/// Routes a chat member update when it matches a predefined member transition.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ChatMemberTransitionAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a chat member transition route.
    /// </summary>
    /// <param name="transition">Predefined transition that must match the member status change.</param>
    public ChatMemberTransitionAttribute(TelegramMemberTransition transition)
    {
        Transition = transition;
    }

    /// <summary>
    /// Predefined transition that must match the member status change.
    /// </summary>
    public TelegramMemberTransition Transition { get; }
}
