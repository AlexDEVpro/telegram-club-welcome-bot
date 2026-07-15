using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using Telegram.Bot;
using Telegram.Bot.Types;

using TelegramClubWelcomeBot.Features.AddWelcome.Models;
using TelegramClubWelcomeBot.Features.NewMember.Models;
using TelegramClubWelcomeBot.Infrastructure.Configuration;
using TelegramClubWelcomeBot.Infrastructure.Notifications;

namespace TelegramClubWelcomeBot.Features.NewMember.Services;

internal class WelcomeSender
{
    private readonly ITelegramBotClient _bot;
    private readonly StorageService _storage;
    private readonly DevNotifier _devNotifier;
    private readonly WelcomeOptions _options;

    private static readonly HttpClient _http = new();

    public WelcomeSender(
        ITelegramBotClient bot,
        StorageService storage,
        DevNotifier devNotifier,
        IOptions<WelcomeOptions> options)
    {
        _bot = bot;
        _storage = storage;
        _devNotifier = devNotifier;
        _options = options.Value;
    }

    public async Task Send(
        long chatId,
        int joinMessageId,
        long joinedUserId,
        string joinedUserFirstName)
    {
        var now = DateTime.UtcNow;
        var cooldownBorder = now.AddMinutes(-_options.WelcomeRepeatCooldownMin);

        var existing = _storage.Data.RecentJoins.FirstOrDefault(x =>
                x.ChatId == chatId &&
                x.UserId == joinedUserId);
        if (existing != null)
        {
            if (existing.JoinedAtUtc > cooldownBorder)
                return;

            existing.JoinedAtUtc = now;
        }
        else
        {
            _storage.Data.RecentJoins.Add(new RecentJoin
            {
                ChatId = chatId,
                UserId = joinedUserId,
                JoinedAtUtc = now
            });
        }

        _storage.Save();

        var welcomes = _storage.Data.GetWelcomes(chatId);
        if (!welcomes.Any())
            return;

        var welcome = welcomes[Random.Shared.Next(welcomes.Count)];
        var welcomeText = (welcome.Message ?? "")
            .Replace("{user}", WebUtility.HtmlEncode(joinedUserFirstName));
        var useUrlFirst = string.IsNullOrWhiteSpace(welcome.FileId);

        try
        {
            var msg = await SendMedia(
                chatId,
                joinMessageId,
                welcome,
                welcomeText,
                useUrlFirst);

            if (useUrlFirst)
            {
                UpdateFileId(welcome, msg);
            }
        }
        catch (Exception ex)
        {
            // Try with the URL source if the first attempt failed and the URL is available.
            if (!useUrlFirst
                && !string.IsNullOrWhiteSpace(welcome.Url))
            {
                try
                {
                    var msg = await SendMedia(
                        chatId,
                        joinMessageId,
                        welcome,
                        welcomeText,
                        true);

                    UpdateFileId(welcome, msg);
                }
                catch (Exception inner)
                {
                    await _devNotifier.Notify(
                        chatId,
                        welcome,
                        inner.ToString());
                }
            }
            else
            {
                await _devNotifier.Notify(
                    chatId,
                    welcome,
                    ex.ToString());
            }
        }
    }

    private async Task<Message> SendMedia(
        long chatId,
        int joinMessageId,
        Welcome welcome,
        string text,
        bool useUrl = false)
    {
        if (useUrl)
        {
            if (string.IsNullOrWhiteSpace(welcome.Url))
            {
                throw new InvalidOperationException($"No URL available for welcome \"{welcome.Name}\".");
            }

            return welcome.Type switch
            {
                WelcomeType.Image =>
                    await SendFromUrl(
                        welcome.Url,
                        fileStream =>
                            _bot.SendPhoto(
                                chatId,
                                fileStream,
                                caption: text,
                                captionEntities: welcome.Entities,
                                replyParameters: new ReplyParameters
                                {
                                    MessageId = joinMessageId
                                })),

                WelcomeType.Animation =>
                    await SendFromUrl(
                        welcome.Url,
                        fileStream =>
                            _bot.SendAnimation(
                                chatId,
                                fileStream,
                                caption: text,
                                captionEntities: welcome.Entities,
                                replyParameters: new ReplyParameters
                                {
                                    MessageId = joinMessageId
                                })),

                WelcomeType.Video =>
                    await SendFromUrl(
                        welcome.Url,
                        fileStream =>
                            _bot.SendVideo(
                                chatId,
                                fileStream,
                                caption: text,
                                captionEntities: welcome.Entities,
                                replyParameters: new ReplyParameters
                                {
                                    MessageId = joinMessageId
                                })),

                _ => await HandleUnsupportedType(chatId, welcome)
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(welcome.FileId))
            {
                throw new InvalidOperationException($"No file ID available for welcome \"{welcome.Name}\".");
            }

            return welcome.Type switch
            {
                WelcomeType.Image =>
                    await _bot.SendPhoto(
                        chatId,
                        welcome.FileId,
                        caption: text,
                        captionEntities: welcome.Entities,
                        replyParameters: new ReplyParameters
                        {
                            MessageId = joinMessageId
                        }),

                WelcomeType.Animation =>
                    await _bot.SendAnimation(
                        chatId,
                        welcome.FileId,
                        caption: text,
                        captionEntities: welcome.Entities,
                        replyParameters: new ReplyParameters
                        {
                            MessageId = joinMessageId
                        }),

                WelcomeType.Video =>
                    await _bot.SendVideo(
                        chatId,
                        welcome.FileId,
                        caption: text,
                        captionEntities: welcome.Entities,
                        replyParameters: new ReplyParameters
                        {
                            MessageId = joinMessageId
                        }),

                _ => await HandleUnsupportedType(chatId, welcome)
            };
        }
    }

    private async Task<Message> SendFromUrl(
        string url,
        Func<InputFile, Task<Message>> sender)
    {
        using var response = await _http.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();

        var inputFileStream =
            new InputFileStream(
                stream,
                GetFileName(
                    response.Content.Headers,
                    url));

        return await sender(inputFileStream);
    }

    private static string GetFileName(
        HttpContentHeaders headers,
        string url)
    {
        var cd = headers.ContentDisposition;

        if (!string.IsNullOrWhiteSpace(cd?.FileNameStar))
            return cd.FileNameStar.Trim('"');

        if (!string.IsNullOrWhiteSpace(cd?.FileName))
            return cd.FileName.Trim('"');

        var uri = new Uri(url);
        var name = Path.GetFileName(uri.AbsolutePath);

        return string.IsNullOrWhiteSpace(name)
            ? "file.bin"
            : name;
    }

    private async Task<Message> HandleUnsupportedType(
        long chatId,
        Welcome welcome)
    {
        var errorMessage = $"Unhandled welcome type: {welcome.Type}.";

        await _devNotifier.Notify(
            chatId,
            welcome,
            errorMessage);

        throw new NotSupportedException(errorMessage);
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
        if (msg.Video != null)
            welcome.FileId = msg.Video.FileId;

        if (msg.Photo != null)
            welcome.FileId = msg.Photo.Last().FileId;

        if (msg.Animation != null)
            welcome.FileId = msg.Animation.FileId;

        _storage.Save();
    }
}
