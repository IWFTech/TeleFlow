using TeleFlow.Framework.Application;

namespace TeleFlow.Telegram.I18n.Fluent.Internal;

/// <summary>
/// Loads and validates every configured Fluent resource before the first Telegram update can reach the application pipeline.
/// </summary>
internal sealed class FluentCatalogRuntimeValidator(FluentCatalog catalog) : ITeleFlowRuntimeValidator
{
    public void Validate(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        catalog.Initialize();
    }
}
