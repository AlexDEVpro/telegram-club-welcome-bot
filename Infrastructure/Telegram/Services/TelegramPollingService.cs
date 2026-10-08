using Microsoft.Extensions.Hosting;

using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace TelegramClubWelcomeBot.Infrastructure.Telegram.Services;

internal class TelegramPollingService : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay =
        TimeSpan.FromSeconds(5);

    private readonly ITelegramBotClient _bot;
    private readonly TelegramUpdateRouter _router;

    public TelegramPollingService(
        ITelegramBotClient bot,
        TelegramUpdateRouter router)
    {
        _bot = bot;
        _router = router;
    }

    protected override async Task ExecuteAsync(
        CancellationToken hostCt)
    {
        while (!hostCt.IsCancellationRequested) // While host isn't stopping.
        {
            using var receiverCts =
                CancellationTokenSource.CreateLinkedTokenSource(hostCt);

            _bot.StartReceiving(
                _router.Update,
                async (client, exception, ct) =>
                    await HandlePollingError(
                        client,
                        exception,
                        ct,
                        receiverCts),
                cancellationToken: receiverCts.Token);

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    receiverCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (hostCt.IsCancellationRequested)
                    break; // Exit if the cancellation was requested by the host.
            }

            if (!hostCt.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        ReconnectDelay,
                        hostCt);
                }
                catch (OperationCanceledException)
                {
                    break; // Exit if the cancellation was requested by the host.
                }
            }
        }
    }

    private async Task HandlePollingError(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken ct,
        CancellationTokenSource receiverCts)
    {
        if (ct.IsCancellationRequested)
            return;

        if (ShouldReconnect(exception))
        {
            Console.WriteLine(
                "HTTPS error. Reconnecting to Telegram...");

            await receiverCts.CancelAsync();

            return;
        }

        try
        {
            await _router.Error(
                bot,
                exception,
                ct);
        }
        catch (Exception errorHandlerException)
        {
            Console.WriteLine(errorHandlerException);
        }
    }

    private static bool ShouldReconnect(Exception exception)
    {
        for (var current = exception;
             current != null;
             current = current.InnerException)
        {
            if (current is HttpRequestException or IOException or TimeoutException)
                return true;

            if (current is TaskCanceledException)
                return true;

            if (current is RequestException requestException &&
                IsTransientRequestException(requestException))
                return true;
        }

        return false;
    }

    private static bool IsTransientRequestException(
        RequestException exception)
    {
        return exception.Message.Contains(
            "timed out",
            StringComparison.OrdinalIgnoreCase);
    }
}
