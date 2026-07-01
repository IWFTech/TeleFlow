using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Methods;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramRuntimeIntegrationTests
{
    [Fact]
    public void AddTelegramBot_RegistersClientAndExecutor()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<ITelegramClient>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITelegramRequestExecutor>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITelegramTransport>());
        Assert.NotNull(serviceProvider.GetRequiredService<TelegramJsonOptions>());
        Assert.NotNull(serviceProvider.GetRequiredService<TelegramDeepLinks>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDeepLinkPayloadSerializer>());
        Assert.Empty(serviceProvider.GetServices<HttpClientTelegramTransport>());
        Assert.Empty(serviceProvider.GetServices<HttpClient>());
        Assert.Empty(serviceProvider.GetServices<JsonSerializerOptions>());
    }

    [Fact]
    public void AddTelegramClient_RegistersLowLevelClientServices()
    {
        var services = new ServiceCollection();

        services.AddTelegramClient(options => options.Token = "test-token");

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<TelegramClientOptions>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITelegramClient>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITelegramRequestExecutor>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITelegramTransport>());
        Assert.NotNull(serviceProvider.GetRequiredService<TelegramJsonOptions>());
        Assert.NotNull(serviceProvider.GetRequiredService<TelegramDeepLinks>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDeepLinkPayloadSerializer>());
        Assert.Empty(serviceProvider.GetServices<HttpClientTelegramTransport>());
        Assert.Empty(serviceProvider.GetServices<HttpClient>());
        Assert.Empty(serviceProvider.GetServices<JsonSerializerOptions>());
    }

    [Fact]
    public void AddTelegramBot_ConfiguresNonNullDefaults()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options =>
        {
            options.Token = "test-token";
            options.Defaults.ParseMode = TelegramParseMode.Html;
            options.Defaults.DisableNotification = true;
            options.Defaults.ProtectContent = true;
        });

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        Assert.Equal(TelegramParseMode.Html, client.Defaults.ParseMode);
        Assert.True(client.Defaults.DisableNotification);
        Assert.True(client.Defaults.ProtectContent);
        Assert.Same(TelegramRetryAfterPolicy.Default, serviceProvider.GetRequiredService<TelegramClientOptions>().RetryAfter);
    }

    [Fact]
    public void AddTelegramBot_ValidatesRetryAfterPolicy()
    {
        var invalidCases = new (Action<TelegramBotOptions> Configure, string ExpectedMessage)[]
        {
            (options => options.RetryAfter = null!, "retry-after policy must be configured"),
            (options => options.RetryAfter = TelegramRetryAfterPolicy.Default with { MaxRetries = -1 }, "maximum retry count must not be negative"),
            (options => options.RetryAfter = TelegramRetryAfterPolicy.Default with { MaxDelay = TimeSpan.Zero }, "maximum retry delay must be greater than zero"),
            (options => options.RetryAfter = TelegramRetryAfterPolicy.Default with { MaxDelay = TimeSpan.FromSeconds(-1) }, "maximum retry delay must be greater than zero")
        };

        foreach (var (configure, expectedMessage) in invalidCases)
        {
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddTelegramBot(options =>
                {
                    options.Token = "test-token";
                    configure(options);
                }));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TelegramDeepLinks_BuildsStartAndStartGroupLinks()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            configureBot: options => options.BotUsername = "@my_bot");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        Assert.Equal("https://t.me/my_bot?start=abc_123", client.DeepLinks.Start("abc_123").ToString());
        Assert.Equal("https://t.me/my_bot?startgroup=setup-42", client.DeepLinks.StartGroup("setup-42").ToString());
    }

    [Fact]
    public void TelegramDeepLinks_NormalizesConfiguredBotUsername()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            configureBot: options => options.BotUsername = " @my_bot ");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        Assert.Equal("https://t.me/my_bot?start=abc", client.DeepLinks.Start("abc").ToString());
    }

    [Fact]
    public void TelegramDeepLinks_FailsClearlyWhenBotUsernameIsMissing()
    {
        using var serviceProvider = CreateTelegramServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = Assert.Throws<InvalidOperationException>(() => client.DeepLinks.Start("abc"));

        Assert.Contains("bot username", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("@")]
    [InlineData("bad bot")]
    [InlineData("bad/bot")]
    public void AddTelegramBot_RejectsInvalidConfiguredBotUsername(string username)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddTelegramBot(options =>
            {
                options.Token = "test-token";
                options.BotUsername = username;
            }));

        Assert.Contains("bot username", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("hello world")]
    [InlineData("hello!")]
    public void TelegramDeepLinks_RejectsInvalidPayload(string payload)
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            configureBot: options => options.BotUsername = "my_bot");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = Assert.Throws<ArgumentException>(() => client.DeepLinks.Start(payload));

        Assert.Contains("payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TelegramDeepLinks_RejectsPayloadOverTelegramLimit()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            configureBot: options => options.BotUsername = "my_bot");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        var payload = new string('a', 65);

        var exception = Assert.Throws<ArgumentException>(() => client.DeepLinks.Start(payload));

        Assert.Contains("64", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TelegramDeepLinks_TypedPayloadRoundTripsThroughDefaultSerializer()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            configureBot: options => options.BotUsername = "my_bot");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var payload = client.DeepLinks.Serialize(new DeepLinkInvitePayload(42));
        var link = client.DeepLinks.Start(new DeepLinkInvitePayload(42));
        var deserialized = client.DeepLinks.Deserialize<DeepLinkInvitePayload>(payload);

        Assert.DoesNotContain("=", payload, StringComparison.Ordinal);
        Assert.Equal(new DeepLinkInvitePayload(42), deserialized);
        Assert.Equal($"https://t.me/my_bot?start={payload}", link.ToString());
    }

    [Fact]
    public void TelegramDeepLinks_DeserializeInvalidTypedPayloadFailsClearly()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            configureBot: options => options.BotUsername = "my_bot");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = Assert.Throws<ArgumentException>(() =>
            client.DeepLinks.Deserialize<DeepLinkInvitePayload>("abc"));

        Assert.Contains("deep-link payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TelegramDeepLinks_UsesReplaceablePayloadSerializer()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options =>
        {
            options.Token = "test-token";
            options.BotUsername = "my_bot";
        });
        services.AddDeepLinkPayloadSerializer<CustomDeepLinkPayloadSerializer>();

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var link = client.DeepLinks.Start(new DeepLinkInvitePayload(42));
        var payload = client.DeepLinks.Serialize(new DeepLinkInvitePayload(7));
        var deserialized = client.DeepLinks.Deserialize<DeepLinkInvitePayload>("custom-13");

        Assert.Equal("https://t.me/my_bot?start=custom-42", link.ToString());
        Assert.Equal("custom-7", payload);
        Assert.Equal(new DeepLinkInvitePayload(13), deserialized);
    }

    [Fact]
    public void TelegramDeepLinks_DoesNotSendTelegramRequest()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":true}"""));
        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.BotUsername = "my_bot");
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var link = client.DeepLinks.Start("abc");

        Assert.Equal("https://t.me/my_bot?start=abc", link.ToString());
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("John", "Doe", "John Doe")]
    [InlineData(" John ", " Doe ", "John Doe")]
    [InlineData("John", null, "John")]
    [InlineData("John", "", "John")]
    [InlineData(" ", " ", "")]
    public void MessageContext_UserFullName_UsesSender(
        string firstName,
        string? lastName,
        string expected)
    {
        using var serviceProvider = CreateTelegramServiceProvider();
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        From = new User { Id = 123, IsBot = false, FirstName = firstName, LastName = lastName }
                    }
                }));

        Assert.Equal(expected, context.GetMessageContext().User?.FullName);
    }

    [Fact]
    public void MessageContext_User_IsNullWithoutSender()
    {
        using var serviceProvider = CreateTelegramServiceProvider();
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        Assert.Null(context.GetMessageContext().User);
    }

    [Fact]
    public void CallbackQueryContext_UserFullName_UsesSender()
    {
        using var serviceProvider = CreateTelegramServiceProvider();
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "callback-id",
                        ChatInstance = "chat-instance",
                        From = new User { Id = 123, IsBot = false, FirstName = " John ", LastName = " Doe " },
                        Data = "payload"
                    }
                }));

        Assert.Equal("John Doe", context.GetCallbackQueryContext().User.FullName);
    }

    [Fact]
    public void AddLongPolling_RegistersExactlyOneUpdateSource()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddLongPolling();

        using var serviceProvider = services.BuildServiceProvider();
        Assert.Single(serviceProvider.GetServices<IUpdateSource>());
    }

    [Fact]
    public void AddLongPolling_ValidatesTelegramBackoffOptions()
    {
        var invalidCases = new (Action<TelegramLongPollingOptions> Configure, string ExpectedMessage)[]
        {
            (options => options.Backoff = null!, "backoff options must be configured"),
            (options => options.AllowedUpdates = null!, "allowed updates options must be configured"),
            (options => options.Backoff.MinDelay = TimeSpan.FromSeconds(-1), "minimum delay must not be negative"),
            (options => options.Backoff.MaxDelay = TimeSpan.FromSeconds(-1), "maximum delay must not be negative"),
            (options =>
            {
                options.Backoff.MinDelay = TimeSpan.FromSeconds(2);
                options.Backoff.MaxDelay = TimeSpan.FromSeconds(1);
            }, "maximum delay must be greater than or equal to minimum delay"),
            (options => options.Backoff.Factor = 0.9, "factor must be greater than or equal to one"),
            (options => options.Backoff.Jitter = -0.1, "jitter must be between zero and one"),
            (options => options.Backoff.Jitter = 1.1, "jitter must be between zero and one")
        };

        foreach (var (configure, expectedMessage) in invalidCases)
        {
            var services = new ServiceCollection();
            services.AddTelegramBot(options => options.Token = "test-token");

            var exception = Assert.Throws<InvalidOperationException>(() => services.AddLongPolling(configure));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AddTelegramBot_DoesNotOverwriteCustomTimeProvider()
    {
        var services = new ServiceCollection();
        var timeProvider = new RecordingTimeProvider();

        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddTelegramBot(options => options.Token = "test-token");

        using var serviceProvider = services.BuildServiceProvider();
        Assert.Same(timeProvider, serviceProvider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task AddTelegramBot_DoesNotUseGlobalJsonSerializerOptions()
    {
        var globalJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        };
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));
        var services = new ServiceCollection();

        services.AddSingleton(globalJsonOptions);
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramHttpTransport(_ => new HttpClient(handler));

        using var serviceProvider = services.BuildServiceProvider();
        var callbackData = serviceProvider.GetRequiredService<ICallbackDataSerializer>();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var serializedCallback = callbackData.Serialize(new JsonFallbackCallback("x"));
        await client.SendAsync(
            new SendMessage
            {
                ChatId = IntegerString.From(123),
                Text = "hello"
            });

        Assert.Same(globalJsonOptions, serviceProvider.GetRequiredService<JsonSerializerOptions>());
        Assert.Contains("\"someValue\"", serializedCallback);
        Assert.DoesNotContain("\"SomeValue\"", serializedCallback);
        Assert.DoesNotContain('\n', handler.Requests.Single().Body);
    }

    [Fact]
    public async Task AddTelegramJsonOptions_ReplacesTelegramJsonOptions()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramJsonOptions(options => options.WriteIndented = true);
        services.AddTelegramHttpTransport(_ => new HttpClient(handler));

        using var serviceProvider = services.BuildServiceProvider();
        var callbackData = serviceProvider.GetRequiredService<ICallbackDataSerializer>();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var serializedCallback = callbackData.Serialize(new JsonFallbackCallback("x"));
        await client.SendAsync(
            new SendMessage
            {
                ChatId = IntegerString.From(123),
                Text = "hello"
            });

        Assert.True(serviceProvider.GetRequiredService<TelegramJsonOptions>().SerializerOptions.WriteIndented);
        Assert.Contains('\n', serializedCallback);
        Assert.Contains('\n', handler.Requests.Single().Body);
    }

    [Fact]
    public async Task TelegramClient_SendAsync_UsesMethodNameAndDeserializesResult()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Equal("TeleFlow Bot", user.FirstName);
        Assert.Single(handler.Requests);
        Assert.Equal("https://api.telegram.org/bottest-token/getMe", handler.Requests[0].RequestUri);
    }

    [Fact]
    public async Task Executor_LogsSuccessfulRequestDiagnosticsWithoutSensitiveData()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler, loggerFactory: loggerFactory);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendAsync(
            new SendMessage
            {
                ChatId = IntegerString.From(123),
                Text = "hello"
            });

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 1 &&
                     entry.Message.Contains("Telegram request started", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=sendMessage", StringComparison.Ordinal) &&
                     entry.Message.Contains("attempt=1", StringComparison.Ordinal) &&
                     entry.Message.Contains("content=json", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 2 &&
                     entry.Message.Contains("Telegram request completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=sendMessage", StringComparison.Ordinal) &&
                     entry.Message.Contains("status=200", StringComparison.Ordinal) &&
                     entry.Message.Contains("request_ms=", StringComparison.Ordinal));
        AssertRequestLogsDoNotContainSensitiveData(loggerFactory);
    }

    [Fact]
    public async Task Executor_DoesNotLogSuccessfulGetUpdatesAsOrdinaryRequestDiagnostics()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":[]}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler, loggerFactory: loggerFactory);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var updates = await client.SendAsync(new GetUpdates());

        Assert.Empty(updates);
        Assert.DoesNotContain(
            loggerFactory.Entries,
            entry => entry.Message.Contains("Telegram request started", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=getUpdates", StringComparison.Ordinal));
        Assert.DoesNotContain(
            loggerFactory.Entries,
            entry => entry.Message.Contains("Telegram request completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=getUpdates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Executor_LogsRetryAfterDiagnostics()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var timeProvider = new RecordingTimeProvider();
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":1}}""",
                HttpStatusCode.TooManyRequests),
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            loggerFactory: loggerFactory,
            timeProvider: timeProvider);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Equal([TimeSpan.FromSeconds(1)], timeProvider.Delays);
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.EventId.Id == 3 &&
                     entry.Message.Contains("Telegram request throttled", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=getMe", StringComparison.Ordinal) &&
                     entry.Message.Contains("attempt=1", StringComparison.Ordinal) &&
                     entry.Message.Contains("retry_after=00:00:01", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 2 &&
                     entry.Message.Contains("Telegram request completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("attempt=2", StringComparison.Ordinal) &&
                     entry.Message.Contains("status=200", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Executor_LogsApiFailureBeforeThrowingTypedException()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":400,"description":"Bad Request"}""",
                HttpStatusCode.BadRequest));

        using var serviceProvider = CreateTelegramServiceProvider(handler, loggerFactory: loggerFactory);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramBadRequestException>(() => client.SendAsync(new GetMe()));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.EventId.Id == 4 &&
                     ReferenceEquals(entry.Exception, exception) &&
                     entry.Message.Contains("Telegram request failed", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=getMe", StringComparison.Ordinal) &&
                     entry.Message.Contains("status=400", StringComparison.Ordinal) &&
                     entry.Message.Contains("request_ms=", StringComparison.Ordinal) &&
                     entry.Message.Contains("exception_type=TeleFlow.Telegram.TelegramBadRequestException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Executor_LogsTransportFailureBeforeThrowingNetworkException()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var handler = new ThrowingHttpMessageHandler(
            new HttpRequestException("network failed", null, HttpStatusCode.BadGateway));

        using var serviceProvider = CreateTelegramServiceProvider(handler, loggerFactory: loggerFactory);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramNetworkException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(502, exception.HttpStatusCode);
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.EventId.Id == 4 &&
                     ReferenceEquals(entry.Exception, exception) &&
                     entry.Message.Contains("Telegram request failed", StringComparison.Ordinal) &&
                     entry.Message.Contains("method=getMe", StringComparison.Ordinal) &&
                     entry.Message.Contains("status=502", StringComparison.Ordinal) &&
                     entry.Message.Contains("request_ms=", StringComparison.Ordinal) &&
                     entry.Message.Contains("exception_type=TeleFlow.Telegram.TelegramNetworkException", StringComparison.Ordinal));
        AssertRequestLogsDoNotContainSensitiveData(loggerFactory);
    }

    [Fact]
    public async Task TelegramClient_GetMeAsyncExtension_UsesGeneratedMethodModel()
    {
        var client = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                GetMe => new User { Id = 42, IsBot = true, FirstName = "TeleFlow Bot" },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        var user = await client.GetMeAsync();

        Assert.Equal(42, user.Id);
        Assert.IsType<GetMe>(client.Methods.Single());
    }

    [Fact]
    public async Task AddTelegramHttpTransport_ConfiguresTelegramSpecificTransport()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        using var globalHttpClient = new HttpClient(
            new ThrowingHttpMessageHandler(new InvalidOperationException("Global HttpClient must not be used.")));
        services.AddSingleton(globalHttpClient);
        services.AddTelegramHttpTransport(_ => new HttpClient(handler));

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AddTelegramTransport_GenericOverload_ReplacesTelegramTransport()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramTransport<RecordingTelegramTransport>();

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        var transport = Assert.IsType<RecordingTelegramTransport>(
            serviceProvider.GetRequiredService<ITelegramTransport>());
        Assert.Equal(42, user.Id);
        Assert.Equal(["https://api.telegram.org/bottest-token/getMe"], transport.RequestUris);
        Assert.IsType<TelegramJsonTransportContent>(transport.Contents.Single());
    }

    [Fact]
    public async Task Executor_UsesTransportNeutralMultipartContent()
    {
        var transport = new RecordingTelegramTransport(
            new TelegramTransportResponse(200, """{"ok":true,"result":true}"""));
        var services = new ServiceCollection();

        services.AddSingleton<ITelegramTransport>(transport);
        services.AddTelegramBot(options => options.Token = "test-token");

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new MemoryStream([1, 2, 3]);

        await client.SendAsync(
            new SetChatPhoto
            {
                ChatId = IntegerString.From(123),
                Photo = InputFile.FromStream(stream, "chat-photo.png")
            });

        var content = Assert.IsType<TelegramMultipartTransportContent>(transport.Contents.Single());
        var field = Assert.Single(content.Fields);
        var file = Assert.Single(content.Files);
        Assert.Equal("chat_id", field.Name);
        Assert.Equal("123", field.Value);
        Assert.Equal("photo", file.Name);
        Assert.Equal("chat-photo.png", file.FileName);
    }

    [Fact]
    public async Task HttpClientTelegramTransport_DisposeIsIdempotent_AndBlocksSend()
    {
        using var httpClient = new HttpClient(new TrackingHttpMessageHandler());
        var transport = new HttpClientTelegramTransport(httpClient);

        transport.Dispose();
        transport.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            transport.SendAsync(new TelegramTransportRequest(
                "test",
                new Uri("https://example.com"),
                new TelegramJsonTransportContent("{}"))));
    }

    [Fact]
    public void HttpClientTelegramTransport_PublicConstructorDoesNotOwnExternalHttpClient()
    {
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var transport = new HttpClientTelegramTransport(httpClient);

        transport.Dispose();

        Assert.Equal(0, handler.DisposeCount);
    }

    [Fact]
    public void AddTelegramHttpTransport_FactoryClientIsDisposedWithServiceProvider()
    {
        var handler = new TrackingHttpMessageHandler();
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramHttpTransport(_ => new HttpClient(handler));

        var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<ITelegramTransport>();

        serviceProvider.Dispose();

        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public void AddTelegramHttpMessageHandler_DoesNotDisposeDiOwnedHandlerThroughHttpClient()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TrackingHttpMessageHandler>();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramHttpMessageHandler<TrackingHttpMessageHandler>();

        var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetRequiredService<TrackingHttpMessageHandler>();
        _ = serviceProvider.GetRequiredService<ITelegramTransport>();

        serviceProvider.Dispose();

        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public async Task Executor_SerializesGeneratedMethodsWithTelegramWireFieldNames()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendAsync(
            new SendMessage
            {
                ChatId = IntegerString.From(123),
                Text = "hello"
            });

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"chat_id\":123", body);
        Assert.Contains("\"text\":\"hello\"", body);
        Assert.DoesNotContain("ChatId", body);
        Assert.DoesNotContain("Text", body);
    }

    [Fact]
    public async Task TelegramClient_SendMessageAsyncExtension_SerializesLikeGeneratedMethodModel()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendMessageAsync(IntegerString.From(123), "hello");

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"chat_id\":123", body);
        Assert.Contains("\"text\":\"hello\"", body);
        Assert.DoesNotContain("ChatId", body);
        Assert.DoesNotContain("Text", body);
    }

    [Fact]
    public async Task TelegramClient_SendMessageAsyncExtension_AppliesBotDefaults()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options =>
            {
                options.Defaults.ParseMode = TelegramParseMode.Html;
                options.Defaults.DisableNotification = true;
                options.Defaults.ProtectContent = true;
            });
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendMessageAsync(IntegerString.From(123), "hello");

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"parse_mode\":\"HTML\"", body);
        Assert.Contains("\"disable_notification\":true", body);
        Assert.Contains("\"protect_content\":true", body);
    }

    [Fact]
    public async Task TelegramClient_SendMessageAsyncExtension_ExplicitArgumentsOverrideDefaults()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options =>
            {
                options.Defaults.ParseMode = TelegramParseMode.Html;
                options.Defaults.DisableNotification = true;
            });
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendMessageAsync(
            IntegerString.From(123),
            "hello",
            parseMode: TelegramParseMode.MarkdownV2,
            disableNotification: false);

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"parse_mode\":\"MarkdownV2\"", body);
        Assert.Contains("\"disable_notification\":false", body);
    }

    [Fact]
    public async Task TelegramClient_SendMessageAsyncExtension_ParseModeNoneSuppressesDefault()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.Defaults.ParseMode = TelegramParseMode.Html);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendMessageAsync(
            IntegerString.From(123),
            "hello",
            parseMode: TelegramParseMode.None);

        Assert.DoesNotContain("\"parse_mode\":", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task TelegramClient_SendMessageAsyncExtension_EntitiesSuppressInheritedParseMode()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.Defaults.ParseMode = TelegramParseMode.Html);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendMessageAsync(
            IntegerString.From(123),
            "hello",
            entities:
            [
                new MessageEntity
                {
                    Type = "bold",
                    Offset = 0,
                    Length = 5
                }
            ]);

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"entities\":", body);
        Assert.DoesNotContain("\"parse_mode\":", body);
    }

    [Fact]
    public async Task TelegramClient_SendAsync_RawMethodDoesNotApplyBotDefaults()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"},"text":"ok"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.Defaults.ParseMode = TelegramParseMode.Html);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendAsync(
            new SendMessage
            {
                ChatId = IntegerString.From(123),
                Text = "hello"
            });

        Assert.DoesNotContain("\"parse_mode\":", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task Executor_UsesMultipartForTopLevelInputFile()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":true}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new MemoryStream([1, 2, 3]);

        await client.SendAsync(
            new SetChatPhoto
            {
                ChatId = IntegerString.From(123),
                Photo = InputFile.FromStream(stream, "chat-photo.png")
            });

        var request = handler.Requests.Single();
        Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=chat_id", request.Body);
        Assert.Contains("123", request.Body);
        Assert.Contains("name=photo", request.Body);
        Assert.Contains("filename=chat-photo.png", request.Body);
    }

    [Fact]
    public async Task Executor_UsesMultipartForInputFileString()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"}}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new MemoryStream([1, 2, 3]);

        await client.SendAsync(
            new SendPhoto
            {
                ChatId = IntegerString.From(123),
                Photo = InputFileString.From(InputFile.FromStream(stream, "photo.png"))
            });

        var request = handler.Requests.Single();
        Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=photo", request.Body);
        Assert.Contains("filename=photo.png", request.Body);
    }

    [Fact]
    public async Task Executor_UsesJsonForUploadCapableMethodWithExistingFileId()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":true,"result":{"message_id":10,"date":0,"chat":{"id":123,"type":"private"}}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await client.SendAsync(
            new SendPhoto
            {
                ChatId = IntegerString.From(123),
                Photo = InputFileString.From("photo-file-id")
            });

        var request = handler.Requests.Single();
        Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"photo\":\"photo-file-id\"", request.Body);
    }

    [Fact]
    public async Task Executor_UsesJsonFastPathForPayloadTypesThatCannotContainInputFiles()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":true}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var result = await client.SendAsync(new JsonOnlyProbeMethod { Value = "ok" });

        var request = handler.Requests.Single();
        Assert.True(result);
        Assert.EndsWith("/jsonOnlyProbe", request.RequestUri, StringComparison.Ordinal);
        Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"value\":\"ok\"", request.Body);
        Assert.DoesNotContain("poison", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Executor_UsesAttachReferencesForNestedMediaFiles()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":[]}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new MemoryStream([1, 2, 3]);

        await client.SendAsync(
            new SendMediaGroup
            {
                ChatId = IntegerString.From(123),
                Media =
                [
                    InputMediaGroupItem.From(
                        new InputMediaPhoto
                        {
                            Media = InputFileString.From(InputFile.FromStream(stream, "album-photo.png")),
                            Caption = "album item"
                        })
                ]
            });

        var request = handler.Requests.Single();
        Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=media", request.Body);
        Assert.Contains("\"media\":\"attach://file0\"", request.Body);
        Assert.Contains("name=file0", request.Body);
        Assert.Contains("filename=album-photo.png", request.Body);
    }

    [Fact]
    public async Task TelegramClient_SendMediaGroupAsyncBuilder_SendsJsonForExistingFileIds()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":[]}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        var media = MediaGroup.Create()
            .Photo("first-file-id", caption: "first", parseMode: TelegramParseMode.Html)
            .Photo("second-file-id");

        await client.SendMediaGroupAsync(IntegerString.From(123), media);

        var request = handler.Requests.Single();
        Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/sendMediaGroup", request.RequestUri, StringComparison.Ordinal);
        Assert.Contains("\"chat_id\":123", request.Body);
        Assert.Contains("\"media\":[", request.Body);
        Assert.Contains("\"media\":\"first-file-id\"", request.Body);
        Assert.Contains("\"caption\":\"first\"", request.Body);
        Assert.Contains("\"parse_mode\":\"HTML\"", request.Body);
        Assert.Contains("\"media\":\"second-file-id\"", request.Body);
    }

    [Fact]
    public async Task TelegramClient_SendMediaGroupAsyncBuilder_AppliesTopLevelDefaults()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":[]}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options =>
            {
                options.Defaults.DisableNotification = true;
                options.Defaults.ProtectContent = true;
                options.Defaults.ParseMode = TelegramParseMode.MarkdownV2;
            });
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        var media = MediaGroup.Create()
            .Photo("first-file-id", caption: "first")
            .Photo("second-file-id");

        await client.SendMediaGroupAsync(IntegerString.From(123), media);

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"disable_notification\":true", body);
        Assert.Contains("\"protect_content\":true", body);
        Assert.DoesNotContain("\"parse_mode\":", body);
    }

    [Fact]
    public async Task TelegramClient_SendMediaGroupAsyncBuilder_UsesMultipartForInputFiles()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":[]}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new MemoryStream([1, 2, 3]);
        var media = MediaGroup.Create()
            .Photo(InputFileString.From(InputFile.FromStream(stream, "album-photo.png")), caption: "first")
            .Photo("second-file-id");

        await client.SendMediaGroupAsync(IntegerString.From(123), media);

        var request = handler.Requests.Single();
        Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=media", request.Body);
        Assert.Contains("\"media\":\"attach://file0\"", request.Body);
        Assert.Contains("name=file0", request.Body);
        Assert.Contains("filename=album-photo.png", request.Body);
    }

    [Fact]
    public async Task Executor_RetriesMultipartUpload_WithSameSeekableFileContent()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":0}}""",
                HttpStatusCode.TooManyRequests),
            CreateJsonResponse("""{"ok":true,"result":true}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("same-file-content"));

        await client.SendAsync(
            new SetChatPhoto
            {
                ChatId = IntegerString.From(123),
                Photo = InputFile.FromStream(stream, "retry-photo.txt")
            });

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(
            handler.Requests,
            request =>
            {
                Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("filename=retry-photo.txt", request.Body);
                Assert.Contains("same-file-content", request.Body);
            });
    }

    [Fact]
    public async Task Executor_RejectsNonSeekableInputFile_WithClearError()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":true}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        await using var stream = new NonSeekableReadStream(Encoding.UTF8.GetBytes("file-content"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync(
                new SetChatPhoto
                {
                    ChatId = IntegerString.From(123),
                    Photo = InputFile.FromStream(stream, "non-seekable.txt")
                }));

        Assert.Contains("not seekable", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Executor_ThrowsTelegramException_OnNonSuccessEnvelope()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""",
                HttpStatusCode.BadRequest));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramBadRequestException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(400, exception.HttpStatusCode);
        Assert.Equal(400, exception.TelegramErrorCode);
        Assert.Contains("chat not found", exception.Message);
    }

    [Theory]
    [InlineData(400, typeof(TelegramBadRequestException))]
    [InlineData(401, typeof(TelegramUnauthorizedException))]
    [InlineData(403, typeof(TelegramForbiddenException))]
    [InlineData(404, typeof(TelegramNotFoundException))]
    [InlineData(409, typeof(TelegramConflictException))]
    [InlineData(413, typeof(TelegramEntityTooLargeException))]
    [InlineData(500, typeof(TelegramServerException))]
    [InlineData(502, typeof(TelegramServerException))]
    public async Task Executor_MapsTelegramFailuresToTypedExceptions(
        int statusCode,
        Type expectedExceptionType)
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                $$"""{"ok":false,"error_code":{{statusCode}},"description":"Telegram failure"}""",
                (HttpStatusCode)statusCode));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync(expectedExceptionType, () => client.SendAsync(new GetMe()));
        var requestException = Assert.IsAssignableFrom<TelegramRequestException>(exception);

        Assert.Equal(statusCode, requestException.HttpStatusCode);
        Assert.Equal(statusCode, requestException.TelegramErrorCode);
        Assert.Equal("getMe", requestException.MethodName);
        Assert.Equal("Telegram failure", requestException.Description);
    }

    [Fact]
    public async Task Executor_MapsMigrateToChatIdToTypedException()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":400,"description":"Bad Request: group migrated","response_parameters":{"migrate_to_chat_id":-100123}}""",
                HttpStatusCode.BadRequest));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramMigrateToChatException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(-100123, exception.MigrateToChatId);
        Assert.Equal("getMe", exception.MethodName);
    }

    [Fact]
    public async Task Executor_RetriesOn429_UsingResponseParametersRetryAfter()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":0}}""",
                HttpStatusCode.TooManyRequests),
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Executor_StopsRetrying429AfterConfiguredMaximum()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":0}}""",
                HttpStatusCode.TooManyRequests),
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":0}}""",
                HttpStatusCode.TooManyRequests));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramRetryAfterException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Equal(0, exception.RetryAfterSeconds);
        Assert.Equal("getMe", exception.MethodName);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Executor_DoesNotRetry429_WhenRetryAfterPolicyIsDisabled()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":0}}""",
                HttpStatusCode.TooManyRequests));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.RetryAfter = TelegramRetryAfterPolicy.Disabled);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramRetryAfterException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Equal(0, exception.RetryAfterSeconds);
        Assert.Equal("getMe", exception.MethodName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_DoesNotRetry429_WhenRetryAfterExceedsConfiguredMaximumDelay()
    {
        var timeProvider = new RecordingTimeProvider();
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":6}}""",
                HttpStatusCode.TooManyRequests));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.RetryAfter = TelegramRetryAfterPolicy.Default with
            {
                MaxDelay = TimeSpan.FromSeconds(5)
            },
            timeProvider: timeProvider);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramRetryAfterException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Equal(6, exception.RetryAfterSeconds);
        Assert.Equal("getMe", exception.MethodName);
        Assert.Empty(timeProvider.Delays);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_UsesRetryAfterHeaderFallback()
    {
        var throttledResponse = CreateJsonResponse(
            """{"ok":false,"error_code":429,"description":"Too Many Requests"}""",
            HttpStatusCode.TooManyRequests);
        throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

        var handler = new RecordingHttpMessageHandler(
            throttledResponse,
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Executor_RetryAfterDelayUsesTimeProvider()
    {
        var timeProvider = new RecordingTimeProvider();
        var throttledResponse = CreateJsonResponse(
            """{"ok":false,"error_code":429,"description":"Too Many Requests"}""",
            HttpStatusCode.TooManyRequests);
        throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

        var handler = new RecordingHttpMessageHandler(
            throttledResponse,
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramHttpTransport(_ => new HttpClient(handler));

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Equal([TimeSpan.FromSeconds(3)], timeProvider.Delays);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Executor_RetryAfterDelayRespectsCancellation()
    {
        var timeProvider = new BlockingTimeProvider();
        var throttledResponse = CreateJsonResponse(
            """{"ok":false,"error_code":429,"description":"Too Many Requests"}""",
            HttpStatusCode.TooManyRequests);
        throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

        var handler = new RecordingHttpMessageHandler(throttledResponse);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramHttpTransport(_ => new HttpClient(handler));

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        using var cancellation = new CancellationTokenSource();

        var sendTask = client.SendAsync(new GetMe(), cancellation.Token);
        await timeProvider.WaitForDelayAsync();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_UsesRetryAfterHeaderFallback_When429ResponseIsNotJson()
    {
        var throttledResponse = CreateTextResponse("Too Many Requests", HttpStatusCode.TooManyRequests);
        throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

        var handler = new RecordingHttpMessageHandler(
            throttledResponse,
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var user = await client.SendAsync(new GetMe());

        Assert.Equal(42, user.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Executor_ThrowsRetryAfterException_When429HasNoRetryMetadata()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests"}""",
                HttpStatusCode.TooManyRequests));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramRetryAfterException>(() => client.SendAsync(new GetMe()));

        Assert.Null(exception.RetryAfterSeconds);
        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Equal(429, exception.TelegramErrorCode);
        Assert.Equal("getMe", exception.MethodName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_ThrowsRetryAfterException_WhenNonJson429HasNoRetryMetadata()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateTextResponse("Too Many Requests", HttpStatusCode.TooManyRequests));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramRetryAfterException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Equal("getMe", exception.MethodName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_InvalidNonJsonResponse_PreservesHttpStatusCode()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateTextResponse("<html>gateway failure</html>", HttpStatusCode.BadGateway));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramDecodeException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(502, exception.HttpStatusCode);
        Assert.Contains("getMe", exception.Message);
    }

    [Fact]
    public async Task Executor_DoesNotRetryNon429Failures()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":400,"description":"Bad Request"}""",
                HttpStatusCode.BadRequest));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await Assert.ThrowsAsync<TelegramBadRequestException>(() => client.SendAsync(new GetMe()));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_DoesNotRetryServerFailuresForOrdinaryRequests()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":500,"description":"Internal Server Error"}""",
                HttpStatusCode.InternalServerError),
            CreateJsonResponse("""{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await Assert.ThrowsAsync<TelegramServerException>(() => client.SendAsync(new GetMe()));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Executor_DoesNotRetryNetworkFailuresForOrdinaryRequests()
    {
        var handler = new ThrowingHttpMessageHandler(
            new HttpRequestException("network failed", null, HttpStatusCode.BadGateway));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        await Assert.ThrowsAsync<TelegramNetworkException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task Executor_ThrowsDecodeException_WhenResultJsonIsInvalid()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"id":"not-a-number","is_bot":true,"first_name":"Bot"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramDecodeException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(200, exception.HttpStatusCode);
        Assert.Equal("getMe", exception.MethodName);
    }

    [Fact]
    public async Task Executor_WrapsHttpRequestExceptionAsNetworkException()
    {
        var handler = new ThrowingHttpMessageHandler(
            new HttpRequestException("network failed", null, HttpStatusCode.BadGateway));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();

        var exception = await Assert.ThrowsAsync<TelegramNetworkException>(() => client.SendAsync(new GetMe()));

        Assert.Equal(502, exception.HttpStatusCode);
        Assert.Equal("getMe", exception.MethodName);
    }

    [Fact]
    public async Task Executor_DoesNotWrapUserCancellation()
    {
        var handler = new ThrowingHttpMessageHandler(new OperationCanceledException());
        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var client = serviceProvider.GetRequiredService<ITelegramClient>();
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(new GetMe(), cancellation.Token));
    }

    [Fact]
    public async Task LongPolling_ProcessesUpdatesSequentially_AndAdvancesOffsetAfterSuccess()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":true,"result":[{"update_id":1,"message":{"message_id":10,"date":0,"chat":{"id":100,"type":"private"},"text":"first"}}]}"""),
            CreateJsonResponse(
                """{"ok":true,"result":[{"update_id":2,"message":{"message_id":11,"date":0,"chat":{"id":100,"type":"private"},"text":"second"}}]}"""));

        var dispatcher = new RecordingDispatcher();
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddTelegramHttpTransport(_ => new HttpClient(handler));
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal([1L, 2L], dispatcher.UpdateIds);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("\"offset\":", handler.Requests[0].Body);
        Assert.Contains("\"offset\":2", handler.Requests[1].Body);
    }

    [Fact]
    public async Task LongPolling_AdvancesOffsetAfterHandledTelegramError()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":true,"result":[{"update_id":1,"message":{"message_id":10,"date":0,"chat":{"id":100,"type":"private"},"text":"/polling-handled-error"}}]}"""),
            CreateJsonResponse(
                """{"ok":true,"result":[{"update_id":2,"message":{"message_id":11,"date":0,"chat":{"id":100,"type":"private"},"text":"/polling-cancel"}}]}"""));
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddTelegramHttpTransport(_ => new HttpClient(handler));
                services.AddTelegramHandler<PollingHandledErrorThrowingHandler>();
                services.AddTelegramHandler<PollingHandledErrorHandler>();
                services.AddTelegramHandler<PollingCancellationHandler>();
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("\"offset\":", handler.Requests[0].Body);
        Assert.Contains("\"offset\":2", handler.Requests[1].Body);
    }

    [Fact]
    public async Task LongPolling_LogsLifecycleAndUpdateProcessing()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.EventId.Id == 1 &&
                     entry.Message.Contains("Starting Telegram long polling", StringComparison.Ordinal) &&
                     entry.Message.Contains("allowed_updates=unset", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.EventId.Id == 2 &&
                     entry.Message.Contains("Telegram long polling connected", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 3 &&
                     entry.Message.Contains("Telegram update received", StringComparison.Ordinal) &&
                     entry.Message.Contains("update_id=1", StringComparison.Ordinal) &&
                     entry.Message.Contains("type=message", StringComparison.Ordinal) &&
                     entry.Message.Contains("batch_index=1/1", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 5 &&
                     entry.Message.Contains("Telegram update processed", StringComparison.Ordinal) &&
                     entry.Message.Contains("total_ms=", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.EventId.Id == 6 &&
                     entry.Message.Contains("Telegram long polling stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LongPolling_BackoffRetriesTransientGetUpdatesFailures_AndResetsAfterSuccess()
    {
        var timeProvider = new RecordingTimeProvider();
        var telegramClient = new SequencedTelegramClient(
            new TelegramNetworkException("network failed", methodName: "getUpdates"),
            new TelegramServerException("server failed", methodName: "getUpdates", httpStatusCode: 502),
            Array.Empty<Update>(),
            new TelegramDecodeException("decode failed", methodName: "getUpdates"),
            new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(options =>
            {
                options.Backoff.MinDelay = TimeSpan.FromSeconds(1);
                options.Backoff.MaxDelay = TimeSpan.FromSeconds(10);
                options.Backoff.Factor = 2;
                options.Backoff.Jitter = 0;
            }),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)],
            timeProvider.Delays);
        Assert.Equal([1L], dispatcher.UpdateIds);
        Assert.Equal(5, telegramClient.GetUpdatesRequests.Count);
    }

    [Fact]
    public async Task LongPolling_UsesRetryAfterDelayForThrottledGetUpdates()
    {
        var timeProvider = new RecordingTimeProvider();
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":false,"error_code":429,"description":"Too Many Requests","response_parameters":{"retry_after":3}}""",
                HttpStatusCode.TooManyRequests),
            CreateJsonResponse(
                """{"ok":true,"result":[{"update_id":1,"message":{"message_id":10,"date":0,"chat":{"id":100,"type":"private"},"text":"first"}}]}"""));
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
                services.AddTelegramHttpTransport(_ => new HttpClient(handler));
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(options =>
            {
                options.Backoff.MinDelay = TimeSpan.FromSeconds(1);
                options.Backoff.MaxDelay = TimeSpan.FromSeconds(1);
                options.Backoff.Jitter = 0;
            }),
            cancellation,
            configureBot: options => options.RetryAfter = TelegramRetryAfterPolicy.Disabled);

        await application.RunAsync(cancellation.Token);

        Assert.Equal([TimeSpan.FromSeconds(3)], timeProvider.Delays);
        Assert.Equal([1L], dispatcher.UpdateIds);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task LongPolling_DoesNotAdvanceOffsetAfterFailedGetUpdates()
    {
        var timeProvider = new RecordingTimeProvider();
        var telegramClient = new SequencedTelegramClient(
            new List<Update> { CreateMessageUpdate(1) },
            new TelegramNetworkException("network failed", methodName: "getUpdates"),
            new List<Update> { CreateMessageUpdate(2) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 2);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(options =>
            {
                options.Backoff.MinDelay = TimeSpan.FromSeconds(1);
                options.Backoff.MaxDelay = TimeSpan.FromSeconds(1);
                options.Backoff.Jitter = 0;
            }),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal([1L, 2L], dispatcher.UpdateIds);
        Assert.Equal([null, 2L, 2L], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());
        Assert.Equal([TimeSpan.FromSeconds(1)], timeProvider.Delays);
    }

    [Fact]
    public async Task LongPolling_DefaultAllowedUpdatesAuto_InfersMessageUpdatesFromHandlers()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTelegramHandler<AllowedUpdatesMessageHandler>();
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(["message"], telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public async Task LongPolling_DefaultAllowedUpdatesAuto_InfersCallbackUpdatesFromHandlers()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTelegramHandler<AllowedUpdatesCallbackHandler>();
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(["callback_query"], telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public async Task LongPolling_DefaultAllowedUpdatesAuto_InfersChatMemberUpdatesFromHandlers()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTelegramHandler<AllowedUpdatesChatMemberHandler>();
                services.AddTelegramHandler<AllowedUpdatesMyChatMemberHandler>();
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(["my_chat_member", "chat_member"], telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public async Task LongPolling_ExplicitAllowedUpdatesOnly_DisablesAutoInference()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTelegramHandler<AllowedUpdatesMessageHandler>();
            },
            services => services.AddLongPolling(options =>
                options.AllowedUpdates = TelegramAllowedUpdates.Only(
                    TelegramUpdateType.CallbackQuery,
                    TelegramUpdateType.Custom("custom_update"))),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(["callback_query", "custom_update"], telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public async Task LongPolling_AllowedUpdatesAll_UsesAllKnownUpdateTypes()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(options =>
                options.AllowedUpdates = TelegramAllowedUpdates.All),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Equal(
            TelegramUpdateType.AllKnown.Select(static updateType => updateType.Value).ToArray(),
            telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public async Task LongPolling_AllowedUpdatesAutoWithoutHandlerMetadata_LeavesAllowedUpdatesUnset()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var dispatcher = new RecordingDispatcher(cancelAfter: 1);
        using var cancellation = new CancellationTokenSource();

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddSingleton<ITelegramClient>(telegramClient);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            },
            services => services.AddLongPolling(),
            cancellation);

        await application.RunAsync(cancellation.Token);

        Assert.Null(telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public void TelegramAllowedUpdates_RejectsInvalidExplicitValues()
    {
        Assert.Throws<ArgumentException>(() => TelegramUpdateType.Custom(" "));
        Assert.Throws<ArgumentException>(() => TelegramAllowedUpdates.Only());
        Assert.Throws<ArgumentException>(() => TelegramAllowedUpdates.Only(TelegramUpdateType.Message, TelegramUpdateType.Message));
    }

    [Fact]
    public async Task LongPolling_RuntimeException_BubblesOutUnchanged()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse(
                """{"ok":true,"result":[{"update_id":1,"message":{"message_id":10,"date":0,"chat":{"id":100,"type":"private"},"text":"boom"}}]}"""));
        var expected = new InvalidOperationException("dispatcher failed");

        var application = CreateTelegramApplication(
            services =>
            {
                services.AddTelegramHttpTransport(_ => new HttpClient(handler));
                services.AddSingleton<IUpdateDispatcher>(new ThrowingDispatcher(expected));
            },
            services => services.AddLongPolling());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task UpdateContextExtensions_ExposeTelegramWrappers_AndKeepRawDtoSeparate()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method =>
            {
                return method switch
                {
                    SendMessage => new Message
                    {
                        MessageId = 20,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "reply"
                    },
                    _ => throw new InvalidOperationException("Unexpected method.")
                };
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "hello"
                    }
                }));

        var telegramContext = context.GetTelegramContext();
        var messageContext = context.GetMessageContext();

        Assert.True(context.TryGetTelegramUpdate(out var update));
        Assert.Same(update, telegramContext.Update);
        Assert.Equal(10, messageContext.TelegramMessage.MessageId);
        Assert.Equal("hello", messageContext.TelegramMessage.Text);
        Assert.IsType<MessageActions>(messageContext.Message);

        await messageContext.Message.AnswerAsync("answer");

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Equal(100L, sendMessage.ChatId.Integer);
        Assert.Null(sendMessage.ReplyParameters);
    }

    [Fact]
    public async Task TelegramContextBot_SendAsync_UsesUpdateCancellationWhenTokenIsDefault()
    {
        using var updateCancellation = new CancellationTokenSource();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => new User { Id = 42, IsBot = true, FirstName = "Bot" }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider, updateCancellation.Token);

        await context.GetTelegramContext().Bot.SendAsync(new GetMe());

        Assert.Equal(updateCancellation.Token, fakeClient.CancellationTokens.Single());
    }

    [Fact]
    public async Task TelegramContextBot_SendAsync_ExplicitTokenOverridesUpdateCancellation()
    {
        using var updateCancellation = new CancellationTokenSource();
        using var explicitCancellation = new CancellationTokenSource();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => new User { Id = 42, IsBot = true, FirstName = "Bot" }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider, updateCancellation.Token);

        await context.GetTelegramContext().Bot.SendAsync(new GetMe(), explicitCancellation.Token);

        Assert.Equal(explicitCancellation.Token, fakeClient.CancellationTokens.Single());
    }

    [Fact]
    public async Task TelegramContextBot_SendAsync_CancellationTokenNoneFallsBackToUpdateCancellation()
    {
        using var updateCancellation = new CancellationTokenSource();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => new User { Id = 42, IsBot = true, FirstName = "Bot" }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider, updateCancellation.Token);

        await context.GetTelegramContext().Bot.SendAsync(new GetMe(), CancellationToken.None);

        Assert.Equal(updateCancellation.Token, fakeClient.CancellationTokens.Single());
    }

    [Fact]
    public async Task TelegramContextBot_GeneratedExtension_UsesUpdateCancellation()
    {
        using var updateCancellation = new CancellationTokenSource();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                SendMessage => new Message
                {
                    MessageId = 20,
                    Date = 0,
                    Chat = new Chat { Id = 100, Type = "private" },
                    Text = "done"
                },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider, updateCancellation.Token);

        await context.GetTelegramContext().Bot.SendMessageAsync(
            IntegerString.From(100),
            "done");

        Assert.Equal(updateCancellation.Token, fakeClient.CancellationTokens.Single());
    }

    [Fact]
    public void TelegramContextBot_ForwardsDefaults()
    {
        var fakeClient = new RecordingTelegramClient();
        fakeClient.Defaults.ParseMode = TelegramParseMode.Html;
        fakeClient.Defaults.DisableNotification = true;

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var contextBot = context.GetTelegramContext().Bot;

        Assert.Same(fakeClient.Defaults, contextBot.Defaults);
        Assert.Equal(TelegramParseMode.Html, contextBot.Defaults.ParseMode);
        Assert.True(contextBot.Defaults.DisableNotification);
    }

    [Fact]
    public async Task TelegramContextBot_WrapsCustomTelegramClient()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => new User { Id = 42, IsBot = true, FirstName = "Bot" }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);
        var contextBot = context.GetTelegramContext().Bot;

        await contextBot.SendAsync(new GetMe());

        Assert.NotSame(fakeClient, contextBot);
        Assert.IsType<GetMe>(fakeClient.Methods.Single());
    }

    [Fact]
    public void TelegramContextBot_ForwardsDeepLinks()
    {
        var fakeClient = new RecordingTelegramClient();
        fakeClient.DeepLinksOverride = new TelegramDeepLinks(
            "context_bot",
            new Base64UrlJsonDeepLinkPayloadSerializer());

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var contextBot = context.GetTelegramContext().Bot;

        Assert.Same(fakeClient.DeepLinks, contextBot.DeepLinks);
        Assert.Equal("https://t.me/context_bot?start=abc", contextBot.DeepLinks.Start("abc").ToString());
    }

    [Fact]
    public async Task MessageReplyAsync_SendsReplyToCurrentMessage()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method =>
            {
                return method switch
                {
                    SendMessage => new Message
                    {
                        MessageId = 20,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "reply"
                    },
                    _ => throw new InvalidOperationException("Unexpected method.")
                };
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "hello"
                    }
                }));

        await context.GetMessageContext().Message.ReplyAsync("reply");

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Equal(100L, sendMessage.ChatId.Integer);
        Assert.Equal(10L, sendMessage.ReplyParameters?.MessageId);
    }

    [Fact]
    public async Task MessageAnswerAsync_CanSendInlineKeyboardWithTypedCallbackData()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                SendMessage => new Message
                {
                    MessageId = 20,
                    Date = 0,
                    Chat = new Chat { Id = 100, Type = "private" },
                    Text = "reply"
                },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "hello"
                    }
                }));

        var messageContext = context.GetMessageContext();
        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Delete", new KeyboardDeleteCallback(42))
            .Url("Open", "https://example.com")
            .Build();

        await messageContext.Message.AnswerAsync("choose", keyboard);

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Null(sendMessage.ReplyParameters);
        Assert.NotNull(sendMessage.ReplyMarkup);
        var markup = sendMessage.ReplyMarkup;
        Assert.True(markup.TryGetInlineKeyboardMarkup(out var inlineKeyboard));
        Assert.NotNull(inlineKeyboard);
        Assert.Equal("del:42", inlineKeyboard.InlineKeyboard[0][0].CallbackData);
        Assert.Equal("https://example.com", inlineKeyboard.InlineKeyboard[0][1].Url);
    }

    [Fact]
    public async Task MessageAnswerAsync_CanSendReplyKeyboard()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                SendMessage => new Message
                {
                    MessageId = 20,
                    Date = 0,
                    Chat = new Chat { Id = 100, Type = "private" },
                    Text = "choose"
                },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);
        var keyboard = ReplyKeyboard.Create()
            .Button("Yes")
            .Button("No")
            .Resize();

        await context.GetMessageContext().Message.AnswerAsync("choose", keyboard);

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Null(sendMessage.ReplyParameters);
        Assert.NotNull(sendMessage.ReplyMarkup);
        Assert.True(sendMessage.ReplyMarkup.TryGetReplyKeyboardMarkup(out var replyKeyboard));
        Assert.True(replyKeyboard?.ResizeKeyboard);
        Assert.Equal(["Yes", "No"], replyKeyboard!.Keyboard[0].Select(static button => button.Text));
    }

    [Fact]
    public async Task MessageReplyAsync_CanSendKeyboardRemove()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                SendMessage => new Message
                {
                    MessageId = 20,
                    Date = 0,
                    Chat = new Chat { Id = 100, Type = "private" },
                    Text = "done"
                },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        await context.GetMessageContext().Message.ReplyAsync("done", KeyboardRemove.Create());

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Equal(10L, sendMessage.ReplyParameters?.MessageId);
        Assert.NotNull(sendMessage.ReplyMarkup);
        Assert.True(sendMessage.ReplyMarkup.TryGetReplyKeyboardRemove(out var keyboardRemove));
        Assert.True(keyboardRemove?.RemoveKeyboard);
    }

    [Fact]
    public async Task MessageAnswerAsync_CanSendForceReply()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                SendMessage => new Message
                {
                    MessageId = 20,
                    Date = 0,
                    Chat = new Chat { Id = 100, Type = "private" },
                    Text = "name"
                },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        await context.GetMessageContext().Message.AnswerAsync(
            "name",
            ForceReplyBuilder.Create().Placeholder("Name"));

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Null(sendMessage.ReplyParameters);
        Assert.NotNull(sendMessage.ReplyMarkup);
        Assert.True(sendMessage.ReplyMarkup.TryGetForceReply(out var forceReply));
        Assert.True(forceReply?.ForceReplyValue);
        Assert.Equal("Name", forceReply?.InputFieldPlaceholder);
    }

    [Fact]
    public async Task MessageAnswerAsync_UsesGeneratedClientDefaults()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":20,"date":0,"chat":{"id":100,"type":"private"},"text":"answer"}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.Defaults.ParseMode = TelegramParseMode.MarkdownV2);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "hello"
                    }
                }));

        await context.GetMessageContext().Message.AnswerAsync("answer");

        Assert.Contains("\"parse_mode\":\"MarkdownV2\"", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task MessageReplyTextAsync_RemainsObsoleteAliasForReplyAsync()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                SendMessage => new Message
                {
                    MessageId = 20,
                    Date = 0,
                    Chat = new Chat { Id = 100, Type = "private" },
                    Text = "reply"
                },
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "hello"
                    }
                }));

#pragma warning disable CS0618
        await context.GetMessageContext().Message.ReplyTextAsync("reply");
#pragma warning restore CS0618

        var sendMessage = Assert.IsType<SendMessage>(fakeClient.Methods.Single());
        Assert.Equal(10L, sendMessage.ReplyParameters?.MessageId);
    }

    [Fact]
    public void MessageActions_ExposesCuratedMediaHelpers()
    {
        var methodNames = typeof(MessageActions)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        var expectedNames = new[]
        {
            "AnswerPhotoAsync",
            "ReplyPhotoAsync",
            "AnswerDocumentAsync",
            "ReplyDocumentAsync",
            "AnswerVideoAsync",
            "ReplyVideoAsync",
            "AnswerAnimationAsync",
            "ReplyAnimationAsync",
            "AnswerAudioAsync",
            "ReplyAudioAsync",
            "AnswerVoiceAsync",
            "ReplyVoiceAsync",
            "AnswerStickerAsync",
            "ReplyStickerAsync",
            "AnswerVideoNoteAsync",
            "ReplyVideoNoteAsync"
        };

        foreach (var expectedName in expectedNames)
        {
            Assert.Contains(expectedName, methodNames);
        }
    }

    [Fact]
    public async Task MessageAnswerPhotoAsync_SendsToCurrentChatWithoutReply()
    {
        var fakeClient = CreateRecordingMessageClient<SendPhoto>();
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        await context.GetMessageContext().Message.AnswerPhotoAsync(
            InputFileString.From("photo-file-id"),
            caption: "cat",
            cancellationToken: CancellationToken.None);

        var sendPhoto = Assert.IsType<SendPhoto>(fakeClient.Methods.Single());
        Assert.Equal(100L, sendPhoto.ChatId.Integer);
        Assert.Equal("photo-file-id", sendPhoto.Photo.String);
        Assert.Equal("cat", sendPhoto.Caption);
        Assert.Null(sendPhoto.ReplyParameters);
    }

    [Fact]
    public async Task MessageReplyPhotoAsync_SendsReplyToCurrentMessage()
    {
        var fakeClient = CreateRecordingMessageClient<SendPhoto>();
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        await context.GetMessageContext().Message.ReplyPhotoAsync(
            InputFileString.From("photo-file-id"),
            caption: "cat");

        var sendPhoto = Assert.IsType<SendPhoto>(fakeClient.Methods.Single());
        Assert.Equal(100L, sendPhoto.ChatId.Integer);
        Assert.Equal(10L, sendPhoto.ReplyParameters?.MessageId);
    }

    [Fact]
    public async Task MessageAnswerPhotoAsync_CanSendInlineKeyboardWithTypedCallbackData()
    {
        var fakeClient = CreateRecordingMessageClient<SendPhoto>();
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var messageContext = context.GetMessageContext();
        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Delete", new KeyboardDeleteCallback(42))
            .Build();

        await messageContext.Message.AnswerPhotoAsync(
            InputFileString.From("photo-file-id"),
            ReplyMarkup.From(keyboard),
            caption: "choose");

        var sendPhoto = Assert.IsType<SendPhoto>(fakeClient.Methods.Single());
        Assert.NotNull(sendPhoto.ReplyMarkup);
        Assert.True(sendPhoto.ReplyMarkup.TryGetInlineKeyboardMarkup(out var inlineKeyboard));
        Assert.NotNull(inlineKeyboard);
        Assert.Equal("del:42", inlineKeyboard.InlineKeyboard[0][0].CallbackData);
    }

    [Fact]
    public async Task MessageAnswerPhotoAsync_CanPassRawReplyMarkup()
    {
        var fakeClient = CreateRecordingMessageClient<SendPhoto>();
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);
        var replyMarkup = ReplyMarkup.From(new ForceReply { ForceReplyValue = true });

        await context.GetMessageContext().Message.AnswerPhotoAsync(
            InputFileString.From("photo-file-id"),
            replyMarkup,
            caption: "force");

        var sendPhoto = Assert.IsType<SendPhoto>(fakeClient.Methods.Single());
        Assert.NotNull(sendPhoto.ReplyMarkup);
        Assert.True(sendPhoto.ReplyMarkup.TryGetForceReply(out var forceReply));
        Assert.True(forceReply?.ForceReplyValue);
    }

    [Fact]
    public async Task MessageAnswerPhotoAsync_UsesGeneratedClientDefaults()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":20,"date":0,"chat":{"id":100,"type":"private"},"photo":[]}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(
            handler,
            configureBot: options => options.Defaults.ParseMode = TelegramParseMode.MarkdownV2);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        await context.GetMessageContext().Message.AnswerPhotoAsync(
            InputFileString.From("photo-file-id"),
            caption: "cat");

        Assert.Contains("\"parse_mode\":\"MarkdownV2\"", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task MessageAnswerPhotoAsync_WithInputFile_UsesMultipartTransport()
    {
        var handler = new RecordingHttpMessageHandler(
            CreateJsonResponse("""{"ok":true,"result":{"message_id":20,"date":0,"chat":{"id":100,"type":"private"},"photo":[]}}"""));

        using var serviceProvider = CreateTelegramServiceProvider(handler);
        var context = CreateScopedMessageUpdateContext(serviceProvider);
        await using var stream = new MemoryStream([1, 2, 3]);

        await context.GetMessageContext().Message.AnswerPhotoAsync(
            InputFileString.From(InputFile.FromStream(stream, "photo.png")),
            caption: "cat");

        var request = handler.Requests.Single();
        Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=chat_id", request.Body);
        Assert.Contains("100", request.Body);
        Assert.Contains("name=photo", request.Body);
        Assert.Contains("filename=photo.png", request.Body);
    }

    [Fact]
    public async Task MessageReplyStickerAsync_SendsReplyToCurrentMessage()
    {
        var fakeClient = CreateRecordingMessageClient<SendSticker>();
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        await context.GetMessageContext().Message.ReplyStickerAsync(InputFileString.From("sticker-file-id"));

        var sendSticker = Assert.IsType<SendSticker>(fakeClient.Methods.Single());
        Assert.Equal(100L, sendSticker.ChatId.Integer);
        Assert.Equal("sticker-file-id", sendSticker.Sticker.String);
        Assert.Equal(10L, sendSticker.ReplyParameters?.MessageId);
    }

    [Fact]
    public void ChatAction_ExposesTelegramWireValues()
    {
        Assert.Equal("typing", ChatAction.Typing.Value);
        Assert.Equal("upload_photo", ChatAction.UploadPhoto.Value);
        Assert.Equal("record_video", ChatAction.RecordVideo.Value);
        Assert.Equal("upload_video", ChatAction.UploadVideo.Value);
        Assert.Equal("record_voice", ChatAction.RecordVoice.Value);
        Assert.Equal("upload_voice", ChatAction.UploadVoice.Value);
        Assert.Equal("upload_document", ChatAction.UploadDocument.Value);
        Assert.Equal("choose_sticker", ChatAction.ChooseSticker.Value);
        Assert.Equal("find_location", ChatAction.FindLocation.Value);
        Assert.Equal("record_video_note", ChatAction.RecordVideoNote.Value);
        Assert.Equal("upload_video_note", ChatAction.UploadVideoNote.Value);
        Assert.Equal("custom_action", ChatAction.Custom("custom_action").Value);
        Assert.Equal("custom_action", ChatAction.Custom(" custom_action ").Value);

        Assert.Throws<ArgumentException>(() => ChatAction.Custom(" "));
    }

    [Fact]
    public async Task MessageChatActionAsync_RejectsDefaultAction()
    {
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: new RecordingTelegramClient());
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await context.GetMessageContext().Chat.ActionAsync(default));

        Assert.Contains("action", exception.Message);
    }

    [Fact]
    public async Task MessageChatActionAsync_SendsImmediateActionToCurrentChat()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var lease = await context.GetMessageContext().Chat.ActionAsync(ChatAction.Typing);
        await lease.DisposeAsync();

        var action = Assert.IsType<SendChatAction>(fakeClient.Methods.Single());
        Assert.Equal(100L, action.ChatId.Integer);
        Assert.Equal("typing", action.Action);
        Assert.Null(action.MessageThreadId);
    }

    [Fact]
    public async Task MessageChatActionAsync_UsesMessageThreadWhenPresent()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        MessageThreadId = 42,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "supergroup" },
                        Text = "hello"
                    }
                }));

        var lease = await context.GetMessageContext().Chat.ActionAsync(ChatAction.UploadDocument);
        await lease.DisposeAsync();

        var action = Assert.IsType<SendChatAction>(fakeClient.Methods.Single());
        Assert.Equal(42L, action.MessageThreadId);
        Assert.Equal("upload_document", action.Action);
    }

    [Fact]
    public async Task ChatActionLease_RepeatsUntilDisposed()
    {
        var timeProvider = new ManualTimeProvider();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(
            clientOverride: fakeClient,
            timeProvider: timeProvider);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var lease = await context.GetMessageContext().Chat.ActionAsync(ChatAction.Typing);

        Assert.Single(fakeClient.Methods);
        Assert.True(timeProvider.FireNextTimer());
        await WaitUntilAsync(() => fakeClient.Methods.Count == 2);

        await lease.DisposeAsync();

        Assert.Equal(2, fakeClient.Methods.Count);
        Assert.All(fakeClient.Methods, method => Assert.IsType<SendChatAction>(method));
    }

    [Fact]
    public async Task ChatActionLease_DisposeStopsRepeatLoop()
    {
        var timeProvider = new ManualTimeProvider();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(
            clientOverride: fakeClient,
            timeProvider: timeProvider);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var lease = await context.GetMessageContext().Chat.ActionAsync(ChatAction.Typing);
        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.False(timeProvider.FireNextTimer());
        Assert.Single(fakeClient.Methods);
    }

    [Fact]
    public async Task ChatActionLease_CancellationStopsRepeatLoop()
    {
        var timeProvider = new ManualTimeProvider();
        using var cancellation = new CancellationTokenSource();
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(
            clientOverride: fakeClient,
            timeProvider: timeProvider);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var lease = await context.GetMessageContext().Chat.ActionAsync(ChatAction.Typing, cancellation.Token);
        await cancellation.CancelAsync();
        await lease.DisposeAsync();

        Assert.False(timeProvider.FireNextTimer());
        Assert.Single(fakeClient.Methods);
    }

    [Fact]
    public async Task ChatActionAsync_ImmediateSendFailureBubblesUnchanged()
    {
        var expected = new InvalidOperationException("send failed");
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => throw expected
        };
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.GetMessageContext().Chat.ActionAsync(ChatAction.Typing));

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task ChatActionLease_RepeatFailureIsObservableOnDispose()
    {
        var timeProvider = new ManualTimeProvider();
        var expected = new InvalidOperationException("repeat failed");
        var attempt = 0;
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method =>
            {
                if (method is not SendChatAction)
                {
                    throw new InvalidOperationException("Unexpected method.");
                }

                attempt++;
                if (attempt == 2)
                {
                    throw expected;
                }

                return true;
            }
        };
        using var serviceProvider = CreateTelegramServiceProvider(
            clientOverride: fakeClient,
            timeProvider: timeProvider);
        var context = CreateScopedMessageUpdateContext(serviceProvider);

        var lease = await context.GetMessageContext().Chat.ActionAsync(ChatAction.Typing);
        Assert.True(timeProvider.FireNextTimer());
        await WaitUntilAsync(() => fakeClient.Methods.Count == 2);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await lease.DisposeAsync());

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task CallbackChatActionAsync_UsesAccessibleCallbackMessageChat()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb",
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        Message = MaybeInaccessibleMessage.From(
                            new Message
                            {
                                MessageId = 99,
                                MessageThreadId = 77,
                                Date = 1,
                                Chat = new Chat { Id = 200, Type = "supergroup" }
                            }),
                        ChatInstance = "chat-instance"
                    }
                }));

        var lease = await context.GetCallbackQueryContext().Chat.ActionAsync(ChatAction.UploadPhoto);
        await lease.DisposeAsync();

        var action = Assert.IsType<SendChatAction>(fakeClient.Methods.Single());
        Assert.Equal(200L, action.ChatId.Integer);
        Assert.Equal(77L, action.MessageThreadId);
        Assert.Equal("upload_photo", action.Action);
    }

    [Fact]
    public async Task CallbackChatActionAsync_FailsClearlyWithoutChatTarget()
    {
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: new RecordingTelegramClient());
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb",
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        InlineMessageId = "inline-only",
                        ChatInstance = "chat-instance"
                    }
                }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.GetCallbackQueryContext().Chat.ActionAsync(ChatAction.Typing));

        Assert.Contains("does not expose a Telegram chat target", exception.Message);
    }

    [Fact]
    public async Task ChatMemberChatActionAsync_UsesChatMemberUpdateChat()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method is SendChatAction
                ? true
                : throw new InvalidOperationException("Unexpected method.")
        };
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = CreateChatMemberUpdateContext(serviceProvider);

        var lease = await context.GetChatMemberUpdatedContext().Chat.ActionAsync(ChatAction.Typing);
        await lease.DisposeAsync();

        var action = Assert.IsType<SendChatAction>(fakeClient.Methods.Single());
        Assert.Equal(300L, action.ChatId.Integer);
        Assert.Equal("typing", action.Action);
    }

    [Fact]
    public void GetMessageContext_AndGetCallbackQueryContext_FailClearlyOnMismatchedUpdates()
    {
        using var serviceProvider = CreateTelegramServiceProvider();
        var messageContext = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" }
                    }
                }));

        var callbackContext = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 2,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb",
                        From = new User
                        {
                            Id = 5,
                            IsBot = false,
                            FirstName = "User"
                        },
                        ChatInstance = "chat-instance"
                    }
                }));

        Assert.Throws<InvalidOperationException>(() => messageContext.GetCallbackQueryContext());
        Assert.Throws<InvalidOperationException>(() => callbackContext.GetMessageContext());
    }

    [Fact]
    public async Task CallbackHelper_SupportsChatMessageAndInlineMessageVariants()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method =>
            {
                if (method is AnswerCallbackQuery)
                {
                    return true;
                }

                if (method is EditMessageText)
                {
                    return MessageBoolean.From(true);
                }

                throw new InvalidOperationException("Unexpected method.");
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);

        var chatCallbackContext = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb-1",
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        Message = MaybeInaccessibleMessage.From(
                            new Message
                            {
                                MessageId = 99,
                                Date = 1,
                                Chat = new Chat { Id = 100, Type = "private" }
                            }),
                        ChatInstance = "chat-instance-1"
                    }
                }));

        var inlineCallbackContext = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 2,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb-2",
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        InlineMessageId = "inline-42",
                        ChatInstance = "chat-instance-2"
                    }
                }));

        var chatContext = chatCallbackContext.GetCallbackQueryContext();
        var inlineContext = inlineCallbackContext.GetCallbackQueryContext();

        await chatContext.Callback.AnswerAsync("done", CancellationToken.None);
        await chatContext.Callback.EditTextAsync("edited");
        await inlineContext.Callback.EditTextAsync("inline");

        Assert.Collection(
            fakeClient.Methods,
            method =>
            {
                var answer = Assert.IsType<AnswerCallbackQuery>(method);
                Assert.Equal("cb-1", answer.CallbackQueryId);
            },
            method =>
            {
                var edit = Assert.IsType<EditMessageText>(method);
                Assert.Equal(100L, edit.ChatId!.Integer);
                Assert.Equal(99L, edit.MessageId);
            },
            method =>
            {
                var edit = Assert.IsType<EditMessageText>(method);
                Assert.Equal("inline-42", edit.InlineMessageId);
                Assert.Null(edit.ChatId);
                Assert.Null(edit.MessageId);
            });
    }

    [Fact]
    public async Task CallbackEditText_CanSendInlineKeyboardWithTypedCallbackData()
    {
        var fakeClient = new RecordingTelegramClient
        {
            Handler = method => method switch
            {
                EditMessageText => MessageBoolean.From(true),
                _ => throw new InvalidOperationException("Unexpected method.")
            }
        };

        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: fakeClient);
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb",
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        InlineMessageId = "inline-42",
                        ChatInstance = "chat-instance"
                    }
                }));

        var callbackContext = context.GetCallbackQueryContext();
        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Next", new KeyboardDeleteCallback(99))
            .Build();

        await callbackContext.Callback.EditTextAsync("edited", keyboard);

        var edit = Assert.IsType<EditMessageText>(fakeClient.Methods.Single());
        Assert.Equal("inline-42", edit.InlineMessageId);
        Assert.Equal("del:99", edit.ReplyMarkup?.InlineKeyboard[0][0].CallbackData);
    }

    [Fact]
    public async Task CallbackDeleteMessage_FailsClearlyForInlineOnlyCallback()
    {
        using var serviceProvider = CreateTelegramServiceProvider(clientOverride: new RecordingTelegramClient());
        var context = new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb",
                        From = new User { Id = 5, IsBot = false, FirstName = "User" },
                        InlineMessageId = "inline-only",
                        ChatInstance = "chat-instance"
                    }
                }));

        var callbackContext = context.GetCallbackQueryContext();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => callbackContext.Callback.DeleteMessageAsync());

        Assert.Contains("deletable chat message target", exception.Message);
    }

    [Fact]
    public void TelegramUpdatePayload_NoLongerCarriesRawCallbackPayloadState()
    {
        var properties = typeof(TelegramUpdatePayload)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(static property => property.Name)
            .ToArray();

        Assert.DoesNotContain("RawJson", properties);
    }

    private static ServiceProvider CreateTelegramServiceProvider(
        HttpMessageHandler? handler = null,
        RecordingTelegramClient? clientOverride = null,
        Action<TelegramBotOptions>? configureBot = null,
        ILoggerFactory? loggerFactory = null,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        if (loggerFactory is not null)
        {
            services.AddSingleton(loggerFactory);
        }

        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddTelegramBot(options =>
        {
            options.Token = "test-token";
            configureBot?.Invoke(options);
        });

        if (handler is not null)
        {
            services.AddTelegramHttpTransport(_ => new HttpClient(handler));
        }

        if (clientOverride is not null)
        {
            services.AddSingleton<ITelegramClient>(clientOverride);
        }

        return services.BuildServiceProvider();
    }

    private static void AssertRequestLogsDoNotContainSensitiveData(RecordingLoggerFactory loggerFactory)
    {
        var messages = loggerFactory.Entries.Select(static entry => entry.Message).ToArray();

        Assert.DoesNotContain(messages, static message => message.Contains("test-token", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, static message => message.Contains("/bottest-token/", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, static message => message.Contains("hello", StringComparison.Ordinal));
    }

    private static UpdateContext CreateScopedMessageUpdateContext(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        return new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    Message = new Message
                    {
                        MessageId = 10,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" },
                        Text = "hello"
                    }
                }),
            cancellationToken);
    }

    private static UpdateContext CreateChatMemberUpdateContext(IServiceProvider serviceProvider)
    {
        return new UpdateContext(
            serviceProvider,
            new TelegramUpdatePayload(
                new Update
                {
                    UpdateId = 1,
                    ChatMember = new ChatMemberUpdated
                    {
                        Chat = new Chat { Id = 300, Type = "supergroup" },
                        From = new User { Id = 42, IsBot = false, FirstName = "Actor" },
                        Date = 0,
                        OldChatMember = ChatMember.From(new ChatMemberLeft
                        {
                            User = new User { Id = 5, IsBot = false, FirstName = "User" }
                        }),
                        NewChatMember = ChatMember.From(new ChatMemberMember
                        {
                            User = new User { Id = 5, IsBot = false, FirstName = "User" }
                        })
                    }
                }));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected condition was not met before the timeout.");
    }

    private static RecordingTelegramClient CreateRecordingMessageClient<TMethod>()
    {
        return new RecordingTelegramClient
        {
            Handler = method =>
            {
                if (method is TMethod)
                {
                    return new Message
                    {
                        MessageId = 20,
                        Date = 0,
                        Chat = new Chat { Id = 100, Type = "private" }
                    };
                }

                throw new InvalidOperationException($"Unexpected method '{method.GetType().Name}'.");
            }
        };
    }

    private static ITeleFlowApplication CreateTelegramApplication(
        Action<IServiceCollection> configureServices,
        Action<IServiceCollection> configureTelegram,
        CancellationTokenSource? cancellation = null,
        Action<TelegramBotOptions>? configureBot = null)
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddTelegramBot(options =>
        {
            options.Token = "test-token";
            configureBot?.Invoke(options);
        });
        configureTelegram(builder.Services);
        configureServices(builder.Services);

        if (cancellation is not null)
        {
            builder.Services.AddSingleton(cancellation);
        }

        return builder.Build();
    }

    private static Update CreateMessageUpdate(long updateId)
    {
        return new Update
        {
            UpdateId = updateId,
            Message = new Message
            {
                MessageId = updateId,
                Date = 0,
                Chat = new Chat { Id = 100, Type = "private" },
                Text = $"message-{updateId}"
            }
        };
    }

    private static HttpResponseMessage CreateJsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreateTextResponse(
        string text,
        HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(text, Encoding.UTF8, "text/plain")
        };
    }

    private sealed class RecordingDispatcher(int cancelAfter = 2) : IUpdateDispatcher
    {
        public List<long> UpdateIds { get; } = [];

        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            var update = Assert.IsType<TelegramUpdatePayload>(context.Payload).Update;
            UpdateIds.Add(update.UpdateId);

            if (UpdateIds.Count == cancelAfter)
            {
                var cancellation = context.Services.GetService<CancellationTokenSource>();
                cancellation?.Cancel();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTimeProvider : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            Delays.Add(dueTime);
            ThreadPool.QueueUserWorkItem(static queuedState =>
            {
                var invocation = (TimerInvocation)queuedState!;
                invocation.Callback(invocation.State);
            }, new TimerInvocation(callback, state));

            return new NoOpTimer();
        }

        private sealed record TimerInvocation(TimerCallback Callback, object? State);

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return true;
            }

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class BlockingTimeProvider : TimeProvider
    {
        private readonly TaskCompletionSource _delayStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForDelayAsync()
        {
            return _delayStarted.Task;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            _delayStarted.TrySetResult();
            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return true;
            }

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];

        public bool FireNextTimer()
        {
            ManualTimer? timer;
            lock (_gate)
            {
                timer = _timers.FirstOrDefault(static candidate => !candidate.IsDisposed);
            }

            if (timer is null)
            {
                return false;
            }

            timer.Fire();
            return true;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        private void Remove(ManualTimer timer)
        {
            lock (_gate)
            {
                _timers.Remove(timer);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return !IsDisposed;
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                owner.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void Fire()
            {
                if (IsDisposed)
                {
                    return;
                }

                callback(state);
            }
        }
    }

    private sealed class ThrowingDispatcher(Exception exception) : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromException<HttpResponseMessage>(exception);
        }
    }

    private sealed class RecordingHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var contentType = request.Content?.Headers.ContentType?.ToString() ?? string.Empty;

            Requests.Add(new RecordedRequest(request.RequestUri?.ToString() ?? string.Empty, body, contentType));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued HTTP responses remain.");
            }

            return _responses.Dequeue();
        }
    }

    private sealed partial record class JsonOnlyProbeMethod : ITelegramApiMethod<bool>
    {
        public static string MethodName => "jsonOnlyProbe";

        [JsonPropertyName("value")]
        public required string Value { get; init; }

        [JsonIgnore]
        public object Poison =>
            throw new InvalidOperationException("JSON-only request content should not scan ignored properties.");
    }

    private sealed class RecordingTelegramTransport : ITelegramTransport
    {
        private readonly Queue<TelegramTransportResponse> _responses;

        public RecordingTelegramTransport()
            : this(Array.Empty<TelegramTransportResponse>())
        {
        }

        public RecordingTelegramTransport(params TelegramTransportResponse[] responses)
        {
            _responses = new Queue<TelegramTransportResponse>(
                responses.Length == 0
                    ? [new TelegramTransportResponse(
                        200,
                        """{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}""")]
                    : responses);
        }

        public List<string> RequestUris { get; } = [];

        public List<TelegramTransportContent> Contents { get; } = [];

        public Task<TelegramTransportResponse> SendAsync(
            TelegramTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestUris.Add(request.Uri.ToString());
            Contents.Add(request.Content);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued transport responses remain.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class TrackingHttpMessageHandler : HttpMessageHandler
    {
        public int DisposeCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateJsonResponse("""{"ok":true,"result":true}"""));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }

    private sealed record RecordedRequest(string RequestUri, string Body, string ContentType);

    private sealed record JsonFallbackCallback(string SomeValue);

    private sealed record DeepLinkInvitePayload(long UserId);

    private sealed class CustomDeepLinkPayloadSerializer : IDeepLinkPayloadSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            var invite = Assert.IsType<DeepLinkInvitePayload>(payload);
            return $"custom-{invite.UserId}";
        }

        public TPayload Deserialize<TPayload>(string payload)
        {
            Assert.StartsWith("custom-", payload, StringComparison.Ordinal);
            var userId = long.Parse(payload["custom-".Length..], CultureInfo.InvariantCulture);
            return (TPayload)(object)new DeepLinkInvitePayload(userId);
        }
    }

    private sealed class RecordingTelegramClient : ITelegramClient
    {
        public Func<object, object>? Handler { get; init; }

        public TelegramBotDefaults Defaults { get; } = new();

        public TelegramDeepLinks DeepLinks => DeepLinksOverride ?? throw new InvalidOperationException("No deep links instance is configured.");

        public TelegramDeepLinks? DeepLinksOverride { get; set; }

        public List<object> Methods { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task<TResult> SendAsync<TResult>(
            ITelegramApiMethod<TResult> method,
            CancellationToken cancellationToken = default)
        {
            Methods.Add(method);
            CancellationTokens.Add(cancellationToken);

            if (Handler is null)
            {
                throw new InvalidOperationException("No telegram client handler is configured.");
            }

            return Task.FromResult((TResult)Handler(method));
        }
    }

    private sealed class AllowedUpdatesMessageHandler
    {
        [Message]
        public Task HandleAsync(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AllowedUpdatesCallbackHandler
    {
        [Callback]
        public Task HandleAsync(CallbackQueryContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AllowedUpdatesChatMemberHandler
    {
        [ChatMemberUpdated]
        public Task HandleAsync(ChatMemberUpdatedContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AllowedUpdatesMyChatMemberHandler
    {
        [MyChatMemberUpdated]
        public Task HandleAsync(ChatMemberUpdatedContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class PollingHandledErrorException : InvalidOperationException
    {
        public PollingHandledErrorException()
            : base("polling handled")
        {
        }
    }

    private sealed class PollingHandledErrorThrowingHandler
    {
        [Command("polling-handled-error")]
        public Task HandleAsync(MessageContext context)
        {
            return Task.FromException(new PollingHandledErrorException());
        }
    }

    private sealed class PollingHandledErrorHandler
    {
        [Error<PollingHandledErrorException>]
        public TelegramErrorHandlingResult Handle(PollingHandledErrorException exception)
        {
            return TelegramErrorHandlingResult.Handled;
        }
    }

    private sealed class PollingCancellationHandler
    {
        [Command("polling-cancel")]
        public Task HandleAsync(MessageContext context, CancellationTokenSource cancellation)
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedTelegramClient(params object[] results) : ITelegramClient
    {
        private readonly Queue<object> _results = new(results);

        public TelegramBotDefaults Defaults { get; } = new();

        public TelegramDeepLinks DeepLinks { get; } =
            new("test_bot", new Base64UrlJsonDeepLinkPayloadSerializer());

        public List<GetUpdates> GetUpdatesRequests { get; } = [];

        public Task<TResult> SendAsync<TResult>(
            ITelegramApiMethod<TResult> method,
            CancellationToken cancellationToken = default)
        {
            if (method is GetUpdates getUpdates)
            {
                GetUpdatesRequests.Add(getUpdates);
            }

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No queued Telegram client results remain.");
            }

            var result = _results.Dequeue();

            return result is Exception exception
                ? Task.FromException<TResult>(exception)
                : Task.FromResult((TResult)result);
        }
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableReadStream(byte[] content)
        {
            _inner = new MemoryStream(content);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    [CallbackData("del")]
    public sealed record KeyboardDeleteCallback(int Id);
}
