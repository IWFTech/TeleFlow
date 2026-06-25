namespace TeleFlow.Benchmarks.Fixtures;

internal static class UpdateFixtureFiles
{
    public static string Read(UpdateFixture fixture)
    {
        var fileName = fixture switch
        {
            UpdateFixture.MessageCommandStart => "message-command-start.json",
            UpdateFixture.MessageStateText => "message-state-text.json",
            UpdateFixture.CallbackTicketTake => "callback-ticket-take.json",
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, null)
        };

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Updates", fileName);
        return File.ReadAllText(path);
    }
}
