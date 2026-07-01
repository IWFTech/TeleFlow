using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;
using TeleFlow.Telegram.Webhooks;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramWebhookTests
{
    [Fact]
    public async Task MapTelegramWebhook_MapsConfiguredPostEndpoint()
    {
        await using var app = CreateApp(
            static (_, _, _) => Task.FromResult<IResult>(Results.Ok()),
            path: "/bot/hook");

        var route = GetSingleRoute(app);
        var methods = route.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods;

        Assert.Equal("/bot/hook", route.RoutePattern.RawText);
        Assert.Equal(["POST"], methods);
    }

    [Fact]
    public async Task RawWebhookEndpoint_ProcessesValidUpdateThroughHandler()
    {
        var bot = new FakeTelegramClient();
        Update? receivedUpdate = null;
        ITelegramClient? receivedBot = null;

        await using var app = CreateApp(
            (update, telegramBot, _) =>
            {
                receivedUpdate = update;
                receivedBot = telegramBot;
                return Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status202Accepted));
            },
            bot: bot);

        var context = await InvokeAsync(app, ValidUpdateJson);

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal(123, receivedUpdate?.UpdateId);
        Assert.Same(bot, receivedBot);
    }

    [Fact]
    public async Task RawWebhookEndpoint_ReturnsBadRequestForInvalidJson()
    {
        var invoked = false;
        await using var app = CreateApp((_, _, _) =>
        {
            invoked = true;
            return Task.FromResult<IResult>(Results.Ok());
        });

        var context = await InvokeAsync(app, "{ invalid");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task RawWebhookEndpoint_ReturnsBadRequestForNullUpdate()
    {
        var invoked = false;
        await using var app = CreateApp((_, _, _) =>
        {
            invoked = true;
            return Task.FromResult<IResult>(Results.Ok());
        });

        var context = await InvokeAsync(app, "null");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task RawWebhookEndpoint_RejectsMissingSecretToken()
    {
        var invoked = false;
        await using var app = CreateApp(
            (_, _, _) =>
            {
                invoked = true;
                return Task.FromResult<IResult>(Results.Ok());
            },
            configure: options => options.SecretToken = "secret");

        var context = await InvokeAsync(app, ValidUpdateJson);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task RawWebhookEndpoint_RejectsInvalidSecretToken()
    {
        var invoked = false;
        await using var app = CreateApp(
            (_, _, _) =>
            {
                invoked = true;
                return Task.FromResult<IResult>(Results.Ok());
            },
            configure: options => options.SecretToken = "secret");

        var context = await InvokeAsync(app, ValidUpdateJson, secretToken: "wrong");

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task RawWebhookEndpoint_LogsSecretTokenRejectionWithoutSecretValues()
    {
        var loggerFactory = new RecordingLoggerFactory();
        await using var app = CreateApp(
            static (_, _, _) => Task.FromResult<IResult>(Results.Ok()),
            configure: options => options.SecretToken = "expected-secret-value",
            loggerFactory: loggerFactory);

        var context = await InvokeAsync(app, ValidUpdateJson, secretToken: "wrong-secret-value");

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        var warning = Assert.Single(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.EventId.Id == 1 &&
                     entry.Category == "TeleFlow.Telegram.Webhooks.Internal.TelegramRawWebhookEndpoint");

        Assert.Contains("secret token validation failed", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("expected-secret-value", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-secret-value", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RawWebhookEndpoint_LogsInvalidPayloadWithoutRequestBody()
    {
        var loggerFactory = new RecordingLoggerFactory();
        await using var app = CreateApp(
            static (_, _, _) => Task.FromResult<IResult>(Results.Ok()),
            loggerFactory: loggerFactory);

        var context = await InvokeAsync(app, "{ invalid");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var warning = Assert.Single(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.EventId.Id == 2 &&
                     entry.Category == "TeleFlow.Telegram.Webhooks.Internal.TelegramRawWebhookEndpoint");

        Assert.Contains("payload was invalid", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{ invalid", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RawWebhookEndpoint_LogsAcceptedUpdateWithoutMessageText()
    {
        var loggerFactory = new RecordingLoggerFactory();
        await using var app = CreateApp(
            static (_, _, _) => Task.FromResult<IResult>(Results.Ok()),
            loggerFactory: loggerFactory);

        var context = await InvokeAsync(app, ValidUpdateJson);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 3 &&
                     entry.Message.Contains("update_id=123", StringComparison.Ordinal) &&
                     entry.Message.Contains("type=message", StringComparison.Ordinal) &&
                     !entry.Message.Contains("hello", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 4 &&
                     entry.Message.Contains("update_id=123", StringComparison.Ordinal) &&
                     entry.Message.Contains("type=message", StringComparison.Ordinal) &&
                     !entry.Message.Contains("hello", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RawWebhookEndpoint_AcceptsValidSecretToken()
    {
        var invoked = false;
        await using var app = CreateApp(
            (_, _, _) =>
            {
                invoked = true;
                return Task.FromResult<IResult>(Results.Ok());
            },
            configure: options => options.SecretToken = "secret");

        var context = await InvokeAsync(app, ValidUpdateJson, secretToken: "secret");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(invoked);
    }

    [Fact]
    public async Task RawWebhookEndpoint_UsesConfiguredFailureStatusCodes()
    {
        var invoked = false;
        await using var app = CreateApp(
            (_, _, _) =>
            {
                invoked = true;
                return Task.FromResult<IResult>(Results.Ok());
            },
            configure: options =>
            {
                options.SecretToken = "secret";
                options.InvalidPayloadStatusCode = StatusCodes.Status422UnprocessableEntity;
                options.SecretTokenFailureStatusCode = StatusCodes.Status403Forbidden;
            });

        var invalidJsonContext = await InvokeAsync(app, "{ invalid", secretToken: "secret");
        var invalidSecretContext = await InvokeAsync(app, ValidUpdateJson, secretToken: "wrong");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, invalidJsonContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, invalidSecretContext.Response.StatusCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task RawWebhookEndpoint_PassesRequestAbortedToHandler()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken receivedCancellationToken = default;

        await using var app = CreateApp((_, _, cancellationToken) =>
        {
            receivedCancellationToken = cancellationToken;
            return Task.FromResult<IResult>(Results.Ok());
        });

        await InvokeAsync(app, ValidUpdateJson, requestAborted: cancellation.Token);

        Assert.Equal(cancellation.Token, receivedCancellationToken);
    }

    [Fact]
    public async Task RawWebhookEndpoint_HandlerExceptionBubblesUnchanged()
    {
        var exception = new InvalidOperationException("handler failed");
        await using var app = CreateApp((_, _, _) => throw exception);
        var context = CreateHttpContext(app.Services, ValidUpdateJson);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GetSingleRoute(app).RequestDelegate!(context));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public void TelegramRawWebhookOptions_RejectsInvalidFailureStatusCodes()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            app.MapTelegramWebhook(
                "/telegram",
                static (_, _, _) => Task.FromResult<IResult>(Results.Ok()),
                options => options.InvalidPayloadStatusCode = 99));

        Assert.Throws<InvalidOperationException>(() =>
            app.MapTelegramWebhook(
                "/telegram",
                static (_, _, _) => Task.FromResult<IResult>(Results.Ok()),
                options => options.SecretTokenFailureStatusCode = 600));
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

    private static WebApplication CreateApp(
        TelegramRawWebhookHandler handler,
        string path = "/telegram",
        FakeTelegramClient? bot = null,
        Action<TelegramRawWebhookOptions>? configure = null,
        RecordingLoggerFactory? loggerFactory = null)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ITelegramClient>(bot ?? new FakeTelegramClient());
        if (loggerFactory is not null)
        {
            builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);
        }

        var app = builder.Build();
        app.MapTelegramWebhook(path, handler, configure);
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

    private sealed class FakeTelegramClient : ITelegramClient
    {
        public TelegramBotDefaults Defaults { get; } = new();

        public TelegramDeepLinks DeepLinks { get; } = new((string?)null, new Base64UrlJsonDeepLinkPayloadSerializer());

        public Task<TResult> SendAsync<TResult>(
            ITelegramApiMethod<TResult> method,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
