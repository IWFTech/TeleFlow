using System.Reflection;
using System.Text.Json.Serialization;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramUpdateClassificationTests
{
    [Fact]
    public void Classifier_RecognizesEveryGeneratedUpdatePayloadProperty()
    {
        var payloadProperties = typeof(Update)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.Name != nameof(Update.UpdateId))
            .OrderBy(static property => property.MetadataToken)
            .ToArray();

        Assert.NotEmpty(payloadProperties);

        foreach (var property in payloadProperties)
        {
            var update = new Update { UpdateId = 1 };
            var payload = Activator.CreateInstance(property.PropertyType)
                ?? throw new InvalidOperationException($"Could not create update payload '{property.PropertyType}'.");
            property.SetValue(update, payload);

            var expectedType = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? throw new InvalidOperationException($"Update property '{property.Name}' has no JSON field name.");
            var classification = TelegramUpdateClassifier.Classify(update);

            Assert.Equal(expectedType, classification.Type);
        }
    }

    [Fact]
    public void Classifier_ExtractsIdentityAcrossUpdateFamilies()
    {
        var user = Object<User>(
            (nameof(User.Id), 42L),
            (nameof(User.IsBot), false),
            (nameof(User.FirstName), "User"));
        var chat = Object<Chat>(
            (nameof(Chat.Id), 100L),
            (nameof(Chat.Type), "private"));
        var message = Object<Message>(
            (nameof(Message.MessageId), 1L),
            (nameof(Message.Date), 0L),
            (nameof(Message.From), user),
            (nameof(Message.Chat), chat));
        var boostSource = ChatBoostSource.From(Object<ChatBoostSourcePremium>((nameof(ChatBoostSourcePremium.User), user)));
        var boost = Object<ChatBoost>((nameof(ChatBoost.Source), boostSource));

        var cases = new[]
        {
            Case(new Update { UpdateId = 1, EditedMessage = message }, "edited_message", user, chat),
            Case(
                new Update
                {
                    UpdateId = 2,
                    BusinessConnection = Object<BusinessConnection>((nameof(BusinessConnection.User), user))
                },
                "business_connection",
                user,
                null),
            Case(
                new Update
                {
                    UpdateId = 3,
                    DeletedBusinessMessages = Object<BusinessMessagesDeleted>((nameof(BusinessMessagesDeleted.Chat), chat))
                },
                "deleted_business_messages",
                null,
                chat),
            Case(
                new Update
                {
                    UpdateId = 4,
                    MessageReaction = Object<MessageReactionUpdated>(
                        (nameof(MessageReactionUpdated.User), user),
                        (nameof(MessageReactionUpdated.Chat), chat))
                },
                "message_reaction",
                user,
                chat),
            Case(
                new Update
                {
                    UpdateId = 5,
                    InlineQuery = Object<InlineQuery>((nameof(InlineQuery.From), user))
                },
                "inline_query",
                user,
                null),
            Case(
                new Update
                {
                    UpdateId = 6,
                    PreCheckoutQuery = Object<PreCheckoutQuery>((nameof(PreCheckoutQuery.From), user))
                },
                "pre_checkout_query",
                user,
                null),
            Case(
                new Update
                {
                    UpdateId = 7,
                    PollAnswer = Object<PollAnswer>(
                        (nameof(PollAnswer.User), user),
                        (nameof(PollAnswer.VoterChat), chat))
                },
                "poll_answer",
                user,
                chat),
            Case(
                new Update
                {
                    UpdateId = 8,
                    ChatJoinRequest = Object<ChatJoinRequest>(
                        (nameof(ChatJoinRequest.From), user),
                        (nameof(ChatJoinRequest.Chat), chat))
                },
                "chat_join_request",
                user,
                chat),
            Case(
                new Update
                {
                    UpdateId = 9,
                    ChatBoost = Object<ChatBoostUpdated>(
                        (nameof(ChatBoostUpdated.Chat), chat),
                        (nameof(ChatBoostUpdated.Boost), boost))
                },
                "chat_boost",
                user,
                chat),
            Case(
                new Update
                {
                    UpdateId = 10,
                    ManagedBot = Object<ManagedBotUpdated>((nameof(ManagedBotUpdated.User), user))
                },
                "managed_bot",
                user,
                null),
            Case(
                new Update
                {
                    UpdateId = 11,
                    Subscription = Object<BotSubscriptionUpdated>((nameof(BotSubscriptionUpdated.User), user))
                },
                "subscription",
                user,
                null)
        };

        foreach (var testCase in cases)
        {
            var classification = TelegramUpdateClassifier.Classify(testCase.Update);

            Assert.Equal(testCase.Type, classification.Type);
            Assert.Same(testCase.User, classification.User);
            Assert.Same(testCase.Chat, classification.Chat);
        }
    }

    private static ClassificationCase Case(Update update, string type, User? user, Chat? chat)
    {
        return new ClassificationCase(update, type, user, chat);
    }

    private static T Object<T>(params (string Property, object? Value)[] values)
        where T : class
    {
        var instance = Activator.CreateInstance<T>();

        foreach (var (property, value) in values)
        {
            var propertyInfo = typeof(T).GetProperty(property)
                ?? throw new InvalidOperationException(
                    $"Property '{property}' was not found on '{typeof(T)}'.");

            propertyInfo.SetValue(instance, value);
        }

        return instance;
    }

    private sealed record ClassificationCase(Update Update, string Type, User? User, Chat? Chat);
}
