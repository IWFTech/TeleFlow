using System.Collections.ObjectModel;

namespace TeleFlow.Core.Application;

public sealed class TeleFlowApplicationArguments
{
    public TeleFlowApplicationArguments(IEnumerable<string>? arguments)
    {
        var values = arguments?.ToArray() ?? [];
        Values = new ReadOnlyCollection<string>(values);
    }

    public IReadOnlyList<string> Values { get; }
}
