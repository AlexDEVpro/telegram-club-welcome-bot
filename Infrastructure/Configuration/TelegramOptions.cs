namespace TelegramClubWelcomeBot.Infrastructure.Configuration;

internal class TelegramOptions
{
    public string? BotToken { get; set; }

    public List<long> AdminIDs { get; set; } = new();

    public int DevNotifierTimeoutSec { get; set; } = 10;
}
