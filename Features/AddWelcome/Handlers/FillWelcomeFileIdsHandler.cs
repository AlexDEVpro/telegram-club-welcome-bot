using MediatR;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using TelegramClubWelcomeBot.Features.AddWelcome.Commands;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Infrastructure.Notifications;
using TelegramClubWelcomeBot.Resources;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Handlers;

internal class FillWelcomeFileIdsHandler
    : IRequestHandler<FillWelcomeFileIdsCommand>
{
    private readonly StorageService _storage;
    private readonly ITelegramBotClient _bot;
    private readonly WelcomeMediaUploader _welcomeMediaUploader;
    private readonly DevNotifier _devNotifier;

    public FillWelcomeFileIdsHandler(
        StorageService storage,
        ITelegramBotClient bot,
        WelcomeMediaUploader welcomeMediaUploader,
        DevNotifier devNotifier)
    {
        _storage = storage;
        _bot = bot;
        _welcomeMediaUploader = welcomeMediaUploader;
        _devNotifier = devNotifier;
    }

    public async Task Handle(
        FillWelcomeFileIdsCommand request,
        CancellationToken cancellationToken)
    {
        var chat = await _bot.GetChat(
            request.ChatId,
            cancellationToken);

        if (chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await _bot.SendMessage(
                request.ChatId,
                BotMessages.FillWelcomeFileIdsGroupOnly,
                cancellationToken: cancellationToken);

            return;
        }

        var member = await _bot.GetChatMember(
            request.ChatId,
            request.FromUserId,
            cancellationToken: cancellationToken);

        if (member.Status is not (
            ChatMemberStatus.Creator or ChatMemberStatus.Administrator))
        {
            await SendEphemeralMessageAsync(
                request,
                BotMessages.FillWelcomeFileIdsAdminRequired,
                cancellationToken);

            return;
        }

        var ephemeralMessageParameters = new EphemeralMessageParameters
        {
            ReceiverUserId = request.FromUserId
        };

        var replyParameters = new ReplyParameters
        {
            EphemeralMessageId = request.EphemeralMessageId
        };

        var welcomes = _storage.Data.GetWelcomes(request.ChatId)
            .Where(x =>
                string.IsNullOrWhiteSpace(x.FileId)
                && !string.IsNullOrWhiteSpace(x.Url))
            .ToList();

        if (welcomes.Count == 0)
        {
            await SendEphemeralMessageAsync(
                request,
                BotMessages.FillWelcomeFileIdsNothingToFill,
                cancellationToken);

            return;
        }

        try
        {
            await _bot.SendChatAction(
                request.FromUserId,
                ChatAction.Typing,
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode is 400 or 403)
        {
            var botInfo = await _bot.GetMe(cancellationToken);

            var keyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(
                    BotMessages.OpenBotPrivateChat,
                    $"https://t.me/{botInfo.Username}"));

            await _bot.SendMessage(
                request.ChatId,
                BotMessages.FillWelcomeFileIdsPrivateChatUnavailable,
                replyMarkup: keyboard,
                ephemeralMessageParameters: ephemeralMessageParameters,
                replyParameters: replyParameters,
                disableNotification: true,
                cancellationToken: cancellationToken);

            return;
        }

        await SendEphemeralMessageAsync(
            request,
            BotMessages.FillWelcomeFileIdsStarted,
            cancellationToken);

        var filled = 0;
        var failed = 0;

        foreach (var welcome in welcomes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var media = await _welcomeMediaUploader.PrepareUploadAsync(
                    welcome.Url!,
                    cancellationToken);

                if (media.Type != welcome.Type)
                {
                    throw new InvalidOperationException($"Media type mismatch for welcome \"{welcome.Name}\": expected {welcome.Type}, received {media.Type}.");
                }

                var message = await _welcomeMediaUploader.UploadAsync(
                    request.FromUserId,
                    media,
                    caption: null,
                    ephemeralMessageParameters: null,
                    replyParameters: null,
                    cancellationToken: cancellationToken,
                    disableNotification: true);

                var fileId = WelcomeMediaUploader.GetFileId(
                    welcome.Type,
                    message);

                if (string.IsNullOrWhiteSpace(fileId))
                {
                    throw new InvalidOperationException($"Telegram returned no FileId for welcome \"{welcome.Name}\".");
                }

                welcome.FileId = fileId;
                _storage.Save();

                filled++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;

                await _devNotifier.Notify(ex);
            }
        }

        await _bot.SendMessage(
            request.ChatId,
            string.Format(
                BotMessages.FillWelcomeFileIdsCompleted,
                filled,
                failed),
            ephemeralMessageParameters: ephemeralMessageParameters,
            replyParameters: replyParameters,
            disableNotification: true,
            cancellationToken: cancellationToken);
    }

    private async Task SendEphemeralMessageAsync(
        FillWelcomeFileIdsCommand request,
        string text,
        CancellationToken cancellationToken)
    {
        await _bot.SendMessage(
            request.ChatId,
            text,
            ephemeralMessageParameters: new EphemeralMessageParameters
            {
                ReceiverUserId = request.FromUserId
            },
            replyParameters: new ReplyParameters
            {
                EphemeralMessageId = request.EphemeralMessageId
            },
            disableNotification: true,
            cancellationToken: cancellationToken);
    }
}
