namespace TelegramClubWelcomeBot.Features.NewMember.Models;

internal class RecentJoin
{
    public required long ChatId { get; set; }

    public required long UserId { get; set; }

    public required DateTime JoinedAtUtc { get; set; }
}
