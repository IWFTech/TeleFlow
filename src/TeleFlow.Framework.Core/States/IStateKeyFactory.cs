using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.States;

public interface IStateKeyFactory
{
    bool TryCreateStateKey(UpdateContext context, out StateKey key);
}
