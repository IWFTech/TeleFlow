using TeleFlow.Framework.States;
using TeleFlow.Storage.Memory;

namespace TeleFlow.ArchitectureTests.StateStorageContracts;

public sealed class MemoryStateStoreContractTests : StateStoreContractTests
{
    protected override IStateStore CreateStore()
    {
        return new MemoryStateStore();
    }
}

public sealed class MemoryStateDataStoreContractTests : StateDataStoreContractTests
{
    protected override IStateDataStore CreateStore()
    {
        return new MemoryStateDataStore();
    }
}

public sealed class MemoryStateHistoryStoreContractTests : StateHistoryStoreContractTests
{
    protected override IStateHistoryStore CreateStore()
    {
        return new MemoryStateHistoryStore();
    }
}
