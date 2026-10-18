using MediatR;

using Telegram.Bot.Types.Enums;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Commands;

internal record FillWelcomeFileIdsCommand(
    long ChatId,
    ChatType ChatType,
    long FromUserId,
    int? EphemeralMessageId) : IRequest;
