using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Webhooks;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramFrameworkWebhookTests
{
    [Fact]
    public void AddWebhook_FailsWhenTelegramBotIsMissing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWebhook());

        Assert.Contains("AddTelegramBot must be called before AddWebhook.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddWebhook_FailsWhenUpdateSourceAlreadyRegistered()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<IUpdateSource, ExistingUpdateSource>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWebhook());

        Assert.Contains("AddWebhook cannot be called more than once for service 'IUpdateSource'.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddLongPolling_FailsWhenWebhookAlreadyRegistered()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddWebhook();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddLongPolling());

        Assert.Contains("AddLongPolling cannot be called more than once for service 'IUpdateSource'.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddWebhook_RegistersDefaultUpdateProcessorWhenNotReplaced()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<IUpdateDispatcher, NoOpUpdateDispatcher>();

        services.AddWebhook();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DefaultUpdateProcessor>(provider.GetRequiredService<IUpdateProcessor>());
    }

    [Fact]
    public void AddWebhook_DoesNotReplaceCustomUpdateProcessor()
    {
        var services = new ServiceCollection();
        var processor = new RecordingUpdateProcessor();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<IUpdateProcessor>(processor);

        services.AddWebhook();

        using var provider = services.BuildServiceProvider();
        Assert.Same(processor, provider.GetRequiredService<IUpdateProcessor>());
    }

    [Fact]
    public void AddWebhook_RegistersSingleWebhookUpdateSource()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddWebhook();

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IUpdateSource>());
    }

    [Fact]
    public void TelegramWebhookOptions_ValidatesPathAndSecretToken()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");

        var pathException = Assert.Throws<InvalidOperationException>(() =>
            services.AddWebhook(options => options.Path = "telegram"));
        var secretException = Assert.Throws<InvalidOperationException>(() =>
            services.AddWebhook(options => options.SecretToken = " "));

        Assert.Contains("Webhook path must start with '/'.", pathException.Message, StringComparison.Ordinal);
        Assert.Contains("Webhook secret token must not be empty.", secretException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MapTelegramWebhook_MapsConfiguredPostEndpoint()
    {
        await using var app = CreateApp(path: "/bot/hook");

        var route = GetSingleRoute(app);
        var methods = route.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods;

        Assert.Equal("/bot/hook", route.RoutePattern.RawText);
        Assert.Equal(["POST"], methods);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_ProcessesValidUpdateThroughProcessor()
    {
        var processor = new RecordingUpdateProcessor();
        await using var app = CreateApp(processor: processor);

        var context = await InvokeAsync(app, ValidUpdateJson);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var payload = Assert.IsType<TelegramUpdatePayload>(Assert.Single(processor.Payloads));
        Assert.Equal(123, payload.Update.UpdateId);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_DispatchesToRegisteredHandlerThroughDefaultProcessor()
    {
        var probe = new WebhookHandlerProbe();
        await using var app = CreateHandlerDispatchApp(probe);

        var context = await InvokeAsync(app, ValidCommandUpdateJson);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(["handler:/webhook:123"], probe.Events);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_ReturnsOkAfterHandledTelegramError()
    {
        var probe = new WebhookHandlerProbe();
        await using var app = CreateHandledErrorDispatchApp(probe);

        var context = await InvokeAsync(app, ValidErrorCommandUpdateJson);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(["error:webhook handled:123"], probe.Events);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_ReturnsBadRequestForInvalidJson()
    {
        var processor = new RecordingUpdateProcessor();
        await using var app = CreateApp(processor: processor);

        var context = await InvokeAsync(app, "{ invalid");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Empty(processor.Payloads);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_RejectsMissingSecretToken()
    {
        var processor = new RecordingUpdateProcessor();
        await using var app = CreateApp(processor: processor, secretToken: "secret");

        var context = await InvokeAsync(app, ValidUpdateJson);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Empty(processor.Payloads);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_AcceptsValidSecretToken()
    {
        var processor = new RecordingUpdateProcessor();
        await using var app = CreateApp(processor: processor, secretToken: "secret");

        var context = await InvokeAsync(app, ValidUpdateJson, secretToken: "secret");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Single(processor.Payloads);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_HandlerExceptionBubblesUnchanged()
    {
        var exception = new InvalidOperationException("processor failed");
        var processor = new RecordingUpdateProcessor(exception);
        await using var app = CreateApp(processor: processor);
        var context = CreateHttpContext(app.Services, ValidUpdateJson);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GetSingleRoute(app).RequestDelegate!(context));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task FrameworkWebhookEndpoint_PassesRequestAbortedToProcessor()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new RecordingUpdateProcessor();
        await using var app = CreateApp(processor: processor);

        await InvokeAsync(app, ValidUpdateJson, requestAborted: cancellation.Token);

        Assert.Equal(cancellation.Token, processor.CancellationTokens.Single());
    }

    private const string ValidUpdateJson = """
        {
          "update_id": 123,
          "message": {
            "message_id": 1,
            "date": 1700000000,
            "chat": {
              "id": 42,
              "type": "private"
            },
            "text": "hello"
          }
        }
        """;

    private const string ValidErrorCommandUpdateJson = """
        {
          "update_id": 123,
          "message": {
            "message_id": 1,
            "date": 1700000000,
            "chat": {
              "id": 42,
              "type": "private"
            },
            "text": "/webhook-error"
          }
        }
        """;

    private const string ValidCommandUpdateJson = """
        {
          "update_id": 123,
          "message": {
            "message_id": 1,
            "date": 1700000000,
            "chat": {
              "id": 42,
              "type": "private"
            },
            "text": "/webhook"
          }
        }
        """;

    private static WebApplication CreateApp(
        string path = "/telegram",
        string? secretToken = null,
        RecordingUpdateProcessor? processor = null)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");

        if (processor is not null)
        {
            builder.Services.AddSingleton<IUpdateProcessor>(processor);
        }
        else
        {
            builder.Services.AddSingleton<IUpdateDispatcher, NoOpUpdateDispatcher>();
        }

        builder.Services.AddWebhook(options =>
        {
            options.Path = path;
            options.SecretToken = secretToken;
        });

        var app = builder.Build();
        app.MapTelegramWebhook();
        return app;
    }

    private static WebApplication CreateHandlerDispatchApp(WebhookHandlerProbe probe)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton(probe);
        builder.Services.AddTelegramHandler<WebhookEndToEndMessageHandler>();
        builder.Services.AddWebhook();

        var app = builder.Build();
        app.MapTelegramWebhook();
        return app;
    }

    private static WebApplication CreateHandledErrorDispatchApp(WebhookHandlerProbe probe)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton(probe);
        builder.Services.AddTelegramHandler<WebhookThrowingMessageHandler>();
        builder.Services.AddTelegramHandler<WebhookErrorHandler>();
        builder.Services.AddWebhook();

        var app = builder.Build();
        app.MapTelegramWebhook();
        return app;
    }

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication app,
        string body,
        string? secretToken = null,
        CancellationToken requestAborted = default)
    {
        var context = CreateHttpContext(app.Services, body, secretToken, requestAborted);

        await GetSingleRoute(app).RequestDelegate!(context);
        return context;
    }

    private static RouteEndpoint GetSingleRoute(WebApplication app)
    {
        var endpointDataSource = Assert.Single(((IEndpointRouteBuilder)app).DataSources);
        return Assert.IsType<RouteEndpoint>(Assert.Single(endpointDataSource.Endpoints));
    }

    private static DefaultHttpContext CreateHttpContext(
        IServiceProvider services,
        string body,
        string? secretToken = null,
        CancellationToken requestAborted = default)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.RequestAborted = requestAborted;

        if (secretToken is not null)
        {
            context.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = secretToken;
        }

        return context;
    }

    private sealed class ExistingUpdateSource : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUpdateProcessor(Exception? exception = null) : IUpdateProcessor
    {
        public List<IUpdatePayload> Payloads { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task ProcessAsync(IUpdatePayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            CancellationTokens.Add(cancellationToken);

            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class NoOpUpdateDispatcher : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Command("webhook")]
    private sealed class WebhookEndToEndMessageHandler : MessageHandler
    {
        public Task HandleAsync(MessageContext context, WebhookHandlerProbe probe)
        {
            probe.Events.Add($"handler:{context.TelegramMessage.Text}:{context.Update.UpdateId}");
            return Task.CompletedTask;
        }
    }

    private sealed class WebhookHandledException : InvalidOperationException
    {
        public WebhookHandledException()
            : base("webhook handled")
        {
        }
    }

    [Command("webhook-error")]
    private sealed class WebhookThrowingMessageHandler : MessageHandler
    {
        public Task HandleAsync(MessageContext context)
        {
            return Task.FromException(new WebhookHandledException());
        }
    }

    private sealed class WebhookErrorHandler
    {
        [Error<WebhookHandledException>]
        public TelegramErrorHandlingResult Handle(
            TelegramErrorContext error,
            WebhookHandledException exception,
            WebhookHandlerProbe probe)
        {
            probe.Events.Add($"error:{exception.Message}:{error.UpdateContext.Update.UpdateId}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    private sealed class WebhookHandlerProbe
    {
        public List<string> Events { get; } = [];
    }
}
