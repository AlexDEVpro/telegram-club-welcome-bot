using MediatR;

using Telegram.Bot;
using Telegram.Bot.Types;

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
    private readonly WelcomeMediaUploader _welcomeMediaUploader;
    private readonly DevNotifier _devNotifier;

    public AddWelcomeWizardHandler(
        WizardStateService states,
        StorageService storage,
        ITelegramBotClient bot,
        WelcomeMediaUploader welcomeMediaUploader,
        DevNotifier devNotifier)
    {
        _states = states;
        _storage = storage;
        _bot = bot;
        _welcomeMediaUploader = welcomeMediaUploader;
        _devNotifier = devNotifier;
    }

    public async Task<bool> Handle(
        AddWelcomeWizardCommand request,
        CancellationToken cancellationToken)
    {
        if (request.EphemeralMessageId == null)
            return false;

        var chatId = request.ChatId;
        var fromUserId = request.FromUserId;
        var ephemeralMessageParameters = new EphemeralMessageParameters { ReceiverUserId = fromUserId };
        var replyParameters = new ReplyParameters { EphemeralMessageId = request.EphemeralMessageId };

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
                    ephemeralMessageParameters: ephemeralMessageParameters,
                    replyParameters: replyParameters,
                    disableNotification: true);

                return true;
            }

            var welcomes = _storage.Data.GetWelcomes(state.ChatIdTarget);
            if (welcomes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                await _bot.SendMessage(
                    chatId,
                    Resources.BotMessages.NameAlreadyExists,
                    ephemeralMessageParameters: ephemeralMessageParameters,
                    replyParameters: replyParameters,
                    disableNotification: true);

                return true;
            }

            state.Name = name;
            state.Step = Step.WaitMedia;

            await _bot.SendMessage(
                chatId,
                Resources.BotMessages.SendMedia,
                ephemeralMessageParameters: ephemeralMessageParameters,
                replyParameters: replyParameters,
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
                    ephemeralMessageParameters: ephemeralMessageParameters,
                    replyParameters: replyParameters,
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
                        ephemeralMessageParameters: ephemeralMessageParameters,
                        replyParameters: replyParameters,
                        disableNotification: true);

                    return true;
                }

                using var timeoutCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                WelcomeMediaUploader.PreparedMedia preparedMedia;

                // Type detection is required to continue the wizard.
                try
                {
                    preparedMedia = await _welcomeMediaUploader.PrepareUploadAsync(
                        url,
                        timeoutCts.Token);
                }
                catch (OperationCanceledException)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    await _bot.SendMessage(
                        chatId,
                        Resources.BotMessages.UrlMediaTypeDetectionTimeout,
                        ephemeralMessageParameters: ephemeralMessageParameters,
                        replyParameters: replyParameters,
                        disableNotification: true,
                        cancellationToken: cancellationToken);

                    return true;
                }
                catch (NotSupportedException ex)
                {
                    await _bot.SendMessage(
                        chatId,
                        Resources.BotMessages.UnsupportedUrlMediaType,
                        ephemeralMessageParameters: ephemeralMessageParameters,
                        replyParameters: replyParameters,
                        disableNotification: true,
                        cancellationToken: cancellationToken);

                    await _devNotifier.Notify(ex);

                    return true;
                }
                catch (Exception ex)
                {
                    await _bot.SendMessage(
                        chatId,
                        Resources.BotMessages.UrlMediaTypeDetectionFailed,
                        ephemeralMessageParameters: ephemeralMessageParameters,
                        replyParameters: replyParameters,
                        disableNotification: true,
                        cancellationToken: cancellationToken);

                    await _devNotifier.Notify(ex);

                    return true;
                }

                using (preparedMedia)
                {
                    // From this point, the wizard can continue even if pre-upload fails.
                    state.Url = url;
                    state.Type = preparedMedia.Type;
                    state.FileId = null;
                    state.Step = Step.WaitMessage;

                    Exception? uploadException = null;

                    try
                    {
                        var message = await _welcomeMediaUploader.UploadAsync(
                            chatId,
                            preparedMedia,
                            Resources.BotMessages.SendMessage,
                            ephemeralMessageParameters,
                            replyParameters,
                            disableNotification: true,
                            cancellationToken: timeoutCts.Token);

                        state.FileId = WelcomeMediaUploader.GetFileId(
                            preparedMedia.Type,
                            message);
                    }
                    catch (OperationCanceledException)
                        when (!cancellationToken.IsCancellationRequested)
                    {
                        // The shared timeout expired.
                        // FileId at this stage is optional therefore just continue.
                    }
                    catch (Exception ex)
                    {
                        uploadException = ex;
                    }

                    // If pre-upload failed, still proceed to the welcome text step.
                    if (state.FileId == null)
                    {
                        await _bot.SendMessage(
                            chatId,
                            Resources.BotMessages.SendMessage,
                            ephemeralMessageParameters: ephemeralMessageParameters,
                            replyParameters: replyParameters,
                            disableNotification: true,
                            cancellationToken: cancellationToken);
                    }

                    if (uploadException != null)
                        await _devNotifier.Notify(uploadException);
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

            var entities = request.Entities?.ToArray()
                ?? request.CaptionEntities?.ToArray();

            var html = Telegram.Bot.Extensions.HtmlText.ToHtml(text, entities);

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
                Message = html
            });
            _storage.Save();

            _states.Remove(
                chatId,
                fromUserId);

            await _bot.SendMessage(
                chatId,
                Resources.BotMessages.WelcomeCreated,
                ephemeralMessageParameters: ephemeralMessageParameters,
                replyParameters: replyParameters,
                disableNotification: true);

            return true;
        }

        return false;
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
