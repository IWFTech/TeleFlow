using System.Text.Json;
using System.Xml.Linq;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Constants;
using TeleFlow.Telegram.Schema.Methods;
using TeleFlow.Telegram.Schema.Types;
using IoFile = System.IO.File;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramSchemaTests
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    [Fact]
    public void SchemaProject_HasNoProjectReferences()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "TeleFlow.Telegram.Schema.csproj"));

        Assert.Empty(document.Descendants("ProjectReference"));
    }

    [Fact]
    public void GeneratedLayout_IsSplitByTypesMethodsResponsesAndAbstractions()
    {
        Assert.True(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types")));
        Assert.True(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods")));
        Assert.True(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Responses")));
        Assert.True(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions")));
        Assert.True(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Constants")));
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "Update.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "SendMessage.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Constants", "ButtonStyles.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Constants", "BotCommandScopeTypes.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Constants", "ChatMemberStatuses.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "SendMessageRequest.g.cs")));
    }

    [Fact]
    public void GeneratedTelegramRuntimeExtensions_ExposeRepresentativeBotMethods()
    {
        var methodsRoot = Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Client", "Generated", "Methods");

        Assert.True(Directory.Exists(methodsRoot));
        Assert.True(IoFile.Exists(Path.Combine(methodsRoot, "GetMeExtensions.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(methodsRoot, "SendMessageExtensions.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(methodsRoot, "SendPhotoExtensions.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(methodsRoot, "AnswerCallbackQueryExtensions.g.cs")));

        var sendMessage = IoFile.ReadAllText(Path.Combine(methodsRoot, "SendMessageExtensions.g.cs"));
        Assert.Contains("Kind: ClientMethod", sendMessage);
        Assert.Contains("public static Task<Message> SendMessageAsync(", sendMessage);
        Assert.Contains("this ITelegramClient bot", sendMessage);
        Assert.Contains("TelegramParseMode? parseMode = null", sendMessage);
        Assert.Contains("TelegramMethodDefaultResolver.ResolveParseMode(bot, parseMode, entities)", sendMessage);
        Assert.Contains("new SendMessage", sendMessage);
        Assert.Contains("bot.SendAsync(", sendMessage);
        Assert.DoesNotContain("SendMessageRequest", sendMessage);
    }

    [Fact]
    public void GeneratedTelegramRuntimeExtensions_ExposeKnownUpdateTypes()
    {
        var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Client", "Generated", "TelegramUpdateTypes.g.cs"));

        Assert.Contains("Kind: UpdateType", file);
        Assert.Contains("public static TelegramUpdateType Message => new(\"message\")", file);
        Assert.Contains("public static TelegramUpdateType CallbackQuery => new(\"callback_query\")", file);
        Assert.Contains("public static IReadOnlyList<TelegramUpdateType> AllKnown", file);
    }

    [Fact]
    public void GeneratedTelegramRuntimeExtensions_AreOwnedByClientPackage()
    {
        Assert.True(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Client", "Generated", "Methods")));
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Client", "Generated", "TelegramUpdateTypes.g.cs")));

        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram", "Generated", "Methods")));
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram", "Generated", "TelegramUpdateTypes.g.cs")));
    }

    [Fact]
    public void GeneratedMethodDeclarations_DoNotUseGlobalQualificationInPublicSurface()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods"), "*.g.cs"))
        {
            var declarationLine = IoFile.ReadLines(file)
                .FirstOrDefault(static line => line.StartsWith("public sealed partial record class ", StringComparison.Ordinal));

            Assert.NotNull(declarationLine);
            Assert.DoesNotContain("global::", declarationLine, StringComparison.Ordinal);
        }

        var getFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "GetFile.g.cs"));
        Assert.Contains("using TelegramFile = TeleFlow.Telegram.Schema.Types.File;", getFile);
        Assert.Contains("ITelegramApiMethod<TelegramFile>", getFile);
    }

    [Fact]
    public void UpdateDto_DeserializesRepresentativeJson()
    {
        const string json =
            """
            {
              "update_id": 10001,
              "message": {
                "message_id": 77,
                "date": 1710000000,
                "chat": {
                  "id": 42,
                  "type": "private",
                  "username": "teleflow_user",
                  "first_name": "Tele"
                },
                "from": {
                  "id": 7,
                  "is_bot": false,
                  "first_name": "Flow",
                  "username": "flow_user"
                },
                "text": "/start"
              }
            }
            """;

        var update = JsonSerializer.Deserialize<Update>(json)!;

        Assert.Equal(10001, update.UpdateId);
        Assert.NotNull(update.Message);
        Assert.Equal(77, update.Message!.MessageId);
        Assert.Equal("/start", update.Message.Text);
        Assert.Equal("private", update.Message.Chat.Type);
        Assert.Equal("flow_user", update.Message.From!.Username);
    }

    [Fact]
    public void CallbackQueryDto_DeserializesRepresentativeJson()
    {
        const string json =
            """
            {
              "id": "cbq-1",
              "from": {
                "id": 9,
                "is_bot": false,
                "first_name": "User"
              },
              "message": {
                "message_id": 81,
                "date": 1710001111,
                "chat": {
                  "id": 50,
                  "type": "private"
                },
                "text": "Tap"
              },
              "chat_instance": "instance-42",
              "data": "delete:81"
            }
            """;

        var callback = JsonSerializer.Deserialize<CallbackQuery>(json)!;

        Assert.Equal("cbq-1", callback.Id);
        Assert.Equal("instance-42", callback.ChatInstance);
        Assert.Equal("delete:81", callback.Data);
        Assert.NotNull(callback.Message);
        Assert.True(callback.Message!.TryGetMessage(out var message));
        Assert.NotNull(message);
        Assert.Equal(81, message.MessageId);
        Assert.Equal("Tap", message.Text);
    }

    [Fact]
    public void MaybeInaccessibleMessage_IsGeneratedAsTypedUnionWrapper()
    {
        var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "MaybeInaccessibleMessage.g.cs"));

        Assert.Contains("public Message? Message { get; }", file);
        Assert.Contains("public InaccessibleMessage? InaccessibleMessage { get; }", file);
        Assert.Contains("public bool TryGetMessage(out Message? value)", file);
        Assert.Contains("public bool TryGetInaccessibleMessage(out InaccessibleMessage? value)", file);
        Assert.Contains("public static MaybeInaccessibleMessage From(Message value)", file);
        Assert.Contains("public static MaybeInaccessibleMessage From(InaccessibleMessage value)", file);
        Assert.DoesNotContain("public object Value { get; }", file, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed partial record class MaybeInaccessibleMessage\r\n{\r\n}", file, StringComparison.Ordinal);
    }

    [Fact]
    public void MaybeInaccessibleMessage_DeserializesAccessibleAndInaccessibleVariants()
    {
        const string accessibleJson =
            """
            {
              "message_id": 81,
              "date": 1710001111,
              "chat": {
                "id": 50,
                "type": "private"
              },
              "text": "Tap"
            }
            """;

        const string inaccessibleJson =
            """
            {
              "message_id": 81,
              "date": 0,
              "chat": {
                "id": 50,
                "type": "private"
              }
            }
            """;

        var accessible = JsonSerializer.Deserialize<MaybeInaccessibleMessage>(accessibleJson)!;
        var inaccessible = JsonSerializer.Deserialize<MaybeInaccessibleMessage>(inaccessibleJson)!;

        Assert.True(accessible.TryGetMessage(out var accessibleMessage));
        Assert.NotNull(accessibleMessage);
        Assert.True(inaccessible.TryGetInaccessibleMessage(out var inaccessibleMessage));
        Assert.NotNull(inaccessibleMessage);
        Assert.Equal(81, inaccessibleMessage.MessageId);
    }

    [Fact]
    public void InputMedia_IsGeneratedAsNamedTypedUnionWrapper()
    {
        var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "InputMedia.g.cs"));

        Assert.Contains("public InputMediaPhoto? InputMediaPhoto { get; }", file);
        Assert.Contains("public bool TryGetInputMediaPhoto(out InputMediaPhoto? value)", file);
        Assert.Contains("public static InputMedia From(InputMediaVideo value)", file);
        Assert.DoesNotContain("public object Value { get; }", file, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed partial record class InputMedia\r\n{\r\n}", file, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedUnionFamilies_AreGeneratedAsTypedWrappers()
    {
        foreach (var typeName in new[] { "ChatMember", "BotCommandScope", "InlineQueryResult", "InputMessageContent", "RichBlock", "RichText" })
        {
            var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", typeName + ".g.cs"));

            Assert.Contains($"[JsonConverter(typeof({typeName}JsonConverter))]", file);
            Assert.Contains($"public sealed partial record class {typeName}", file);
            Assert.Contains($"public static {typeName} From(", file);
            Assert.Contains("TryGet", file);
            Assert.DoesNotContain("public object Value { get; }", file, StringComparison.Ordinal);
            Assert.DoesNotContain($"public sealed partial record class {typeName}\r\n{{\r\n}}", file, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NamedUnions_DeserializeUsingLiteralDiscriminatorsAndShapeRefinement()
    {
        const string chatMemberJson =
            """
            {
              "status": "creator",
              "user": {
                "id": 7,
                "is_bot": false,
                "first_name": "Owner"
              },
              "is_anonymous": false
            }
            """;

        const string inlineArticleJson =
            """
            {
              "type": "article",
              "id": "article-1",
              "title": "Article",
              "input_message_content": {
                "message_text": "Hello"
              }
            }
            """;

        const string cachedAudioJson =
            """
            {
              "type": "audio",
              "id": "audio-1",
              "audio_file_id": "file-1"
            }
            """;

        var chatMember = JsonSerializer.Deserialize<ChatMember>(chatMemberJson)!;
        var inlineArticle = JsonSerializer.Deserialize<InlineQueryResult>(inlineArticleJson)!;
        var cachedAudio = JsonSerializer.Deserialize<InlineQueryResult>(cachedAudioJson)!;

        Assert.True(chatMember.TryGetChatMemberOwner(out var owner));
        Assert.NotNull(owner);
        Assert.Equal(ChatMemberOwner.StatusValue, owner.Status);
        Assert.True(inlineArticle.TryGetInlineQueryResultArticle(out var article));
        Assert.NotNull(article);
        Assert.True(cachedAudio.TryGetInlineQueryResultCachedAudio(out var audio));
        Assert.NotNull(audio);
    }

    [Fact]
    public void InputMessageContent_DeserializesMostSpecificRequiredPropertyMatch()
    {
        const string venueJson =
            """
            {
              "latitude": 10.5,
              "longitude": 20.5,
              "title": "Venue",
              "address": "Address"
            }
            """;

        var content = JsonSerializer.Deserialize<InputMessageContent>(venueJson)!;

        Assert.True(content.TryGetInputVenueMessageContent(out var venue));
        Assert.NotNull(venue);
        Assert.Equal("Venue", venue.Title);
    }

    [Fact]
    public void LiteralFields_HaveDefaultsAndValidateDuringDeserialization()
    {
        var owner = new ChatMemberOwner
        {
            User = new User
            {
                Id = 1,
                IsBot = false,
                FirstName = "Owner"
            },
            IsAnonymous = false
        };

        Assert.Equal(ChatMemberOwner.StatusValue, owner.Status);

        const string invalidJson =
            """
            {
              "status": "owner",
              "user": {
                "id": 7,
                "is_bot": false,
                "first_name": "Owner"
              },
              "is_anonymous": false
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ChatMemberOwner>(invalidJson));
    }

    [Fact]
    public void SendMessage_SerializesTelegramWireNames()
    {
        var request = new SendMessage
        {
            ChatId = IntegerString.From(42L),
            Text = "Hello",
            ParseMode = "MarkdownV2"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"chat_id\":42", json);
        Assert.Contains("\"text\":\"Hello\"", json);
        Assert.Contains("\"parse_mode\":\"MarkdownV2\"", json);
        Assert.DoesNotContain("ChatId", json);
        Assert.DoesNotContain("ParseMode", json);
    }

    [Fact]
    public void OptionalFields_AreOmittedWhenNull()
    {
        var request = new AnswerCallbackQuery
        {
            CallbackQueryId = "cbq-1"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"callback_query_id\":\"cbq-1\"", json);
        Assert.DoesNotContain("\"text\":", json);
        Assert.DoesNotContain("\"show_alert\":", json);
    }

    [Fact]
    public void GetMe_IsGeneratedAsMethodWithoutParameters()
    {
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "GetMe.g.cs")));

        var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "GetMe.g.cs"));
        Assert.Contains("ITelegramApiMethod<User>", file);
        Assert.DoesNotContain("[JsonPropertyName(", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionalReturnTypes_NormalizeIntoReadableUnionAbstractions()
    {
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "MessageBoolean.g.cs")));

        var methodFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "SetGameScore.g.cs"));
        Assert.Contains("ITelegramApiMethod<MessageBoolean>", methodFile);
    }

    [Fact]
    public void MethodResultTypes_AreNormalizedForKnownHostileReturnShapes()
    {
        var methodsRoot = Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods");

        var getMe = IoFile.ReadAllText(Path.Combine(methodsRoot, "GetMe.g.cs"));
        var getMyName = IoFile.ReadAllText(Path.Combine(methodsRoot, "GetMyName.g.cs"));
        var getUpdates = IoFile.ReadAllText(Path.Combine(methodsRoot, "GetUpdates.g.cs"));
        var setGameScore = IoFile.ReadAllText(Path.Combine(methodsRoot, "SetGameScore.g.cs"));

        Assert.Contains("ITelegramApiMethod<User>", getMe);
        Assert.Contains("ITelegramApiMethod<BotName>", getMyName);
        Assert.Contains("ITelegramApiMethod<IReadOnlyList<Update>>", getUpdates);
        Assert.Contains("ITelegramApiMethod<MessageBoolean>", setGameScore);
    }

    [Fact]
    public void IntegerString_SerializesAndDeserializesAsScalarUnion()
    {
        var json = JsonSerializer.Serialize(IntegerString.From(42L), JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<IntegerString>("42", JsonOptions)!;

        Assert.Equal("42", json);
        Assert.Equal(42L, roundTrip.Integer);
        Assert.True(roundTrip.TryGetInteger(out var value));
        Assert.Equal(42L, value);
    }

    [Fact]
    public void AnonymousUnionWrappers_DoNotExposeObjectValue()
    {
        foreach (var abstractionName in new[] { "IntegerString", "MessageBoolean", "ReplyMarkup", "InputFileString", "InputMediaGroupItem" })
        {
            var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", abstractionName + ".g.cs"));

            Assert.Contains($"public sealed partial record class {abstractionName}", file);
            Assert.Contains($"public static {abstractionName} From(", file);
            Assert.Contains("TryGet", file);
            Assert.DoesNotContain("public object Value { get; }", file, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InputFile_IsGeneratedAsUploadPseudoType()
    {
        var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "InputFile.g.cs"));
        using var stream = new MemoryStream([1, 2, 3]);

        var inputFile = InputFile.FromStream(stream, "photo.png");

        Assert.Contains("public static InputFile FromStream(Stream content, string fileName)", file);
        Assert.Contains("multipart/form-data", file);
        Assert.Same(stream, inputFile.Content);
        Assert.Equal("photo.png", inputFile.FileName);
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(inputFile, JsonOptions));
    }

    [Fact]
    public void UploadCapableMediaFields_UseInputFileString()
    {
        var photo = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "InputMediaPhoto.g.cs"));
        var video = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "InputMediaVideo.g.cs"));

        Assert.Contains("public required InputFileString Media", photo);
        Assert.DoesNotContain("public required string Media", photo);
        Assert.Contains("public required InputFileString Media", video);
        Assert.Contains("public InputFileString? Thumbnail", video);
        Assert.Contains("public InputFileString? Cover", video);
    }

    [Fact]
    public void ReplyMarkup_UsesReadableGeneratedAbstractionName()
    {
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "ReplyMarkup.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "InlineKeyboardMarkupReplyKeyboardMarkupReplyKeyboardRemoveForceReply.g.cs")));
    }

    [Fact]
    public void MediaUnion_UsesSemanticGeneratedAbstractionName()
    {
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "InputMediaGroupItem.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "InputMUnionF83C0A.g.cs")));

        var file = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "SendMediaGroup.g.cs"));
        Assert.Contains("IReadOnlyList<InputMediaGroupItem>", file);
    }

    [Fact]
    public void GeneratedAbstractions_DoNotUsePlaceholderOrAbsurdNames()
    {
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "Unknown.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "ToGroupChannelAutomaticallyButWillBeAbleToJoinVilinkEtc.g.cs")));
        Assert.DoesNotContain(
            Directory.GetFiles(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions"), "*.g.cs")
                .Select(Path.GetFileNameWithoutExtension),
            static name => name is not null && System.Text.RegularExpressions.Regex.IsMatch(name, "Union[0-9A-F]{6}$"));
    }

    [Fact]
    public void GeneratedOutput_DoesNotContainResponseEnvelopeDebrisOrObjectUnionFallbacks()
    {
        Assert.True(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Responses", "TelegramApiResponse.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "TelegramApiResponse.g.cs")));

        foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema"), "*.g.cs", SearchOption.AllDirectories))
        {
            var contents = IoFile.ReadAllText(file);
            Assert.DoesNotContain("public object Value { get; }", contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GeneratedFiles_ContainXmlDocumentation()
    {
        var updateFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "Update.g.cs"));
        var sendMessageFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "SendMessage.g.cs"));

        Assert.Contains("/// <summary>", updateFile);
        Assert.Contains("This object represents an incoming update.", updateFile);
        Assert.Contains("/// <summary>", sendMessageFile);
        Assert.Contains("Use this method to send text messages.", sendMessageFile);
    }

    [Fact]
    public void GeneratedFiles_EmitIntendedXmlDocInlineCodeMarkup()
    {
        var backgroundFillFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "BackgroundFill.g.cs"));

        Assert.Contains("<c>BackgroundFillSolid</c>", backgroundFillFile);
        Assert.DoesNotContain("&lt;c&gt;BackgroundFillSolid&lt;/c&gt;", backgroundFillFile);
    }

    [Fact]
    public void GeneratedFiles_KeepTelegramProseAngleBracketsEscaped()
    {
        var richBlockAnchorFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "RichBlockAnchor.g.cs"));

        Assert.Contains("HTML tag &lt;a&gt;", richBlockAnchorFile);
        Assert.DoesNotContain("HTML tag <a>", richBlockAnchorFile);
    }

    [Fact]
    public void GeneratedFiles_ContainCanonicalHeaderMetadata()
    {
        var typeFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Types", "Update.g.cs"));
        var methodFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Methods", "SendMessage.g.cs"));
        var responseFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Responses", "TelegramApiResponse.g.cs"));
        var abstractionFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Abstractions", "ITelegramApiMethod.g.cs"));
        var constantsFile = IoFile.ReadAllText(Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Constants", "ButtonStyles.g.cs"));

        AssertGeneratedHeader(typeFile, "Type");
        AssertGeneratedHeader(methodFile, "Method");
        AssertGeneratedHeader(responseFile, "Response");
        AssertGeneratedHeader(abstractionFile, "Abstraction");
        AssertGeneratedHeader(constantsFile, "ConstantGroup");
    }

    [Fact]
    public void GeneratedConstants_UseReadableGroupNamesForDiscriminatorFamilies()
    {
        var constantsRoot = Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "Constants");

        Assert.True(IoFile.Exists(Path.Combine(constantsRoot, "BackgroundTypes.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(constantsRoot, "ChatBoostSources.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(constantsRoot, "ReactionTypes.g.cs")));
        Assert.True(IoFile.Exists(Path.Combine(constantsRoot, "StoryAreaTypes.g.cs")));

        Assert.False(IoFile.Exists(Path.Combine(constantsRoot, "BackgroundTypeTypes.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(constantsRoot, "ChatBoostSourceSources.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(constantsRoot, "ReactionTypeTypes.g.cs")));
        Assert.False(IoFile.Exists(Path.Combine(constantsRoot, "StoryAreaTypeTypes.g.cs")));
    }

    [Fact]
    public void GeneratedConstants_ExposeKnownBotApiStringValues()
    {
        Assert.Equal("danger", ButtonStyles.Danger);
        Assert.Equal("primary", ButtonStyles.Primary);
        Assert.Equal("success", ButtonStyles.Success);

        Assert.Equal("channel", ChatTypes.Channel);
        Assert.Equal("group", ChatTypes.Group);
        Assert.Equal("private", ChatTypes.Private);
        Assert.Equal("sender", ChatTypes.Sender);
        Assert.Equal("supergroup", ChatTypes.Supergroup);

        Assert.Equal("custom_emoji", ReactionTypes.CustomEmoji);
        Assert.Equal("emoji", ReactionTypes.Emoji);
        Assert.Equal("paid", ReactionTypes.Paid);

        Assert.Equal("all_private_chats", BotCommandScopeTypes.AllPrivateChats);
        Assert.Equal("chat_member", BotCommandScopeTypes.ChatMember);
        Assert.Equal("default", BotCommandScopeTypes.Default);

        Assert.Equal("administrator", ChatMemberStatuses.Administrator);
        Assert.Equal("creator", ChatMemberStatuses.Creator);
        Assert.Equal("kicked", ChatMemberStatuses.Kicked);
        Assert.Equal("left", ChatMemberStatuses.Left);
        Assert.Equal("member", ChatMemberStatuses.Member);
        Assert.Equal("restricted", ChatMemberStatuses.Restricted);

        Assert.Equal("gift_code", ChatBoostSources.GiftCode);
        Assert.Equal("giveaway", ChatBoostSources.Giveaway);
        Assert.Equal("premium", ChatBoostSources.Premium);

        Assert.Equal("photo", InlineQueryResultTypes.Photo);
        Assert.Equal("photo", InlineQueryResultTypes.CachedPhoto);

        Assert.Equal("data", PassportElementErrorSources.Data);
        Assert.Equal("file", PassportElementErrorSources.File);
    }

    [Fact]
    public void GeneratedManifest_ContainsSnapshotAndPipelineMetadata()
    {
        var manifestPath = Path.Combine(RepositoryRoot, "src", "TeleFlow.Telegram.Schema", "telegram-bot-api.manifest.json");

        Assert.True(IoFile.Exists(manifestPath));

        using var document = JsonDocument.Parse(IoFile.ReadAllText(manifestPath));
        var manifest = document.RootElement;
        var source = manifest.GetProperty("source");
        var telegramBotApi = manifest.GetProperty("telegramBotApi");
        var pipeline = manifest.GetProperty("pipeline");

        Assert.Equal(1, manifest.GetProperty("manifestVersion").GetInt32());
        Assert.Equal("https://core.telegram.org/bots/api", source.GetProperty("url").GetString());
        Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("capturedAtUtc").GetString()));
        Assert.Matches("^[0-9a-f]{64}$", source.GetProperty("sha256").GetString()!);
        Assert.Equal("10.1", telegramBotApi.GetProperty("version").GetString());
        Assert.Equal("2026-06-11", telegramBotApi.GetProperty("releasedAt").GetString());
        Assert.Equal("june-11-2026", telegramBotApi.GetProperty("changelogAnchor").GetString());
        Assert.Equal("https://core.telegram.org/bots/api-changelog#june-11-2026", telegramBotApi.GetProperty("changelogUrl").GetString());
        Assert.Equal(8, pipeline.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(11, pipeline.GetProperty("generatorVersion").GetInt32());
    }

    [Fact]
    public void GeneratedTypes_DoNotReferenceCoreOrTelegramAssemblies()
    {
        var referencedAssemblies = typeof(Update).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("TeleFlow.Core", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
    }

    private static void AssertGeneratedHeader(string fileContents, string kind)
    {
        Assert.Contains("// <auto-generated>", fileContents);
        Assert.Contains("//   This file was generated by TeleFlow.Telegram.SchemaGenerator.", fileContents);
        Assert.Contains($"//   Kind: {kind}", fileContents);
        Assert.Contains("//   Source: https://core.telegram.org/bots/api", fileContents);
        Assert.Contains("//   Telegram Bot API version: 10.1", fileContents);
        Assert.Contains("//   Telegram Bot API release: 2026-06-11", fileContents);
        Assert.Contains("//   Telegram Bot API changelog: https://core.telegram.org/bots/api-changelog#june-11-2026", fileContents);
        Assert.DoesNotContain("//   Source snapshot:", fileContents);
        Assert.DoesNotContain("//   Source SHA-256:", fileContents);
        Assert.DoesNotContain("//   Schema version:", fileContents);
        Assert.DoesNotContain("//   Generator version:", fileContents);
        Assert.Contains("//   Do not edit this file manually.", fileContents);
        Assert.Contains("// </auto-generated>", fileContents);
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !IoFile.Exists(Path.Combine(directory.FullName, "TeleFlow.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be resolved.");
    }
}
