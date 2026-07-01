using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class CurrentUpdateAccessorTests
{
    [Fact]
    public void UpdateContextAccessor_FailsClearlyOutsideUpdateProcessing()
    {
        var services = new ServiceCollection();
        services.AddUpdateContextAccessor();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<IUpdateContextAccessor>();

        Assert.False(accessor.IsAvailable);
        Assert.False(accessor.TryGetCurrent(out _));

        var exception = Assert.Throws<InvalidOperationException>(() => accessor.Current);

        Assert.Contains("current update", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateContextAccessor_IsAvailableToScopedMiddlewareAndDispatcher()
    {
        var recorder = new CoreAccessorRecorder();
        var services = new ServiceCollection();

        services.AddUpdateContextAccessor();
        services.AddSingleton(recorder);
        services.AddSingleton<IUpdateDispatcher, CoreAccessorDispatcher>();
        services.AddUpdateMiddleware<CoreAccessorMiddleware>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await ProcessCoreAsync(provider, new TestUpdatePayload("first"));

        Assert.Equal(["middleware:before:first", "dispatcher:first", "middleware:after:first"], recorder.Events);
    }

    [Fact]
    public async Task UpdateContextAccessor_IsScopedPerUpdate()
    {
        var recorder = new CoreAccessorRecorder();
        var services = new ServiceCollection();

        services.AddUpdateContextAccessor();
        services.AddSingleton(recorder);
        services.AddSingleton<IUpdateDispatcher, CoreAccessorDispatcher>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await ProcessCoreAsync(provider, new TestUpdatePayload("first"));
        await ProcessCoreAsync(provider, new TestUpdatePayload("second"));

        Assert.Equal(2, recorder.Accessors.Count);
        Assert.NotSame(recorder.Accessors[0], recorder.Accessors[1]);
    }

    [Fact]
    public void TelegramCurrentUpdateAccessor_FailsClearlyOutsideTelegramUpdateProcessing()
    {
        var services = CreateTelegramServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITelegramCurrentUpdateAccessor>();

        Assert.False(accessor.IsAvailable);
        Assert.False(accessor.TryGetCurrent(out _));
        Assert.Null(accessor.User);
        Assert.Null(accessor.Chat);
        Assert.Null(accessor.Message);
        Assert.Null(accessor.CallbackQuery);
        Assert.Null(accessor.ChatMemberUpdated);
        Assert.Null(accessor.StateKey);

        var exception = Assert.Throws<InvalidOperationException>(() => accessor.Current);

        Assert.Contains("current Telegram update", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TelegramCurrentUpdateAccessor_ExposesMessageIdentityToApplicationServices()
    {
        var probe = new TelegramAccessorProbe();
        var services = CreateTelegramServices();

        services.AddSingleton(probe);
        services.AddTelegramHandler<MessageAccessorHandler>();

        using var provider = BuildProvider(services);

        await ProcessTelegramAsync(provider, CreateMessageUpdate("/start"));

        Assert.Equal(
        [
            "message:True:1:5:100:/start:False:False:False"
        ], probe.Events);
    }

    [Fact]
    public async Task TelegramCurrentUpdateAccessor_ExposesCallbackIdentityToApplicationServices()
    {
        var probe = new TelegramAccessorProbe();
        var services = CreateTelegramServices();

        services.AddSingleton(probe);
        services.AddTelegramHandler<CallbackAccessorHandler>();

        using var provider = BuildProvider(services);

        await ProcessTelegramAsync(provider, CreateCallbackUpdate("open", includeMessage: true));

        Assert.Equal(
        [
            "callback:True:1:5:100:open:False:False:False"
        ], probe.Events);
    }

    [Fact]
    public async Task TelegramCurrentUpdateAccessor_ExposesChatMemberIdentityToApplicationServices()
    {
        var probe = new TelegramAccessorProbe();
        var services = CreateTelegramServices();

        services.AddSingleton(probe);
        services.AddTelegramHandler<ChatMemberAccessorHandler>();

        using var provider = BuildProvider(services);

        await ProcessTelegramAsync(
            provider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(
        [
            "chat-member:True:1:42:100:5:False:False:True"
        ], probe.Events);
    }

    [Fact]
    public async Task TelegramCurrentUpdateAccessor_ExposesStateKeyAfterStateMiddleware()
    {
        var probe = new TelegramAccessorProbe();
        var services = CreateTelegramServices();

        services.AddMemoryStateStorage();
        services.AddSingleton(probe);
        services.AddTelegramHandler<StateKeyAccessorHandler>();

        using var provider = BuildProvider(services);

        await ProcessTelegramAsync(provider, CreateMessageUpdate("state"));

        Assert.Equal(["state-key:telegram:user:5:chat:100"], probe.Events);
    }

    private static ServiceCollection CreateTelegramServices()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        return services;
    }

    private static ServiceProvider BuildProvider(IServiceCollection services)
    {
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static Task ProcessCoreAsync(ServiceProvider provider, IUpdatePayload payload)
    {
        var processor = new DefaultUpdateProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IUpdateDispatcher>(),
            provider.GetServices<UpdateMiddlewareRegistration>());

        return processor.ProcessAsync(payload);
    }

    private static Task ProcessTelegramAsync(ServiceProvider provider, Update update)
    {
        var processor = new DefaultUpdateProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IUpdateDispatcher>(),
            provider.GetServices<UpdateMiddlewareRegistration>());

        return processor.ProcessAsync(new TelegramUpdatePayload(update));
    }

    private static Update CreateMessageUpdate(string? text)
    {
        return new Update
        {
            UpdateId = 1,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                From = new User { Id = 5, IsBot = false, FirstName = "User" },
                Chat = new Chat { Id = 100, Type = "private" },
                Text = text
            }
        };
    }

    private static Update CreateCallbackUpdate(string? data, bool includeMessage)
    {
        return new Update
        {
            UpdateId = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb",
                From = new User
                {
                    Id = 5,
                    IsBot = false,
                    FirstName = "User"
                },
                ChatInstance = "chat-instance",
                Data = data,
                Message = includeMessage
                    ? MaybeInaccessibleMessage.From(new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        Text = "callback source"
                    })
                    : null
            }
        };
    }

    private static Update CreateChatMemberUpdate(
        bool isOwnUpdate,
        ChatMember oldChatMember,
        ChatMember newChatMember)
    {
        var chatMemberUpdated = new ChatMemberUpdated
        {
            Chat = new Chat { Id = 100, Type = "supergroup" },
            From = new User { Id = 42, IsBot = false, FirstName = "Actor" },
            Date = 0,
            OldChatMember = oldChatMember,
            NewChatMember = newChatMember
        };

        return new Update
        {
            UpdateId = 1,
            ChatMember = isOwnUpdate ? null : chatMemberUpdated,
            MyChatMember = isOwnUpdate ? chatMemberUpdated : null
        };
    }

    private static ChatMember MemberChatMember()
    {
        return ChatMember.From(new ChatMemberMember
        {
            User = new User { Id = 5, IsBot = false, FirstName = "Member" }
        });
    }

    private static ChatMember LeftChatMember()
    {
        return ChatMember.From(new ChatMemberLeft
        {
            User = new User { Id = 5, IsBot = false, FirstName = "Member" }
        });
    }

    private sealed record TestUpdatePayload(string Name) : IUpdatePayload;

    private sealed class CoreAccessorRecorder
    {
        public List<string> Events { get; } = [];

        public List<IUpdateContextAccessor> Accessors { get; } = [];
    }

    private sealed class CoreAccessorMiddleware(
        IUpdateContextAccessor accessor,
        CoreAccessorRecorder recorder) : IUpdateMiddleware
    {
        public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            recorder.Events.Add($"middleware:before:{((TestUpdatePayload)accessor.Current.Payload).Name}");
            await next(context);
            recorder.Events.Add($"middleware:after:{((TestUpdatePayload)accessor.Current.Payload).Name}");
        }
    }

    private sealed class CoreAccessorDispatcher(
        CoreAccessorRecorder recorder) : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            var accessor = context.Services.GetRequiredService<IUpdateContextAccessor>();

            recorder.Accessors.Add(accessor);
            recorder.Events.Add($"dispatcher:{((TestUpdatePayload)accessor.Current.Payload).Name}");
            Assert.Same(context, accessor.Current);
            return Task.CompletedTask;
        }
    }

    private sealed class TelegramAccessorProbe
    {
        public List<string> Events { get; } = [];
    }

    private sealed class MessageAccessorHandler(
        ITelegramCurrentUpdateAccessor current,
        TelegramAccessorProbe probe)
    {
        [Message]
        public Task Handle(MessageContext context)
        {
            probe.Events.Add(
                $"message:{current.IsAvailable}:{current.Update.UpdateId}:{current.User?.Id}:{current.Chat?.Id}:{current.Message?.Text}:{current.CallbackQuery is not null}:{current.ChatMemberUpdated is not null}:{current.TryGetCallbackQueryContext(out _)}");
            Assert.True(current.TryGetCurrent(out var currentContext));
            Assert.True(current.TryGetMessageContext(out var messageContext));
            Assert.Same(context, currentContext);
            Assert.Same(context, messageContext);
            return Task.CompletedTask;
        }
    }

    private sealed class CallbackAccessorHandler(
        ITelegramCurrentUpdateAccessor current,
        TelegramAccessorProbe probe)
    {
        [Callback]
        public Task Handle(CallbackQueryContext context)
        {
            probe.Events.Add(
                $"callback:{current.IsAvailable}:{current.Update.UpdateId}:{current.User?.Id}:{current.Chat?.Id}:{current.CallbackQuery?.Data}:{current.Message is not null}:{current.ChatMemberUpdated is not null}:{current.TryGetMessageContext(out _)}");
            Assert.True(current.TryGetCurrent(out var currentContext));
            Assert.True(current.TryGetCallbackQueryContext(out var callbackContext));
            Assert.Same(context, currentContext);
            Assert.Same(context, callbackContext);
            return Task.CompletedTask;
        }
    }

    private sealed class ChatMemberAccessorHandler(
        ITelegramCurrentUpdateAccessor current,
        TelegramAccessorProbe probe)
    {
        [ChatMemberUpdated]
        public Task Handle(ChatMemberUpdatedContext context)
        {
            probe.Events.Add(
                $"chat-member:{current.IsAvailable}:{current.Update.UpdateId}:{current.User?.Id}:{current.Chat?.Id}:{context.Member.Id}:{current.Message is not null}:{current.CallbackQuery is not null}:{current.TryGetChatMemberUpdatedContext(out _)}");
            Assert.True(current.TryGetCurrent(out var currentContext));
            Assert.True(current.TryGetChatMemberUpdatedContext(out var chatMemberContext));
            Assert.Same(context, currentContext);
            Assert.Same(context, chatMemberContext);
            return Task.CompletedTask;
        }
    }

    private sealed class StateKeyAccessorHandler(
        ITelegramCurrentUpdateAccessor current,
        TelegramAccessorProbe probe)
    {
        [Message]
        public Task Handle(MessageContext context)
        {
            var key = current.StateKey;
            probe.Events.Add($"state-key:{key?.Scope}:{key?.Subject}:{key?.Partition}");
            return Task.CompletedTask;
        }
    }
}
