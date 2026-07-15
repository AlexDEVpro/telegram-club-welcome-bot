using MediatR;

using Telegram.Bot;

using TelegramClubWelcomeBot.Features.AddWelcome.Commands;
using TelegramClubWelcomeBot.Features.AddWelcome.Models;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Infrastructure.Notifications;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Handlers;

internal class AddWelcomeWizardHandler
    : IRequestHandler<AddWelcomeWizardCommand, bool>
{
    private readonly WizardStateService _states;
    private readonly StorageService _storage;
    private readonly ITelegramBotClient _bot;
    private readonly DevNotifier _devNotifier;

    private static readonly HttpClient _http = new();

    public AddWelcomeWizardHandler(
        WizardStateService states,
        StorageService storage,
        ITelegramBotClient bot,
        DevNotifier devNotifier)
    {
        _states = states;
        _storage = storage;
        _bot = bot;
        _devNotifier = devNotifier;
    }

    public async Task<bool> Handle(
        AddWelcomeWizardCommand request,
        CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        var fromUserId = request.FromUserId;

        if (!_states.TryGet(chatId, fromUserId, out var state))
            return false;

        // Welcome name.
        if (state.Step == Step.WaitName)
        {
            var name = (request.Text ?? "")
                .Trim()
                .Trim('"');

            if (string.IsNullOrWhiteSpace(name))
            {
                await _bot.SendMessage(
                    chatId,
                    Resources.BotMessages.SendWelcomeName,
                    disableNotification: true);

                return true;
            }

            var welcomes = _storage.Data.GetWelcomes(state.ChatIdTarget);
            if (welcomes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                await _bot.SendMessage(
                    chatId,
                    Resources.BotMessages.NameAlreadyExists,
                    disableNotification: true);

                return true;
            }

            state.Name = name;
            state.Step = Step.WaitMedia;

            await _bot.SendMessage(
                chatId,
                Resources.BotMessages.SendMedia,
                disableNotification: true);

            return true;
        }

        // Welcome media.
        if (state.Step == Step.WaitMedia)
        {
            if (request.Photo != null
                || request.Animation != null
                || request.Video != null)
            {
                // Photo.
                if (request.Photo != null)
                {
                    state.FileId = request.Photo.Last().FileId;
                    state.Type = WelcomeType.Image;
                }

                // Animation.
                if (request.Animation != null)
                {
                    state.FileId = request.Animation.FileId;
                    state.Type = WelcomeType.Animation;
                }

                // Video.
                if (request.Video != null)
                {
                    state.FileId = request.Video.FileId;
                    state.Type = WelcomeType.Video;
                }

                state.Step = Step.WaitMessage;

                await _bot.SendMessage(
                    chatId,
                    Resources.BotMessages.SendMessage,
                    disableNotification: true);

                return true;
            }

            // URL.
            var urlCandidate = request.Text
                ?? request.Caption;

            if (!string.IsNullOrWhiteSpace(urlCandidate))
            {
                var url = urlCandidate.Trim();
                if (!IsValidUrl(url))
                {
                    await _bot.SendMessage(
                        chatId,
                        Resources.BotMessages.InvalidUrl,
                        disableNotification: true);

                    return true;
                }

                try
                {
                    var type = await DetectWelcomeType(url);

                    state.Url = url;
                    state.Type = type;
                    state.Step = Step.WaitMessage;

                    await _bot.SendMessage(
                        chatId,
                        Resources.BotMessages.SendMessage,
                        disableNotification: true);
                }
                catch (Exception ex)
                {
                    await _bot.SendMessage(
                        chatId,
                        Resources.BotMessages.UnsupportedUrlMediaType,
                        disableNotification: true);
                    await _devNotifier.Notify(ex);
                }

                return true;
            }

            return true;
        }

        // Welcome message.
        if (state.Step == Step.WaitMessage)
        {
            var text = request.Text
                ?? request.Caption
                ?? "";

            var entities = request.Entities?.ToList()
                ?? request.CaptionEntities?.ToList();

            if (state.Name == null)
                throw new InvalidOperationException("Welcome name is not specified.");
            var type = state.Type
                ?? throw new InvalidOperationException("Welcome type is not specified.");

            _storage.Data.AddWelcome(state.ChatIdTarget, new Welcome
            {
                Name = state.Name,
                Url = state.Url,
                FileId = state.FileId,
                Type = type,
                Message = text,
                Entities = entities
            });
            _storage.Save();

            _states.Remove(
                chatId,
                fromUserId);

            await _bot.SendMessage(
                chatId,
                Resources.BotMessages.WelcomeCreated,
                disableNotification: true);

            return true;
        }

        return false;
    }

    private static async Task<WelcomeType> DetectWelcomeType(
        string url)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Head,
            url);

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new Exception("Content-Type header is missing.");

        if (mediaType.StartsWith("image/"))
            return WelcomeType.Image;
        if (mediaType.StartsWith("image/gif"))
            return WelcomeType.Animation;
        if (mediaType.StartsWith("video/"))
            return WelcomeType.Video;

        throw new Exception($"Unsupported content type: {mediaType}.");
    }

    private static bool IsValidUrl(
        string urlCandidate)
    {
        return Uri.TryCreate(
            urlCandidate,
            UriKind.Absolute,
            out var uri)
            &&
            (uri.Scheme == Uri.UriSchemeHttps ||
             uri.Scheme == Uri.UriSchemeHttp);
    }
}
