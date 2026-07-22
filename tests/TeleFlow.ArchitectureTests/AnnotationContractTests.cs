using TeleFlow.Annotations;
using TeleFlow.Telegram;

namespace TeleFlow.ArchitectureTests;

public sealed class AnnotationContractTests
{
    [Fact]
    public void PublicAnnotationTypes_KeepStableRootNamespace()
    {
        var publicTypes = typeof(CommandAttribute).Assembly.GetExportedTypes();

        Assert.All(publicTypes, type => Assert.Equal("TeleFlow.Annotations", type.Namespace));
    }

    [Fact]
    public void TelegramChatType_ContainsOnlyActualChatTypeValues()
    {
        Assert.Equal(
            [
                TelegramChatType.Private,
                TelegramChatType.Group,
                TelegramChatType.Supergroup,
                TelegramChatType.Channel
            ],
            Enum.GetValues<TelegramChatType>());
    }

    [Fact]
    public void ChatTypeAttribute_DefensivelyCopiesInputArray()
    {
        var chatTypes = new[] { TelegramChatType.Private };

        var attribute = new ChatTypeAttribute(chatTypes);
        chatTypes[0] = TelegramChatType.Group;

        Assert.Equal([TelegramChatType.Private], attribute.ChatTypes);
    }

    [Fact]
    public void SenderChatTypeAttribute_DefensivelyCopiesInputArray()
    {
        var chatTypes = new[] { TelegramChatType.Channel };

        var attribute = new SenderChatTypeAttribute(chatTypes);
        chatTypes[0] = TelegramChatType.Supergroup;

        Assert.Equal([TelegramChatType.Channel], attribute.ChatTypes);
    }

    [Fact]
    public void FromUserAttribute_DefensivelyCopiesInputArray()
    {
        var userIds = new[] { 100L };

        var attribute = new FromUserAttribute(userIds);
        userIds[0] = 200;

        Assert.Equal([100L], attribute.UserIds);
    }

    [Fact]
    public void ChatIdAttribute_DefensivelyCopiesInputArray()
    {
        var chatIds = new[] { -100L };

        var attribute = new ChatIdAttribute(chatIds);
        chatIds[0] = -200;

        Assert.Equal([-100L], attribute.ChatIds);
    }

    [Fact]
    public void MessageThreadIdAttribute_DefensivelyCopiesInputArray()
    {
        var threadIds = new[] { 42L };

        var attribute = new MessageThreadIdAttribute(threadIds);
        threadIds[0] = 77;

        Assert.Equal([42L], attribute.MessageThreadIds);
    }

    [Fact]
    public void TelegramGeneratedFilterDescriptor_DefensivelyCopiesInputCollections()
    {
        var stringValues = new[] { "admin:" };
        var longValues = new[] { 100L };

        var descriptor = new TelegramGeneratedFilterDescriptor(
            TelegramGeneratedFilterKind.CallbackDataPrefix,
            stringValues,
            longValues);
        stringValues[0] = "mutated:";
        longValues[0] = 200;

        Assert.Equal(["admin:"], descriptor.StringValues);
        Assert.Equal([100L], descriptor.LongValues);
    }

    [Fact]
    public void TelegramGeneratedFilterKind_PreservesExistingNumericValues()
    {
        Assert.Equal(27, (int)TelegramGeneratedFilterKind.Custom);
    }

    [Fact]
    public void TelegramGeneratedHandlerDescriptor_DefensivelyCopiesInputCollections()
    {
        var commandPrefixes = new[] { "!" };
        var textFilters = new[]
        {
            new TelegramGeneratedTextFilterDescriptor("help", TextMatchMode.Equals, ignoreCase: true)
        };
        var filters = new[]
        {
            new TelegramGeneratedFilterDescriptor(TelegramGeneratedFilterKind.ChatId, longValues: [100])
        };
        var transitions = new[]
        {
            new TelegramGeneratedChatMemberTransitionDescriptor(
                TelegramMemberStatusSet.IsNotMember,
                TelegramMemberStatusSet.IsMember)
        };
        var roles = new[]
        {
            new TelegramGeneratedRoleRequirementDescriptor(TelegramMemberStatusSet.IsAdmin)
        };
        var states = new[] { "registration:name" };
        var parameters = new[]
        {
            new TelegramGeneratedHandlerParameterDescriptor(
                typeof(MessageContext),
                TelegramGeneratedHandlerParameterKind.Context,
                "context")
        };
        var routeValues = new[]
        {
            new TelegramGeneratedRouteValueDescriptor("id", typeof(long), isOptional: false)
        };

        var descriptor = new TelegramGeneratedHandlerDescriptor(
            typeof(object),
            "Handle",
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.TextTemplate,
            routePattern: "order {id:long}",
            commandPrefixes,
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 1,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters,
            filters,
            transitions,
            roles,
            states,
            parameters,
            static (_, _, _) => ValueTask.CompletedTask,
            routeValues: routeValues,
            prefixMode: CommandPrefixMode.Optional);

        commandPrefixes[0] = "/";
        textFilters[0] = new TelegramGeneratedTextFilterDescriptor("mutated", TextMatchMode.Equals, ignoreCase: true);
        filters[0] = new TelegramGeneratedFilterDescriptor(TelegramGeneratedFilterKind.ChatId, longValues: [200]);
        transitions[0] = new TelegramGeneratedChatMemberTransitionDescriptor(
            TelegramMemberStatusSet.IsMember,
            TelegramMemberStatusSet.IsNotMember);
        roles[0] = new TelegramGeneratedRoleRequirementDescriptor(TelegramMemberStatusSet.IsMember);
        states[0] = "registration:age";
        parameters[0] = new TelegramGeneratedHandlerParameterDescriptor(
            typeof(string),
            TelegramGeneratedHandlerParameterKind.Service,
            "service");
        routeValues[0] = new TelegramGeneratedRouteValueDescriptor("other", typeof(string), isOptional: true);

        Assert.Equal(["!"], descriptor.CommandPrefixes);
        Assert.Equal(CommandPrefixMode.Optional, descriptor.PrefixMode);
        Assert.Equal("help", Assert.Single(descriptor.TextFilters).Value);
        Assert.Equal([100L], Assert.Single(descriptor.Filters).LongValues);
        Assert.Equal(TelegramMemberStatusSet.IsNotMember, Assert.Single(descriptor.ChatMemberTransitions).OldStatus);
        Assert.Equal(TelegramMemberStatusSet.IsAdmin, Assert.Single(descriptor.RoleRequirements).AllowedStatuses);
        Assert.Equal(["registration:name"], descriptor.States);
        Assert.Equal(typeof(MessageContext), Assert.Single(descriptor.Parameters).ParameterType);
        Assert.Equal("id", Assert.Single(descriptor.RouteValues).Name);
    }

    [Fact]
    public void TelegramGeneratedErrorHandlerDescriptor_DefensivelyCopiesInputCollections()
    {
        var parameters = new[]
        {
            new TelegramGeneratedErrorHandlerParameterDescriptor(
                typeof(TelegramErrorContext),
                TelegramGeneratedErrorHandlerParameterKind.ErrorContext,
                "error")
        };

        var descriptor = new TelegramGeneratedErrorHandlerDescriptor(
            typeof(object),
            "Handle",
            exceptionType: typeof(InvalidOperationException),
            telegramContextType: typeof(MessageContext),
            registrationOrder: 1,
            moduleName: "orders",
            parameters,
            static (_, _, _) => ValueTask.FromResult(TelegramErrorHandlingResult.Handled));

        parameters[0] = new TelegramGeneratedErrorHandlerParameterDescriptor(
            typeof(string),
            TelegramGeneratedErrorHandlerParameterKind.Service,
            "service");

        Assert.Equal(typeof(TelegramErrorContext), Assert.Single(descriptor.Parameters).ParameterType);
    }
}
