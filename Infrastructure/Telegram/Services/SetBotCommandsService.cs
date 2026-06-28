using System.Globalization;
using System.Resources;

using Microsoft.Extensions.Hosting;

using Telegram.Bot;
using Telegram.Bot.Types;

using TelegramClubWelcomeBot.Constants;

using TelegramClubWelcomeBot.Resources;

namespace TelegramClubWelcomeBot.Infrastructure.Telegram.Services;

internal class SetBotCommandsService : IHostedService
{
    private readonly ITelegramBotClient _bot;

    private readonly ResourceManager _resources =
        new(typeof(BotMessages));

    public SetBotCommandsService(
        ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var lang in SupportedLanguages.All)
        {
            await RegisterCommands(
                lang.Code,
                new CultureInfo(lang.Culture),
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private async Task RegisterCommands(
        string languageCode,
        CultureInfo culture,
        CancellationToken cancellationToken)
    {
        await _bot.SetMyCommands(
            CreateCommands(culture, true),
            scope: new BotCommandScopeAllChatAdministrators(),
            languageCode: languageCode,
            cancellationToken: cancellationToken);

        // Call if there will be any non admin commands in the future.
        //await _bot.SetMyCommands(
        //    CreateCommands(culture, false),
        //    scope: new BotCommandScopeDefault(),
        //    languageCode: languageCode,
        //    cancellationToken: cancellationToken);
    }

    private BotCommand[] CreateCommands(
        CultureInfo culture,
        bool isAdmin)
    {
        return
        [
            ..(isAdmin ?
                new []
                {
                    new BotCommand
                    {
                        Command = BotCommands.AddWelcome,
                        Description = GetString(nameof(BotMessages.BotCommandAddWelcome), culture)
                    },
                    new BotCommand
                    {
                        Command = BotCommands.Cancel,
                        Description = GetString(nameof(BotMessages.BotCommandCancel), culture)
                    }
                }
                : []
            )
        ];
    }

    private string GetString(string resourceName,
        CultureInfo culture)
    {
        return _resources.GetString(resourceName, culture)!;
    }
}
