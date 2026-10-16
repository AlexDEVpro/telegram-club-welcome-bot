using TelegramClubWelcomeBot.Features.NewMember.Models;
using TelegramClubWelcomeBot.Infrastructure.Notifications;

namespace TelegramClubWelcomeBot.Features.NewMember.Services;

internal class PendingWelcomeProcessor
{
    private readonly StorageService _storage;
    private readonly WelcomeSender _sender;
    private readonly DevNotifier _devNotifier;

    public PendingWelcomeProcessor(
        StorageService storage,
        WelcomeSender sender,
        DevNotifier devNotifier)
    {
        _storage = storage;
        _sender = sender;
        _devNotifier = devNotifier;
    }

    public async Task HandleJoin(
        long chatId,
        int joinMessageId,
        long userId,
        string firstName)
    {
        var pending = new PendingWelcome
        {
            ChatId = chatId,
            JoinMessageId = joinMessageId,
            UserId = userId,
            FirstName = firstName
        };

        _storage.Data.PendingWelcomes.Add(pending);
        _storage.Save();

        await Process(pending);
    }

    public async Task ProcessAll()
    {
        foreach (var pending in _storage.Data.PendingWelcomes.ToList())
        {
            try
            {
                await Process(pending);
            }
            catch (Exception ex)
            {
                await _devNotifier.Notify($"Failed to process pending welcome (chat ID: {pending.ChatId}, user ID: {pending.UserId}, first name: {pending.FirstName}): {ex}");
            }
        }
    }

    private async Task Process(PendingWelcome pending)
    {
        var succeeded = await _sender.Send(
            pending.ChatId,
            pending.JoinMessageId,
            pending.UserId,
            pending.FirstName);

        if (!succeeded)
            return;

        _storage.Data.PendingWelcomes.Remove(pending);
        _storage.Save();
    }
}
