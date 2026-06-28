namespace TelegramClubWelcomeBot.Features.AddWelcome.Models;

internal class WizardState
{
    public required Step Step { get; set; }

    public required long ChatIdTarget { get; set; }

    public string? Name { get; set; }

    public string? Url { get; set; }

    public string? FileId { get; set; }

    public WelcomeType? Type { get; set; }
}
