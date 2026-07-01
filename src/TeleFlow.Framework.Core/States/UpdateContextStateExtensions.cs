using System.Diagnostics.CodeAnalysis;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.States;

public static class UpdateContextStateExtensions
{
    public static UpdateState GetState(this UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TryGetState(out var state))
        {
            return state;
        }

        throw new InvalidOperationException(
            "State is not available for the current update. Register state storage and state middleware before using ctx.State.");
    }

    public static bool TryGetState(
        this UpdateContext context,
        [NotNullWhen(true)] out UpdateState? state)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(UpdateStateContextKeys.State, out var value) &&
            value is UpdateState updateState)
        {
            state = updateState;
            return true;
        }

        state = null;
        return false;
    }
}
