namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Stores route filters in the order the dispatcher actually evaluates them so handler selection does not
/// split built-in filters, custom filters, and role requirements on every incoming Telegram update.
/// </summary>
internal sealed class TelegramRouteFilterPlan
{
    private static readonly IReadOnlyList<TelegramFilterDescriptor> EmptyBuiltInFilters = [];
    private static readonly IReadOnlyList<Type> EmptyCustomFilterTypes = [];
    private static readonly IReadOnlyList<TelegramRoleRequirementDescriptor> EmptyRoleRequirements = [];
    private static readonly TelegramRouteFilterPlan Empty = new(
        EmptyBuiltInFilters,
        EmptyCustomFilterTypes,
        EmptyRoleRequirements);

    private TelegramRouteFilterPlan(
        IReadOnlyList<TelegramFilterDescriptor> builtInFilters,
        IReadOnlyList<Type> customFilterTypes,
        IReadOnlyList<TelegramRoleRequirementDescriptor> roleRequirements)
    {
        BuiltInFilters = builtInFilters;
        CustomFilterTypes = customFilterTypes;
        RoleRequirements = roleRequirements;
    }

    public IReadOnlyList<TelegramFilterDescriptor> BuiltInFilters { get; }

    public IReadOnlyList<Type> CustomFilterTypes { get; }

    public IReadOnlyList<TelegramRoleRequirementDescriptor> RoleRequirements { get; }

    public static TelegramRouteFilterPlan Create(TelegramRouteDescriptor route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return Create(route.Filters, route.RoleRequirements);
    }

    private static TelegramRouteFilterPlan Create(
        IReadOnlyList<TelegramFilterDescriptor> filters,
        IReadOnlyList<TelegramRoleRequirementDescriptor> roleRequirements)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(roleRequirements);

        if (filters.Count == 0 && roleRequirements.Count == 0)
        {
            return Empty;
        }

        List<TelegramFilterDescriptor>? builtInFilters = null;
        List<Type>? customFilterTypes = null;

        for (var index = 0; index < filters.Count; index++)
        {
            var filter = filters[index];

            if (filter.CustomFilterType is { } customFilterType)
            {
                customFilterTypes ??= [];
                customFilterTypes.Add(customFilterType);
                continue;
            }

            builtInFilters ??= [];
            builtInFilters.Add(filter);
        }

        return new TelegramRouteFilterPlan(
            builtInFilters?.ToArray() ?? EmptyBuiltInFilters,
            customFilterTypes?.ToArray() ?? EmptyCustomFilterTypes,
            roleRequirements);
    }
}
