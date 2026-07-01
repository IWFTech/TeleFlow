using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramHandlerRegistrationValidator
{
    public static void EnsureAssemblyCanRegister(
        IServiceCollection services,
        Assembly assembly,
        TelegramHandlerRegistrationMode requestedMode)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var existing = services
            .Where(static descriptor => descriptor.ServiceType == typeof(TelegramHandlerAssemblyRegistrationMarker))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<TelegramHandlerAssemblyRegistrationMarker>()
            .FirstOrDefault(marker => marker.Assembly == assembly);

        if (existing is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Telegram handler assembly '{assembly.FullName}' is already registered through {FormatMode(existing.Mode)} registration and cannot be registered through {FormatMode(requestedMode)} registration.");
    }

    public static void EnsureHandlerTypesCanRegister(
        IServiceCollection services,
        IReadOnlyList<Type> handlerTypes,
        TelegramHandlerRegistrationMode requestedMode)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(handlerTypes);

        var duplicateInRequest = handlerTypes
            .GroupBy(static type => type)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateInRequest is not null)
        {
            throw new InvalidOperationException(
                $"Telegram handler type '{duplicateInRequest.Key.FullName}' is listed more than once for {FormatMode(requestedMode)} registration.");
        }

        var existingMarkers = services
            .Where(static descriptor => descriptor.ServiceType == typeof(TelegramHandlerTypeRegistrationMarker))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<TelegramHandlerTypeRegistrationMarker>()
            .ToArray();

        foreach (var handlerType in handlerTypes)
        {
            var existing = existingMarkers.FirstOrDefault(marker => marker.HandlerType == handlerType);

            if (existing is null)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Telegram handler type '{handlerType.FullName}' is already registered through {FormatMode(existing.Mode)} registration and cannot be registered through {FormatMode(requestedMode)} registration.");
        }
    }

    public static void EnsureNoDuplicateCommands(
        IServiceCollection services,
        IReadOnlyList<TelegramHandlerDescriptor> newDescriptors)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(newDescriptors);

        var existingCommandHandlers = services
            .Where(static descriptor => descriptor.ServiceType == typeof(TelegramHandlerDescriptor))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<TelegramHandlerDescriptor>()
            .Where(static descriptor => descriptor.Route.RouteKind == TelegramRouteKind.CommandExact)
            .ToArray();

        var allCommandHandlers = existingCommandHandlers
            .Concat(newDescriptors.Where(static descriptor => descriptor.Route.RouteKind == TelegramRouteKind.CommandExact))
            .SelectMany(CreateDuplicateKeys)
            .ToArray();

        var duplicate = allCommandHandlers
            .GroupBy(static item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            var display = duplicate.First().Display;

            throw new InvalidOperationException(
                $"Duplicate Telegram command handler registration for command '{display}'.");
        }
    }

    private static IEnumerable<DuplicateCommandCandidate> CreateDuplicateKeys(TelegramHandlerDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Command))
        {
            yield break;
        }

        foreach (var prefix in descriptor.Route.CommandPolicy.Prefixes)
        {
            yield return new DuplicateCommandCandidate(
                $"{prefix.ToUpperInvariant()}\u001F{descriptor.Route.CommandPolicy.AllowSpaceAfterPrefix}\u001F{descriptor.Command.ToUpperInvariant()}",
                $"{prefix}{descriptor.Command}");
        }
    }

    private static string FormatMode(TelegramHandlerRegistrationMode mode)
    {
        return mode switch
        {
            TelegramHandlerRegistrationMode.Direct => "direct",
            TelegramHandlerRegistrationMode.Generated => "generated",
            TelegramHandlerRegistrationMode.Reflection => "reflection",
            _ => mode.ToString()
        };
    }

    private sealed record DuplicateCommandCandidate(string Key, string Display);
}
