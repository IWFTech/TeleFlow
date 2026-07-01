namespace TeleFlow.Framework.Updates;

/// <summary>
/// Initializes the scoped current-update accessor from the runtime pipeline before middleware and
/// handler dispatch execute for an incoming update.
/// </summary>
internal interface IUpdateContextAccessorInitializer
{
    void Initialize(UpdateContext context);

    void Clear(UpdateContext context);
}
