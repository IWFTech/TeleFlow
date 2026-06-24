using TeleFlow.Core.Updates;

namespace TeleFlow.Core.States;

public interface IStateKeyFactory
{
    bool TryCreateStateKey(UpdateContext context, out StateKey key);
}
