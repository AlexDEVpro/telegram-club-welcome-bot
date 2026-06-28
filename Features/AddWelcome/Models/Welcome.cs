using Telegram.Bot.Types;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Models;

internal class Welcome
{
    public required string Name { get; set; }

    public required long ChatId { get; set; }

    public string? Url { get; set; }

    public string? FileId { get; set; }

    public required WelcomeType Type { get; set; }

    public required string Message { get; set; }

    public List<MessageEntity>? Entities { get; set; }
}
