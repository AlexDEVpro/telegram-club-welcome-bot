using Microsoft.Extensions.Options;

using Telegram.Bot;

using TelegramClubWelcomeBot.Features.AddWelcome.Models;
using TelegramClubWelcomeBot.Infrastructure.Configuration;

namespace TelegramClubWelcomeBot.Infrastructure.Notifications;

internal class DevNotifier
{
    private readonly ITelegramBotClient _bot;
    private readonly TelegramOptions _options;

    private readonly TimeSpan _timeout;

    public DevNotifier(
        ITelegramBotClient bot,
        IOptions<TelegramOptions> options)
    {
        _bot = bot;
        _options = options.Value;

        _timeout = TimeSpan.FromSeconds(_options.DevNotifierTimeoutSec);
    }

    public Task Notify(
        long chatId,
        Welcome welcome,
        string error)
    {
        var text =
$"""
🚨 Welcome bot error

Name:
{welcome.Name}

Chat:
{chatId}

Message:
{welcome.Message}

Error:
{error}
""";

        return SendToAdmins(text);
    }

    public Task Notify(
        Exception ex)
    {
        var text =
$"""
🚨 Welcome bot error

Exception message:
{ex.Message}

Exception stack trace:
{ex.StackTrace}

Inner exception:
{ex.InnerException}
""";

        return SendToAdmins(text);
    }

    public Task Notify(
        string message)
    {
        var text =
$"""
🚨 Welcome bot error

Message:
{message}
""";

        return SendToAdmins(text);
    }

    private async Task SendToAdmins(
        string text)
    {
        foreach (var adminId in _options.AdminIDs)
        {
            using var cts = new CancellationTokenSource(
                    _timeout);

            await _bot.SendMessage(
                adminId,
                text,
                cancellationToken: cts.Token);
        }
    }
}
