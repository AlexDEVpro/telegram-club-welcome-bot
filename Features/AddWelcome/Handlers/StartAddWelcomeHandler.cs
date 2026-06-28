using MediatR;

using Telegram.Bot;
using Telegram.Bot.Types.Enums;

using TelegramClubWelcomeBot.Features.AddWelcome.Commands;
using TelegramClubWelcomeBot.Features.AddWelcome.Models;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Resources;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Handlers;

internal class StartAddWelcomeHandler
: IRequestHandler<StartAddWelcomeCommand>
{

    private readonly WizardStateService _states;
    private readonly ITelegramBotClient _bot;

    public StartAddWelcomeHandler(
        WizardStateService states,
        ITelegramBotClient bot)
    {
        _states = states;
        _bot = bot;
    }

    public async Task Handle(
        StartAddWelcomeCommand request,
        CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        var chatType = request.ChatType;
        var fromUserId = request.FromUserId;

        if (chatType is not (ChatType.Group or ChatType.Supergroup))
        {
            await _bot.SendMessage(
                chatId,
                BotMessages.AddWelcomeMustBeGroup,
                disableNotification: true,
                cancellationToken: cancellationToken);

            return;
        }

        var member = await _bot.GetChatMember(
                chatId,
                fromUserId,
                cancellationToken);

        if (member.Status != ChatMemberStatus.Creator)
        {
            await _bot.SendMessage(
                chatId,
                BotMessages.AddWelcomeOwnerOnly,
                disableNotification: true,
                cancellationToken: cancellationToken);

            return;
        }

        _states.Set(
            chatId,
            fromUserId,
            new WizardState
            {
                ChatIdTarget = chatId,
                Step = Step.WaitName
            });

        await _bot.SendMessage(
            chatId,
            BotMessages.SendWelcomeName,
            disableNotification: true,
            cancellationToken: cancellationToken);
    }
}
