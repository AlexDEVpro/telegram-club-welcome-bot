namespace TelegramClubWelcomeBot.Infrastructure.Localization.Models;

internal sealed record Language(
    string Code,    // e. g. "en"
    string Culture, // e. g. "en-US"
    string Label);  // e. g. "English"
