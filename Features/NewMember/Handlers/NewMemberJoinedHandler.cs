using MediatR;

using TelegramClubWelcomeBot.Features.NewMember.Commands;
using TelegramClubWelcomeBot.Features.NewMember.Services;

namespace TelegramClubWelcomeBot.Features.NewMember.Handlers;

internal class NewMemberJoinedHandler
: IRequestHandler<NewMemberJoinedCommand>
{
    private readonly WelcomeSender _sender;

    public NewMemberJoinedHandler(
        WelcomeSender sender)
    {
        _sender = sender;
    }

    public async Task Handle(
        NewMemberJoinedCommand request,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            request.ChatId,
            request.JoinMessageId,
            request.JoinedUserId,
            request.JoinedUserFirstName);
    }
}
