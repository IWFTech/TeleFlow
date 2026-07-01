using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;
using TeleFlow.Telegram.Webhooks;

namespace TeleFlow.ArchitectureTests;

public sealed class RuntimeDependencyValidationTests
{
    [Fact]
    public void Build_FailsClearlyForMissingHandlerServiceParameter()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddTelegramHandler<HandlerWithMissingServiceParameter>();

        var exception = Assert.Throws<TeleFlowConfigurationException>(() => builder.Build());

        Assert.Contains("Handler dependency was not registered", exception.Message);
        Assert.Contains(nameof(HandlerWithMissingServiceParameter), exception.Message);
        Assert.Contains(nameof(IMissingHandlerService), exception.Message);
    }

    [Fact]
    public void Build_FailsClearlyForMissingErrorHandlerServiceParameter()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddTelegramHandler<ThrowingMessageHandler>();
        builder.Services.AddTelegramHandler<ErrorHandlerWithMissingServiceParameter>();

        var exception = Assert.Throws<TeleFlowConfigurationException>(() => builder.Build());

        Assert.Contains("Error handler dependency was not registered", exception.Message);
        Assert.Contains(nameof(ErrorHandlerWithMissingServiceParameter), exception.Message);
        Assert.Contains(nameof(IMissingErrorHandlerService), exception.Message);
    }

    [Fact]
    public void Build_FailsClearlyForMissingCustomFilterRegistration()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddTelegramHandler<HandlerWithMissingCustomFilter>();

        var exception = Assert.Throws<TeleFlowConfigurationException>(() => builder.Build());

        Assert.Contains("Custom filter was not registered", exception.Message);
        Assert.Contains(nameof(HandlerWithMissingCustomFilter), exception.Message);
        Assert.Contains(nameof(MissingCustomFilter), exception.Message);
    }

    [Fact]
    public void Build_ReportsMultipleMissingDependenciesTogether()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddTelegramHandler<HandlerWithMissingServiceParameter>();
        builder.Services.AddTelegramHandler<HandlerWithMissingCustomFilter>();

        var exception = Assert.Throws<TeleFlowConfigurationException>(() => builder.Build());

        Assert.Contains(nameof(IMissingHandlerService), exception.Message);
        Assert.Contains(nameof(MissingCustomFilter), exception.Message);
    }

    [Fact]
    public async Task DefaultUpdateProcessor_FallbackValidationFailsBeforeDispatch()
    {
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddTelegramHandler<HandlerWithMissingServiceParameter>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var processor = new DefaultUpdateProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IUpdateDispatcher>(),
            provider.GetServices<TeleFlow.Framework.Middleware.UpdateMiddlewareRegistration>());

        var exception = await Assert.ThrowsAsync<TeleFlowConfigurationException>(() =>
            processor.ProcessAsync(new TelegramUpdatePayload(CreateMessageUpdate("/start"))));

        Assert.Contains(nameof(IMissingHandlerService), exception.Message);
        Assert.Equal(0, dispatcher.DispatchCount);
    }

    [Fact]
    public async Task WebhookEndpoint_FailsValidationBeforeProcessingUpdate()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddTelegramHandler<HandlerWithMissingServiceParameter>();
        builder.Services.AddWebhook();

        await using var app = builder.Build();

        var exception = Assert.Throws<TeleFlowConfigurationException>(() => app.MapTelegramWebhook());

        Assert.Contains(nameof(IMissingHandlerService), exception.Message);
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

    private sealed class NoOpUpdateSource : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDispatcher : IUpdateDispatcher
    {
        public int DispatchCount { get; private set; }

        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            return Task.CompletedTask;
        }
    }

    private interface IMissingHandlerService;

    private interface IMissingErrorHandlerService;

    private sealed class HandlerWithMissingServiceParameter
    {
        [Command("start")]
        public Task Handle(MessageContext context, IMissingHandlerService missing)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingMessageHandler
    {
        [Message]
        public Task Handle(MessageContext context)
        {
            return Task.FromException(new InvalidOperationException("handler failed"));
        }
    }

    private sealed class ErrorHandlerWithMissingServiceParameter
    {
        [Error<InvalidOperationException>]
        public TelegramErrorHandlingResult Handle(
            InvalidOperationException exception,
            IMissingErrorHandlerService missing)
        {
            return TelegramErrorHandlingResult.Handled;
        }
    }

    private sealed class HandlerWithMissingCustomFilter
    {
        [Message]
        [UseFilter<MissingCustomFilter>]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class MissingCustomFilter : ITelegramFilter<MessageContext>
    {
        public ValueTask<bool> MatchesAsync(
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(true);
        }
    }
}
