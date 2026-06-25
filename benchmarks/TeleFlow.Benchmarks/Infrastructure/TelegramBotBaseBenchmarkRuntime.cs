using TelegramBotBase;
using TelegramBotBase.Base;
using TelegramBotBase.Builder;
using TelegramBotBase.Form;
using TeleFlow.Benchmarks.Fixtures;
using TelegramBotUpdate = Telegram.Bot.Types.Update;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class TelegramBotBaseBenchmarkRuntime
{
    private const long DeviceId = 2000001;

    private readonly BotBase _bot;
    private readonly TelegramBotUpdate _textMessageUpdate;

    private TelegramBotBaseBenchmarkRuntime(
        BotBase bot,
        TelegramBotUpdate textMessageUpdate)
    {
        _bot = bot;
        _textMessageUpdate = textMessageUpdate;
    }

    public static async Task<TelegramBotBaseBenchmarkRuntime> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        TelegramBotBaseBenchmarkCounter.Reset();

        var bot = BotBaseBuilder
            .Create()
            .QuickStart<CountingTelegramBotBaseForm>("123:benchmark", false)
            .Build();

        var updateJson = UpdateFixtureFiles.Read(UpdateFixture.MessageStateText);
        var update = TelegramBotJson.DeserializeUpdate(updateJson);
        await bot.Sessions.StartSession(DeviceId).ConfigureAwait(false);

        var runtime = new TelegramBotBaseBenchmarkRuntime(bot, update);

        await runtime.DispatchTextMessageAsync(cancellationToken).ConfigureAwait(false);

        if (TelegramBotBaseBenchmarkCounter.Count != 1)
        {
            throw new InvalidOperationException(
                "TelegramBotBase text dispatch benchmark is invalid: the form was not executed.");
        }

        TelegramBotBaseBenchmarkCounter.Reset();
        return runtime;
    }

    public async Task<int> DispatchTextMessageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = new MessageResult(_textMessageUpdate);

        await _bot
            .InvokeMessageLoop(DeviceId, message)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return TelegramBotBaseBenchmarkCounter.Count;
    }
}

internal sealed class CountingTelegramBotBaseForm : FormBase
{
    public override Task Load(MessageResult message)
    {
        ArgumentNullException.ThrowIfNull(message);

        TelegramBotBaseBenchmarkCounter.Record();
        message.Handled = true;

        return Task.CompletedTask;
    }
}

internal static class TelegramBotBaseBenchmarkCounter
{
    public static int Count { get; private set; }

    public static void Record()
    {
        Count++;
    }

    public static void Reset()
    {
        Count = 0;
    }
}
