using MediatR;

namespace TelegramClubWelcomeBot.Features.NewMember.Commands;

internal record NewMemberJoinedCommand(
    long ChatId,
    int JoinMessageId,
    long JoinedUserId,
    string JoinedUserFirstName) : IRequest;
