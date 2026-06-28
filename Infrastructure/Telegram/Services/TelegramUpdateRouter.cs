using MediatR;

using Telegram.Bot;
using Telegram.Bot.Types;

using TelegramClubWelcomeBot.Constants;
using TelegramClubWelcomeBot.Features.AddWelcome.Commands;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Features.NewMember.Commands;
using TelegramClubWelcomeBot.Infrastructure.Localization.Services;
using TelegramClubWelcomeBot.Infrastructure.Notifications;

namespace TelegramClubWelcomeBot.Infrastructure.Telegram.Services;

internal class TelegramUpdateRouter
{
    private readonly IMediator _mediator;
    private readonly UserCultureResolver _userCultureResolver;
    private readonly CultureContextManager _cultureContextManager;
    private readonly WizardStateService _states;
    private readonly ITelegramBotClient _bot;
    private readonly DevNotifier _devNotifier;

    public TelegramUpdateRouter(
        IMediator mediator,
        UserCultureResolver userCultureResolver,
        CultureContextManager cultureContextManager,
        WizardStateService states,
        ITelegramBotClient bot,
        DevNotifier devNotifier)
    {
        _mediator = mediator;
        _userCultureResolver = userCultureResolver;
        _cultureContextManager = cultureContextManager;
        _states = states;
        _bot = bot;
        _devNotifier = devNotifier;
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

        var from = msg.From;
        if (from == null)
        {
            await _bot.SendMessage(
                chatId,
                Resources.BotMessages.ErrorMessageFromIsNull,
                disableNotification: true);
            await _devNotifier.Notify(
                $"From is null in message \"{msg.ToString()}\".");

            return;
        }

        using (_cultureContextManager.Use(_userCultureResolver.Resolve(from.LanguageCode)))
        {
            // Cancel command.
            if (msg.Text == $"/{BotCommands.Cancel}")
            {
                _states.Remove(
                    chatId,
                    from.Id);

                await bot.SendMessage(
                    chatId,
                    Resources.BotMessages.Cancelled,
                    disableNotification: true);

                return;
            }

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
                            joinedUser.FirstName));
                }

                return;
            }

            // Add welcome command.
            if (msg.Text == $"/{BotCommands.AddWelcome}")
            {
                await _mediator.Send(
                    new StartAddWelcomeCommand(
                        chatId,
                        msg.Chat.Type,
                        from.Id));

                return;
            }
            var processed = await _mediator.Send(
                new AddWelcomeWizardCommand(
                    msg.Chat.Id,
                    from.Id,
                    msg.Text,
                    msg.Entities,
                    msg.Caption,
                    msg.CaptionEntities,
                    msg.Photo,
                    msg.Animation,
                    msg.Video));

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
