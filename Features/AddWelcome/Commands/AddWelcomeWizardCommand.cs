using MediatR;

using Telegram.Bot.Types;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Commands;

internal record AddWelcomeWizardCommand(
    long ChatId,
    long FromUserId,
    int? EphemeralMessageId,
    string? Text,
    MessageEntity[]? Entities,
    string? Caption,
    MessageEntity[]? CaptionEntities,
    PhotoSize[]? Photo,
    Animation? Animation,
    Video? Video) : IRequest<bool>;
