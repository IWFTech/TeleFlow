namespace TeleFlow.Annotations;
/// <summary>
/// Marks a handler or handler class as matching Telegram callback query updates.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CallbackAttribute : TeleFlowAttribute
{
}
/// <summary>
/// Marks a handler or handler class as matching callback queries with a typed callback data payload.
/// </summary>
/// <typeparam name="TPayload">Callback payload type decorated with <see cref="CallbackDataAttribute"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CallbackAttribute<TPayload> : TeleFlowAttribute
{
}
