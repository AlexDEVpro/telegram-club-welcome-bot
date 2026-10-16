namespace TelegramClubWelcomeBot.Features.NewMember.Models;

internal class PendingWelcome
{
    public required long ChatId { get; set; }
    public required int JoinMessageId { get; set; }
    public required long UserId { get; set; }
    public required string FirstName { get; set; }
}
