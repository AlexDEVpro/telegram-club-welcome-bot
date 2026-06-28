using TelegramClubWelcomeBot.Infrastructure.Localization.Models;

namespace TelegramClubWelcomeBot.Constants;

internal static class SupportedLanguages
{
    public static readonly Language[] All =
    [
        new("en", "en-US", "English"),
        new("ru", "ru-RU", "Русский")
    ];

    public static readonly IReadOnlyDictionary<string, Language> ByCode = All.ToDictionary(x => x.Code);
    public static readonly IReadOnlyDictionary<string, Language> ByCulture = All.ToDictionary(x => x.Culture);
    public static readonly IReadOnlyDictionary<string, Language> ByLabel = All.ToDictionary(x => x.Label);

    public static Language Default => All[0];

    public static Language Get(string code)
        => ByCode.GetValueOrDefault(code) ?? Default;
}
