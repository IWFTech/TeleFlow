using System.Collections.Immutable;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerSourceGenerator
{
    private enum GeneratedHandlerKind
    {
        Command,
        Message,
        Callback,
        ChatMember
    }

    private enum GeneratedParameterKind
    {
        Context,
        CallbackPayload,
        RouteValue,
        Service,
        CancellationToken
    }

    private enum GeneratedErrorParameterKind
    {
        ErrorContext,
        TelegramContext,
        Exception,
        RouteValue,
        Service,
        CancellationToken
    }

    private enum GeneratedErrorReturnKind
    {
        Sync,
        Task,
        ValueTask
    }

    private enum GeneratedRouteKind
    {
        MessageAny,
        TextExact,
        CommandExact,
        TextTemplate,
        CommandTemplate,
        TextRegex,
        CommandRegex,
        Callback,
        ChatMemberUpdated,
        MyChatMemberUpdated
    }

    private sealed record GeneratedHandlerMethod(
        string SignatureKey,
        string HandlerTypeName,
        string HandlerTypeMetadataName,
        string MethodName,
        string? ModuleName,
        string? SceneName,
        string? CallbackPayloadType,
        GeneratedAutoAnswerCallback? AutoAnswerCallback,
        ImmutableArray<GeneratedRoute> Routes,
        ImmutableArray<string> States,
        ImmutableArray<GeneratedParameter> Parameters,
        string SourcePath,
        int SourceSpanStart);

    private sealed record GeneratedErrorHandlerMethod(
        string SignatureKey,
        string HandlerTypeName,
        string HandlerTypeMetadataName,
        string MethodName,
        string? ModuleName,
        ImmutableArray<string?> ExceptionTypes,
        string? TelegramContextType,
        GeneratedErrorReturnKind ReturnKind,
        ImmutableArray<GeneratedErrorParameter> Parameters,
        string SourcePath,
        int SourceSpanStart);

    private sealed record GeneratedRoute(
        GeneratedHandlerKind Kind,
        GeneratedRouteKind RouteKind,
        string? Command,
        string? Pattern,
        ImmutableArray<string> CommandPrefixes,
        bool AllowSpaceAfterPrefix,
        bool IgnoreCase,
        ImmutableArray<GeneratedTextFilter> TextFilters,
        ImmutableArray<GeneratedFilter> Filters,
        ImmutableArray<GeneratedChatMemberTransition> ChatMemberTransitions,
        ImmutableArray<int> RoleRequirements,
        ImmutableDictionary<string, GeneratedRouteValue> RouteValues);

    private readonly record struct GeneratedRouteValue(
        string? Constraint,
        bool IsOptional);

    private readonly record struct GeneratedChatMemberTransition(int OldStatus, int NewStatus);

    private sealed record GeneratedAutoAnswerCallback(
        bool Enabled,
        string? Text,
        bool ShowAlert);

    private sealed record GeneratedTextFilter(string Value, int Mode, bool IgnoreCase);

    private sealed record GeneratedFilter(
        string Kind,
        ImmutableArray<string> StringValues,
        ImmutableArray<long> LongValues,
        string? CustomTypeName = null);

    private sealed record GeneratedParameter(string TypeName, GeneratedParameterKind Kind, string Name);

    private sealed record GeneratedErrorParameter(string TypeName, GeneratedErrorParameterKind Kind, string Name);

    private sealed record GeneratedStateGroup(
        string TypeName,
        string TypeMetadataName,
        string? Namespace,
        string Accessibility,
        bool IsStatic,
        string Prefix,
        ImmutableArray<GeneratedStateProperty> States,
        int SourceSpanStart);

    private sealed record GeneratedStateProperty(string Name, string Segment);
}
