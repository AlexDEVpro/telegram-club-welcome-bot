using System.Globalization;

using Microsoft.Extensions.Options;

using TelegramClubWelcomeBot.Constants;
using TelegramClubWelcomeBot.Infrastructure.Configuration;

namespace TelegramClubWelcomeBot.Infrastructure.Localization.Services;

internal class UserCultureResolver
{
    private readonly string _defaultLanguageCode;

    public UserCultureResolver(IOptions<WelcomeOptions> welcomeOptions)
    {
        _defaultLanguageCode = welcomeOptions.Value.DefaultLanguageCode;
    }

    public CultureInfo Resolve(string? telegramLanguageCode)
    {
        var language = string.IsNullOrWhiteSpace(telegramLanguageCode)
                ? SupportedLanguages.Get(_defaultLanguageCode)
                : SupportedLanguages.Get(telegramLanguageCode);

        return new CultureInfo(language.Culture);
    }
}
