using System.Net.Http.Headers;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using TelegramClubWelcomeBot.Features.AddWelcome.Models;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Services;

internal class WelcomeMediaUploader
{
    private readonly ITelegramBotClient _bot;

    private static readonly HttpClient _http = new();

    public WelcomeMediaUploader(
        ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task<PreparedMedia> PrepareUploadAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        try
        {
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (string.IsNullOrWhiteSpace(mediaType))
                throw new InvalidOperationException("Content-Type header is missing.");

            var type = DetectType(mediaType);

            return new PreparedMedia(
                response,
                type,
                GetFileName(response.Content.Headers, url));
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task<Message> UploadAsync(
        long chatId,
        PreparedMedia media,
        string? caption,
        EphemeralMessageParameters? ephemeralMessageParameters,
        ReplyParameters? replyParameters,
        ParseMode parseMode = ParseMode.None,
        bool disableNotification = false,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await media.Response.Content.ReadAsStreamAsync(
            cancellationToken);

        var inputFile = new InputFileStream(stream, media.FileName);

        return media.Type switch
        {
            WelcomeType.Image => await _bot.SendPhoto(
                chatId,
                inputFile,
                caption: caption,
                parseMode: parseMode,
                ephemeralMessageParameters: ephemeralMessageParameters,
                replyParameters: replyParameters,
                disableNotification: disableNotification,
                cancellationToken: cancellationToken),

            WelcomeType.Animation => await _bot.SendAnimation(
                chatId,
                inputFile,
                caption: caption,
                parseMode: parseMode,
                ephemeralMessageParameters: ephemeralMessageParameters,
                replyParameters: replyParameters,
                disableNotification: disableNotification,
                cancellationToken: cancellationToken),

            WelcomeType.Video => await _bot.SendVideo(
                chatId,
                inputFile,
                caption: caption,
                parseMode: parseMode,
                ephemeralMessageParameters: ephemeralMessageParameters,
                replyParameters: replyParameters,
                disableNotification: disableNotification,
                cancellationToken: cancellationToken),

            _ => throw new NotSupportedException($"Unsupported welcome type: {media.Type}.")
        };
    }

    public async Task<Message> SendByFileIdAsync(
        long chatId,
        WelcomeType type,
        string fileId,
        string? caption,
        ReplyParameters? replyParameters,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default)
    {
        return type switch
        {
            WelcomeType.Image => await _bot.SendPhoto(
                chatId,
                fileId,
                caption: caption,
                parseMode: parseMode,
                replyParameters: replyParameters,
                cancellationToken: cancellationToken),

            WelcomeType.Animation => await _bot.SendAnimation(
                chatId,
                fileId,
                caption: caption,
                parseMode: parseMode,
                replyParameters: replyParameters,
                cancellationToken: cancellationToken),

            WelcomeType.Video => await _bot.SendVideo(
                chatId,
                fileId,
                caption: caption,
                parseMode: parseMode,
                replyParameters: replyParameters,
                cancellationToken: cancellationToken),

            _ => throw new NotSupportedException(
                $"Unsupported welcome type: {type}.")
        };
    }

    public static string? GetFileId(
        WelcomeType type,
        Message message)
    {
        return type switch
        {
            WelcomeType.Image => message.Photo?.LastOrDefault()?.FileId,
            WelcomeType.Animation => message.Animation?.FileId,
            WelcomeType.Video => message.Video?.FileId,
            _ => null
        };
    }

    private static WelcomeType DetectType(string mediaType)
    {
        // GIF must be checked before the general image/ prefix.
        if (mediaType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            return WelcomeType.Animation;

        if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return WelcomeType.Image;

        if (mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return WelcomeType.Video;

        throw new NotSupportedException($"Unsupported content type: {mediaType}.");
    }

    private static string GetFileName(
        HttpContentHeaders headers,
        string url)
    {
        var contentDisposition = headers.ContentDisposition;

        if (!string.IsNullOrWhiteSpace(contentDisposition?.FileNameStar))
            return contentDisposition.FileNameStar.Trim('"');

        if (!string.IsNullOrWhiteSpace(contentDisposition?.FileName))
            return contentDisposition.FileName.Trim('"');

        var uri = new Uri(url);
        var name = Path.GetFileName(uri.AbsolutePath);

        return string.IsNullOrWhiteSpace(name)
            ? "file.bin"
            : name;
    }

    internal sealed class PreparedMedia : IDisposable
    {
        internal HttpResponseMessage Response { get; }
        internal WelcomeType Type { get; }
        internal string FileName { get; }

        internal PreparedMedia(
            HttpResponseMessage response,
            WelcomeType type,
            string fileName)
        {
            Response = response;
            Type = type;
            FileName = fileName;
        }

        public void Dispose()
        {
            Response.Dispose();
        }
    }
}
