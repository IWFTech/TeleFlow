using Microsoft.CodeAnalysis;

namespace TeleFlow.Generators;

internal static class TelegramHandlerSymbols
{
    public const string CommandAttribute = "TeleFlow.Annotations.CommandAttribute";
    public const string MessageAttribute = "TeleFlow.Annotations.MessageAttribute";
    public const string CallbackAttribute = "TeleFlow.Annotations.CallbackAttribute";
    public const string GenericCallbackAttribute = "TeleFlow.Annotations.CallbackAttribute<TPayload>";
    public const string ErrorAttribute = "TeleFlow.Annotations.ErrorAttribute";
    public const string GenericErrorAttribute = "TeleFlow.Annotations.ErrorAttribute<TException>";
    public const string CallbackDataAttribute = "TeleFlow.Annotations.CallbackDataAttribute";
    public const string TelegramModuleAttribute = "TeleFlow.Annotations.TelegramModuleAttribute";
    public const string StateAttribute = "TeleFlow.Annotations.StateAttribute";
    public const string GenericStateAttribute = "TeleFlow.Annotations.StateAttribute<TStateGroup>";
    public const string StateGroupAttribute = "TeleFlow.Annotations.StateGroupAttribute";
    public const string StateValueAttribute = "TeleFlow.Annotations.StateValueAttribute";
    public const string SceneAttribute = "TeleFlow.Annotations.SceneAttribute";
    public const string SceneStepAttribute = "TeleFlow.Annotations.SceneStepAttribute";
    public const string TextAttribute = "TeleFlow.Annotations.TextAttribute";
    public const string TextTemplateAttribute = "TeleFlow.Annotations.TextTemplateAttribute";
    public const string CommandTemplateAttribute = "TeleFlow.Annotations.CommandTemplateAttribute";
    public const string TextRegexAttribute = "TeleFlow.Annotations.TextRegexAttribute";
    public const string CommandRegexAttribute = "TeleFlow.Annotations.CommandRegexAttribute";
    public const string ChatMemberUpdatedAttribute = "TeleFlow.Annotations.ChatMemberUpdatedAttribute";
    public const string MyChatMemberUpdatedAttribute = "TeleFlow.Annotations.MyChatMemberUpdatedAttribute";
    public const string ChatMemberTransitionAttribute = "TeleFlow.Annotations.ChatMemberTransitionAttribute";
    public const string ChatMemberChangedAttribute = "TeleFlow.Annotations.ChatMemberChangedAttribute";
    public const string AutoAnswerCallbackAttribute = "TeleFlow.Annotations.AutoAnswerCallbackAttribute";
    public const string RequireTelegramRoleAttribute = "TeleFlow.Annotations.RequireTelegramRoleAttribute";
    public const string ChatTypeAttribute = "TeleFlow.Annotations.ChatTypeAttribute";
    public const string ChatIdAttribute = "TeleFlow.Annotations.ChatIdAttribute";
    public const string ChatUsernameAttribute = "TeleFlow.Annotations.ChatUsernameAttribute";
    public const string FromUserAttribute = "TeleFlow.Annotations.FromUserAttribute";
    public const string HasTextAttribute = "TeleFlow.Annotations.HasTextAttribute";
    public const string HasPhotoAttribute = "TeleFlow.Annotations.HasPhotoAttribute";
    public const string HasDocumentAttribute = "TeleFlow.Annotations.HasDocumentAttribute";
    public const string HasCaptionAttribute = "TeleFlow.Annotations.HasCaptionAttribute";
    public const string HasVideoAttribute = "TeleFlow.Annotations.HasVideoAttribute";
    public const string HasAnimationAttribute = "TeleFlow.Annotations.HasAnimationAttribute";
    public const string HasAudioAttribute = "TeleFlow.Annotations.HasAudioAttribute";
    public const string HasVoiceAttribute = "TeleFlow.Annotations.HasVoiceAttribute";
    public const string HasVideoNoteAttribute = "TeleFlow.Annotations.HasVideoNoteAttribute";
    public const string HasStickerAttribute = "TeleFlow.Annotations.HasStickerAttribute";
    public const string HasContactAttribute = "TeleFlow.Annotations.HasContactAttribute";
    public const string HasLocationAttribute = "TeleFlow.Annotations.HasLocationAttribute";
    public const string HasVenueAttribute = "TeleFlow.Annotations.HasVenueAttribute";
    public const string HasPollAttribute = "TeleFlow.Annotations.HasPollAttribute";
    public const string HasDiceAttribute = "TeleFlow.Annotations.HasDiceAttribute";
    public const string FromBotAttribute = "TeleFlow.Annotations.FromBotAttribute";
    public const string FromPremiumUserAttribute = "TeleFlow.Annotations.FromPremiumUserAttribute";
    public const string IsReplyAttribute = "TeleFlow.Annotations.IsReplyAttribute";
    public const string ReplyToBotAttribute = "TeleFlow.Annotations.ReplyToBotAttribute";
    public const string MessageThreadIdAttribute = "TeleFlow.Annotations.MessageThreadIdAttribute";
    public const string HasMessageThreadAttribute = "TeleFlow.Annotations.HasMessageThreadAttribute";
    public const string HasCallbackDataAttribute = "TeleFlow.Annotations.HasCallbackDataAttribute";
    public const string CallbackDataPrefixAttribute = "TeleFlow.Annotations.CallbackDataPrefixAttribute";
    public const string GenericUseFilterAttribute = "TeleFlow.Annotations.UseFilterAttribute<TFilter>";
    public const string GenericTelegramFilterAttribute = "TeleFlow.Annotations.TelegramFilterAttribute<TFilter>";
    public const string TextMatchMode = "TeleFlow.Annotations.TextMatchMode";
    public const string State = "TeleFlow.Core.States.State";
    public const string MessageContext = "TeleFlow.Telegram.MessageContext";
    public const string CallbackQueryContext = "TeleFlow.Telegram.CallbackQueryContext";
    public const string ChatMemberUpdatedContext = "TeleFlow.Telegram.ChatMemberUpdatedContext";
    public const string TelegramErrorContext = "TeleFlow.Telegram.TelegramErrorContext";
    public const string TelegramErrorHandlingResult = "TeleFlow.Telegram.TelegramErrorHandlingResult";
    public const string TelegramUpdateContext = "TeleFlow.Telegram.TelegramUpdateContext";
    public const string MessageHandler = "TeleFlow.Telegram.MessageHandler";
    public const string CallbackHandler = "TeleFlow.Telegram.CallbackHandler";
    public const string GenericCallbackHandler = "TeleFlow.Telegram.CallbackHandler<TPayload>";
    public const string ChatMemberUpdateHandler = "TeleFlow.Telegram.ChatMemberUpdateHandler";
    public const string GenericTelegramFilter = "TeleFlow.Telegram.ITelegramFilter<TContext>";
    public const string GenericParameterizedTelegramFilter = "TeleFlow.Telegram.ITelegramFilter<TContext, TAttribute>";
    public const string Task = "System.Threading.Tasks.Task";
    public const string ValueTask = "System.Threading.Tasks.ValueTask";
    public const string CancellationToken = "System.Threading.CancellationToken";
    public const string Exception = "System.Exception";

    public static bool HasAttribute(
        ISymbol symbol,
        string metadataName,
        bool inherit = false)
    {
        return GetAttributes(symbol, metadataName, inherit).Count > 0;
    }

    public static IReadOnlyList<AttributeData> GetAttributes(
        ISymbol symbol,
        string metadataName,
        bool inherit = false)
    {
        return GetAttributesCore(
            symbol,
            attribute => IsAttribute(attribute, metadataName),
            inherit,
            AllowsMultiple(metadataName));
    }

    public static bool HasGenericAttribute(
        ISymbol symbol,
        string metadataName,
        bool inherit = false)
    {
        return GetGenericAttributes(symbol, metadataName, inherit).Count > 0;
    }

    public static IReadOnlyList<AttributeData> GetGenericAttributes(
        ISymbol symbol,
        string metadataName,
        bool inherit = false)
    {
        return GetAttributesCore(
            symbol,
            attribute => IsGenericAttribute(attribute, metadataName),
            inherit,
            AllowsMultiple(metadataName));
    }

    public static AttributeData? GetFirstAttribute(
        ISymbol symbol,
        string metadataName,
        bool inherit = false)
    {
        IReadOnlyList<AttributeData> attributes = GetAttributes(symbol, metadataName, inherit);
        return attributes.Count > 0 ? attributes[0] : null;
    }

    public static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return string.Equals(
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            metadataName,
            StringComparison.Ordinal);
    }

    public static bool IsGenericAttribute(AttributeData attribute, string metadataName)
    {
        return string.Equals(
            attribute.AttributeClass?.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            metadataName,
            StringComparison.Ordinal);
    }

    public static bool IsType(ITypeSymbol symbol, string metadataName)
    {
        return string.Equals(
            symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            metadataName,
            StringComparison.Ordinal);
    }

    public static IReadOnlyList<AttributeData> GetTelegramFilterAttributes(
        ISymbol symbol,
        bool inherit = false)
    {
        return GetAttributesCore(
            symbol,
            static attribute => TryGetTelegramFilterAttributeFilterType(attribute, out _),
            inherit,
            allowMultiple: true);
    }

    public static bool TryGetTelegramFilterAttributeFilterType(
        AttributeData attribute,
        out ITypeSymbol filterType)
    {
        filterType = null!;

        if (attribute.AttributeClass is not { } attributeType)
        {
            return false;
        }

        for (INamedTypeSymbol? current = attributeType; current is not null; current = current.BaseType)
        {
            if (string.Equals(
                    current.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    GenericTelegramFilterAttribute,
                    StringComparison.Ordinal) &&
                current.TypeArguments.Length == 1)
            {
                filterType = current.TypeArguments[0];
                return true;
            }
        }

        return false;
    }

    public static bool HasAnyRouteAttribute(ISymbol symbol)
    {
        return HasAttribute(symbol, CommandAttribute, inherit: true) ||
               HasAttribute(symbol, MessageAttribute, inherit: true) ||
               HasAttribute(symbol, TextAttribute, inherit: true) ||
               HasAttribute(symbol, TextTemplateAttribute, inherit: true) ||
               HasAttribute(symbol, CommandTemplateAttribute, inherit: true) ||
               HasAttribute(symbol, TextRegexAttribute, inherit: true) ||
               HasAttribute(symbol, CommandRegexAttribute, inherit: true) ||
               HasAttribute(symbol, CallbackAttribute, inherit: true) ||
               HasGenericAttribute(symbol, GenericCallbackAttribute, inherit: true) ||
               HasAttribute(symbol, ChatMemberUpdatedAttribute, inherit: true) ||
               HasAttribute(symbol, MyChatMemberUpdatedAttribute, inherit: true);
    }

    public static bool HasAnyErrorAttribute(ISymbol symbol)
    {
        return GetErrorAttributes(symbol, inherit: true).Count > 0;
    }

    public static IReadOnlyList<AttributeData> GetErrorAttributes(
        ISymbol symbol,
        bool inherit = false)
    {
        return GetAttributesCore(
            symbol,
            static attribute => IsAttribute(attribute, ErrorAttribute) ||
                                IsGenericAttribute(attribute, GenericErrorAttribute),
            inherit,
            allowMultiple: true);
    }

    private static IReadOnlyList<AttributeData> GetAttributesCore(
        ISymbol symbol,
        Func<AttributeData, bool> predicate,
        bool inherit,
        bool allowMultiple)
    {
        if (!inherit)
        {
            return GetDirectAttributes(symbol, predicate);
        }

        return symbol switch
        {
            IMethodSymbol method => GetInheritedMethodAttributes(method, predicate, allowMultiple),
            INamedTypeSymbol type => GetInheritedTypeAttributes(type, predicate, allowMultiple),
            _ => GetDirectAttributes(symbol, predicate)
        };
    }

    private static IReadOnlyList<AttributeData> GetInheritedMethodAttributes(
        IMethodSymbol method,
        Func<AttributeData, bool> predicate,
        bool allowMultiple)
    {
        List<AttributeData> attributes = new List<AttributeData>();

        for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
        {
            AttributeData[] directAttributes = GetDirectAttributes(current, predicate);

            if (allowMultiple)
            {
                attributes.AddRange(directAttributes);
                continue;
            }

            if (directAttributes.Length > 0)
            {
                return directAttributes;
            }
        }

        return attributes;
    }

    private static IReadOnlyList<AttributeData> GetInheritedTypeAttributes(
        INamedTypeSymbol type,
        Func<AttributeData, bool> predicate,
        bool allowMultiple)
    {
        List<AttributeData> attributes = new List<AttributeData>();

        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            AttributeData[] directAttributes = GetDirectAttributes(current, predicate);

            if (allowMultiple)
            {
                attributes.AddRange(directAttributes);
                continue;
            }

            if (directAttributes.Length > 0)
            {
                return directAttributes;
            }
        }

        return attributes;
    }

    private static AttributeData[] GetDirectAttributes(
        ISymbol symbol,
        Func<AttributeData, bool> predicate)
    {
        return symbol
            .GetAttributes()
            .Where(predicate)
            .ToArray();
    }

    private static bool AllowsMultiple(string metadataName)
    {
        return metadataName is CommandAttribute or
            TextAttribute or
            TextTemplateAttribute or
            CommandTemplateAttribute or
            TextRegexAttribute or
            CommandRegexAttribute or
            ChatMemberTransitionAttribute or
            ChatMemberChangedAttribute or
            RequireTelegramRoleAttribute or
            StateAttribute or
            GenericStateAttribute or
            GenericUseFilterAttribute or
            GenericTelegramFilterAttribute or
            ErrorAttribute or
            GenericErrorAttribute;
    }
}
