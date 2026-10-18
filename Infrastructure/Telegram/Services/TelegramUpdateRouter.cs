using MediatR;

using Telegram.Bot;
using Telegram.Bot.Types;

using TelegramClubWelcomeBot.Constants;
using TelegramClubWelcomeBot.Features.AddWelcome.Commands;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Features.NewMember.Commands;
using TelegramClubWelcomeBot.Infrastructure.Localization.Services;
using TelegramClubWelcomeBot.Infrastructure.Notifications;
using TelegramClubWelcomeBot.Infrastructure.Persistence.Services;

namespace TelegramClubWelcomeBot.Infrastructure.Telegram.Services;

internal class TelegramUpdateRouter
{
    private readonly IMediator _mediator;
    private readonly UserCultureResolver _userCultureResolver;
    private readonly CultureContextManager _cultureContextManager;
    private readonly WizardStateService _states;
    private readonly ITelegramBotClient _bot;
    private readonly DevNotifier _devNotifier;
    private readonly StorageService _storage;

    public TelegramUpdateRouter(
        IMediator mediator,
        UserCultureResolver userCultureResolver,
        CultureContextManager cultureContextManager,
        WizardStateService states,
        ITelegramBotClient bot,
        DevNotifier devNotifier,
        StorageService storage)
    {
        _mediator = mediator;
        _userCultureResolver = userCultureResolver;
        _cultureContextManager = cultureContextManager;
        _states = states;
        _bot = bot;
        _devNotifier = devNotifier;
        _storage = storage;
    }

    public async Task Update(
        ITelegramBotClient bot,
        Update update,
        CancellationToken ct)
    {
        if (update.Message == null)
            return;

        var msg = update.Message;
        var chatId = msg.Chat.Id;

        // Migration.
        // Group migrating to a supergroup or similar transformation.
        if (msg.MigrateToChatId is long newChatId)
        {
            _storage.MigrateChatId(chatId, newChatId);
            _states.MigrateChatId(chatId, newChatId);

            //return; In case of system message has been merged with the regular message let the logic to continue.
        }
        // This message belongs to the new supergroup and references the old group.
        if (msg.MigrateFromChatId is long oldChatId)
        {
            _storage.MigrateChatId(oldChatId, chatId);
            _states.MigrateChatId(oldChatId, chatId);

            //return; In case of system message has been merged with the regular message let the logic to continue.
        }

        var from = msg.From;
        if (from == null)
        {
            await _bot.SendMessage(
                chatId,
                Resources.BotMessages.ErrorMessageFromIsNull,
                disableNotification: true,
                cancellationToken: ct);
            await _devNotifier.Notify(
                $"From is null in message \"{msg.ToString()}\".");

            return;
        }

        using (_cultureContextManager.Use(_userCultureResolver.Resolve(from.LanguageCode)))
        {
            // New members joined.
            if (msg.NewChatMembers?.Any() == true)
            {
                foreach (var joinedUser in msg.NewChatMembers)
                {
                    await _mediator.Send(
                        new NewMemberJoinedCommand(
                            chatId,
                            msg.Id,
                            joinedUser.Id,
                            joinedUser.FirstName),
                        cancellationToken: ct);
                }

                return;
            }

            if (msg.Text == null)
                return;

            var ephemeralMessageParameters = new EphemeralMessageParameters { ReceiverUserId = from.Id };
            var replyParameters = new ReplyParameters { EphemeralMessageId = msg.EphemeralMessageId };

            // Cancel command.
            if (msg.Text.StartsWith($"/{BotCommands.Cancel}"))
            {
                _states.Remove(
                    chatId,
                    from.Id);

                await bot.SendMessage(
                    chatId,
                    Resources.BotMessages.Cancelled,
                    ephemeralMessageParameters: ephemeralMessageParameters,
                    replyParameters: replyParameters,
                    disableNotification: true,
                    cancellationToken: ct);

                return;
            }

            // Add welcome command.
            if (msg.Text.StartsWith($"/{BotCommands.AddWelcome}"))
            {
                await _mediator.Send(
                    new StartAddWelcomeCommand(
                        chatId,
                        msg.Chat.Type,
                        from.Id,
                        msg.EphemeralMessageId),
                    cancellationToken: ct);

                return;
            }
            var processed = await _mediator.Send(
                new AddWelcomeWizardCommand(
                    msg.Chat.Id,
                    from.Id,
                    msg.EphemeralMessageId,
                    msg.Text,
                    msg.Entities,
                    msg.Caption,
                    msg.CaptionEntities,
                    msg.Photo,
                    msg.Animation,
                    msg.Video),
                cancellationToken: ct);

            // Fill in missing welcome FileIDs.
            if (msg.Text.StartsWith($"/{BotCommands.FillWelcomeFileIds}"))
            {
                await _mediator.Send(
                    new FillWelcomeFileIdsCommand(
                        chatId,
                        msg.Chat.Type,
                        from.Id,
                        msg.EphemeralMessageId),
                    cancellationToken: ct);

                return;
            }

            if (processed)
                return;
        }
    }

    public Task Error(
        ITelegramBotClient bot,
        Exception ex,
        CancellationToken ct)
    {
        Console.WriteLine(ex);

        return _devNotifier.Notify(
            ex);
    }
}
