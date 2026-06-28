using TelegramClubWelcomeBot.Constants;

namespace TelegramClubWelcomeBot.Infrastructure.Configuration;

internal class WelcomeOptions
{
    public string DefaultLanguageCode { get; set; } = SupportedLanguages.Default.Code;

    public int WelcomeRepeatCooldownMin { get; set; } = 60;
}
