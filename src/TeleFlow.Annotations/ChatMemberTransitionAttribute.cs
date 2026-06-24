namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ChatMemberTransitionAttribute : TeleFlowAttribute
{
    public ChatMemberTransitionAttribute(TelegramMemberTransition transition)
    {
        Transition = transition;
    }

    public TelegramMemberTransition Transition { get; }
}
