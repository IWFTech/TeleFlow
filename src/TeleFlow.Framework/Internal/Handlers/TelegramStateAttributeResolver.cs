using System.Reflection;
using TeleFlow.Annotations;
using TeleFlow.Framework.States;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramStateAttributeResolver
{
    public static IReadOnlyList<string> GetStates(MemberInfo handlerType, MethodInfo method)
    {
        return GetStateAttributes(handlerType)
            .Concat(GetStateAttributes(method))
            .Select(ResolveState)
            .ToArray();
    }

    private static IEnumerable<Attribute> GetStateAttributes(MemberInfo member)
    {
        return member
            .GetCustomAttributes(inherit: true)
            .OfType<Attribute>()
            .Where(static attribute =>
                attribute is StateAttribute ||
                IsGenericStateAttribute(attribute.GetType()));
    }

    private static string ResolveState(Attribute attribute)
    {
        if (attribute is StateAttribute stateAttribute)
        {
            return stateAttribute.State;
        }

        var attributeType = attribute.GetType();
        if (!IsGenericStateAttribute(attributeType))
        {
            throw new InvalidOperationException(
                $"Unsupported Telegram state attribute '{attributeType.FullName}'.");
        }

        var stateName = attributeType
            .GetProperty(nameof(StateAttribute<object>.StateName))!
            .GetValue(attribute) as string;

        if (string.IsNullOrWhiteSpace(stateName))
        {
            throw new InvalidOperationException(
                $"Typed Telegram state attribute '{attributeType.FullName}' must declare a state member name.");
        }

        var groupType = attributeType.GetGenericArguments()[0];
        var property = groupType.GetProperty(
            stateName,
            BindingFlags.Public | BindingFlags.Static);

        if (property is null || property.PropertyType != typeof(State))
        {
            throw new InvalidOperationException(
                $"Typed Telegram state attribute references missing state '{groupType.FullName}.{stateName}'.");
        }

        return ((State)property.GetValue(null)!).Id;
    }

    private static bool IsGenericStateAttribute(Type attributeType)
    {
        return attributeType.IsGenericType &&
               attributeType.GetGenericTypeDefinition() == typeof(StateAttribute<>);
    }
}
