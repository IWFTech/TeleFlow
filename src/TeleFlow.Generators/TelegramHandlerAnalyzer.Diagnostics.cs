using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    public const string MultipleRouteAttributesId = "TLF001";
    public const string UnsupportedReturnTypeId = "TLF002";
    public const string InvalidContextParameterId = "TLF003";
    public const string MultipleCancellationTokensId = "TLF004";
    public const string InvalidCommandId = "TLF005";
    public const string TextOnCallbackId = "TLF006";
    public const string DuplicateCommandId = "TLF007";
    public const string InvalidHandlerTypeId = "TLF008";
    public const string InvalidHandlerMethodId = "TLF009";
    public const string InvalidCallbackDataId = "TLF010";
    public const string DuplicateCallbackDataPrefixId = "TLF011";
    public const string InvalidStateGroupId = "TLF012";
    public const string InvalidTypedStateId = "TLF013";
    public const string InvalidTypedCallbackId = "TLF014";
    public const string InvalidTelegramModuleId = "TLF015";
    public const string InvalidRouteTemplateId = "TLF016";
    public const string InvalidRouteRegexId = "TLF017";
    public const string InvalidRouteValueId = "TLF018";
    public const string InvalidFilterId = "TLF019";
    public const string InheritedHandlerMethodId = "TLF020";
    public const string InvalidCommandPrefixId = "TLF021";
    public const string InvalidSceneId = "TLF022";
    public const string InvalidSceneStepId = "TLF023";
    public const string InvalidAutoAnswerCallbackId = "TLF024";
    public const string InvalidClassBasedHandlerId = "TLF025";
    public const string InvalidErrorHandlerId = "TLF026";
    public const string MissingRouteAttributeId = "TLF027";

    private static readonly DiagnosticDescriptor MultipleRouteAttributes = CreateDescriptor(
        MultipleRouteAttributesId,
        "Telegram handler has multiple route attributes",
        "Telegram handler method '{0}' must have exactly one route attribute",
        "Usage");

    private static readonly DiagnosticDescriptor UnsupportedReturnType = CreateDescriptor(
        UnsupportedReturnTypeId,
        "Telegram handler has unsupported return type",
        "Telegram handler method '{0}' must return Task or ValueTask",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidContextParameter = CreateDescriptor(
        InvalidContextParameterId,
        "Telegram handler has invalid context parameter",
        "{0} handler method '{1}' must declare exactly one {2} parameter",
        "Usage");

    private static readonly DiagnosticDescriptor MultipleCancellationTokens = CreateDescriptor(
        MultipleCancellationTokensId,
        "Telegram handler has multiple CancellationToken parameters",
        "Telegram handler method '{0}' can declare at most one CancellationToken parameter",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidCommand = CreateDescriptor(
        InvalidCommandId,
        "Telegram command name is invalid",
        "CommandAttribute value on '{0}' must be a command name without '/', bot username, whitespace, or empty value",
        "Usage");

    private static readonly DiagnosticDescriptor TextOnCallback = CreateDescriptor(
        TextOnCallbackId,
        "Text filter cannot be used on callback handlers",
        "TextAttribute can be used only with message or command handlers",
        "Usage");

    private static readonly DiagnosticDescriptor DuplicateCommand = CreateDescriptor(
        DuplicateCommandId,
        "Duplicate Telegram command handler",
        "Duplicate Telegram command handler registration for command '{0}'",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidHandlerType = CreateDescriptor(
        InvalidHandlerTypeId,
        "Telegram handler type is invalid",
        "Telegram handler type '{0}' must be a concrete class",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidHandlerMethod = CreateDescriptor(
        InvalidHandlerMethodId,
        "Telegram handler method is invalid",
        "Telegram handler method '{0}' must be a public instance method",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidCallbackData = CreateDescriptor(
        InvalidCallbackDataId,
        "Telegram callback data payload is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor DuplicateCallbackDataPrefix = CreateDescriptor(
        DuplicateCallbackDataPrefixId,
        "Duplicate Telegram callback data prefix",
        "Duplicate Telegram callback data prefix '{0}'",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidStateGroup = CreateDescriptor(
        InvalidStateGroupId,
        "Telegram state group is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidTypedState = CreateDescriptor(
        InvalidTypedStateId,
        "Telegram typed state reference is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidTypedCallback = CreateDescriptor(
        InvalidTypedCallbackId,
        "Telegram typed callback handler is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidTelegramModule = CreateDescriptor(
        InvalidTelegramModuleId,
        "Telegram module is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidRouteTemplate = CreateDescriptor(
        InvalidRouteTemplateId,
        "Telegram route template is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidRouteRegex = CreateDescriptor(
        InvalidRouteRegexId,
        "Telegram route regex is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidRouteValue = CreateDescriptor(
        InvalidRouteValueId,
        "Telegram route value binding is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidFilter = CreateDescriptor(
        InvalidFilterId,
        "Telegram filter usage is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InheritedHandlerMethod = CreateDescriptor(
        InheritedHandlerMethodId,
        "Telegram handler method must be declared on the concrete handler type",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidCommandPrefix = CreateDescriptor(
        InvalidCommandPrefixId,
        "Telegram command prefix is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidScene = CreateDescriptor(
        InvalidSceneId,
        "Telegram scene is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidSceneStep = CreateDescriptor(
        InvalidSceneStepId,
        "Telegram scene step is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidAutoAnswerCallback = CreateDescriptor(
        InvalidAutoAnswerCallbackId,
        "Telegram auto callback answer usage is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidClassBasedHandler = CreateDescriptor(
        InvalidClassBasedHandlerId,
        "Telegram class-based handler is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor InvalidErrorHandler = CreateDescriptor(
        InvalidErrorHandlerId,
        "Telegram error handler is invalid",
        "{0}",
        "Usage");

    private static readonly DiagnosticDescriptor MissingRouteAttribute = CreateDescriptor(
        MissingRouteAttributeId,
        "Telegram handler is missing a route attribute",
        "Telegram handler method '{0}' uses state or filter attributes but has no route attribute. Add [Message], [Command], [Callback], [ChatMemberUpdated], or another explicit Telegram route attribute.",
        "Usage");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            MultipleRouteAttributes,
            UnsupportedReturnType,
            InvalidContextParameter,
            MultipleCancellationTokens,
            InvalidCommand,
            TextOnCallback,
            DuplicateCommand,
            InvalidHandlerType,
            InvalidHandlerMethod,
            InvalidCallbackData,
            DuplicateCallbackDataPrefix,
            InvalidStateGroup,
            InvalidTypedState,
            InvalidTypedCallback,
            InvalidTelegramModule,
            InvalidRouteTemplate,
            InvalidRouteRegex,
            InvalidRouteValue,
            InvalidFilter,
            InheritedHandlerMethod,
            InvalidCommandPrefix,
            InvalidScene,
            InvalidSceneStep,
            InvalidAutoAnswerCallback,
            InvalidClassBasedHandler,
            InvalidErrorHandler,
            MissingRouteAttribute);

    private static DiagnosticDescriptor CreateDescriptor(
        string id,
        string title,
        string messageFormat,
        string category)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
