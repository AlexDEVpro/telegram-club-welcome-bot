using MediatR;

using TelegramClubWelcomeBot.Features.NewMember.Commands;
using TelegramClubWelcomeBot.Features.NewMember.Services;

namespace TelegramClubWelcomeBot.Features.NewMember.Handlers;

internal class NewMemberJoinedHandler
: IRequestHandler<NewMemberJoinedCommand>
{
    private readonly PendingWelcomeProcessor _processor;

    public NewMemberJoinedHandler(
        PendingWelcomeProcessor processor)
    {
        _processor = processor;
    }

    public async Task Handle(
        NewMemberJoinedCommand request,
        CancellationToken cancellationToken)
    {
        await _processor.HandleJoin(
            request.ChatId,
            request.JoinMessageId,
            request.JoinedUserId,
            request.JoinedUserFirstName,
            cancellationToken);
    }
}
