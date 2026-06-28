using MediatR;

using Telegram.Bot.Types.Enums;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Commands;

internal record StartAddWelcomeCommand(
    long ChatId,
    ChatType ChatType,
    long FromUserId) : IRequest;
