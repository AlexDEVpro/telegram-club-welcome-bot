using System.Diagnostics.CodeAnalysis;

using TelegramClubWelcomeBot.Features.AddWelcome.Models;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Services;

internal class WizardStateService
{
    private readonly Dictionary<(long, long), WizardState>
        _states = new();

    public bool TryGet(
        long chatId,
        long userId,
        [NotNullWhen(true)] out WizardState? state)
    {
        return _states.TryGetValue(
            (chatId, userId),
            out state);
    }

    public void Set(
        long chatId,
        long userId,
        WizardState state)
    {
        _states[(chatId, userId)] = state;
    }

    public void Remove(
        long chatId,
        long userId)
    {
        _states.Remove((chatId, userId));
    }
}
