using System.Net;

using Microsoft.Extensions.Options;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using TelegramClubWelcomeBot.Features.AddWelcome.Models;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Features.NewMember.Models;
using TelegramClubWelcomeBot.Infrastructure.Configuration;
using TelegramClubWelcomeBot.Infrastructure.Notifications;

namespace TelegramClubWelcomeBot.Features.NewMember.Services;

internal class WelcomeSender
{
    const string WelcomeUserPlaceholder = "{user}";

    private readonly ITelegramBotClient _bot;
    private readonly StorageService _storage;
    private readonly WelcomeMediaUploader _welcomeMediaUploader;
    private readonly DevNotifier _devNotifier;
    private readonly WelcomeOptions _options;

    private static readonly HttpClient _http = new();

    public WelcomeSender(
        ITelegramBotClient bot,
        StorageService storage,
        WelcomeMediaUploader welcomeMediaUploader,
        DevNotifier devNotifier,
        IOptions<WelcomeOptions> options)
    {
        _bot = bot;
        _storage = storage;
        _welcomeMediaUploader = welcomeMediaUploader;
        _devNotifier = devNotifier;
        _options = options.Value;
    }

    public async Task<bool> Send(
        long chatId,
        int joinMessageId,
        long joinedUserId,
        string joinedUserFirstName,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cooldownBorder = now.AddMinutes(-_options.WelcomeRepeatCooldownMin);

        var existing = _storage.Data.RecentJoins.FirstOrDefault(x =>
                x.ChatId == chatId &&
                x.UserId == joinedUserId);
        if (existing != null)
        {
            if (existing.JoinedAtUtc > cooldownBorder)
                return true;
        }

        var welcomes = _storage.Data.GetWelcomes(chatId);
        if (welcomes.Count == 0)
            return false;

        var welcome = welcomes[Random.Shared.Next(welcomes.Count)];
        var welcomeText = welcome.Message?.Replace(
            WelcomeUserPlaceholder,
            $"""<a href="tg://user?id={joinedUserId}">{WebUtility.HtmlEncode(joinedUserFirstName)}</a>""");
        var useUrlFirst = string.IsNullOrWhiteSpace(welcome.FileId);

        Message sendMediaResponse;

        try
        {
            sendMediaResponse = await SendMedia(
                chatId,
                joinMessageId,
                welcome,
                welcomeText,
                useUrlFirst,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Try with the URL source if the first attempt failed and the URL is available.
            if (!useUrlFirst
                && !string.IsNullOrWhiteSpace(welcome.Url))
            {
                try
                {
                    sendMediaResponse = await SendMedia(
                        chatId,
                        joinMessageId,
                        welcome,
                        welcomeText,
                        true,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception inner)
                {
                    await _devNotifier.Notify(
                        chatId,
                        welcome,
                        inner.ToString());

                    return false;
                }
            }
            else
            {
                await _devNotifier.Notify(
                    chatId,
                    welcome,
                    ex.ToString());

                return false;
            }
        }

        UpdateFileId(welcome, sendMediaResponse);

        if (existing == null)
        {
            _storage.Data.RecentJoins.Add(new RecentJoin
            {
                ChatId = chatId,
                UserId = joinedUserId,
                JoinedAtUtc = now
            });
            _storage.Save();
        }
        else
        {
            existing.JoinedAtUtc = now;
            _storage.Save();
        }

        return true;
    }

    private async Task<Message> SendMedia(
        long chatId,
        int joinMessageId,
        Welcome welcome,
        string? text,
        bool useUrl = false,
        CancellationToken cancellationToken = default)
    {
        var replyParameters = new ReplyParameters
        {
            MessageId = joinMessageId
        };

        if (!useUrl)
        {
            if (string.IsNullOrWhiteSpace(welcome.FileId))
            {
                throw new InvalidOperationException(
                    $"No file ID available for welcome \"{welcome.Name}\".");
            }

            return await _welcomeMediaUploader.SendByFileIdAsync(
                chatId,
                welcome.Type,
                welcome.FileId,
                text,
                replyParameters,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(welcome.Url))
        {
            throw new InvalidOperationException(
                $"No URL available for welcome \"{welcome.Name}\".");
        }

        using var media = await _welcomeMediaUploader.PrepareUploadAsync(
            welcome.Url,
            cancellationToken);

        if (media.Type != welcome.Type)
        {
            throw new InvalidOperationException(
                $"Welcome type mismatch for \"{welcome.Name}\": expected {welcome.Type}, received {media.Type}.");
        }

        return await _welcomeMediaUploader.UploadAsync(
            chatId,
            media,
            text,
            ephemeralMessageParameters: null,
            replyParameters,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stores the file ID after sending the media for future use.
    /// </summary>
    /// <param name="welcome">Welcome object.</param>
    /// <param name="msg">Sent message.</param>
    private void UpdateFileId(
        Welcome welcome,
        Message msg)
    {
        var fileId = WelcomeMediaUploader.GetFileId(
            welcome.Type,
            msg);

        if (fileId == null || fileId == welcome.FileId)
            return;

        welcome.FileId = fileId;
        _storage.Save();
    }
}
