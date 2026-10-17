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

    public void MigrateChatId(long oldChatId, long newChatId)
    {
        if (oldChatId == newChatId)
            return;

        foreach (var stateKeyValuePair in _states
            .Where(x => x.Key.Item1 == oldChatId)
            .ToList())
        {
            var oldKey = stateKeyValuePair.Key;
            var newKey = (newChatId, oldKey.Item2);

            _states.Remove(oldKey);

            // If a state already exists for the new chat and this user,
            // preserve the state belonging to the new chat.
            if (_states.ContainsKey(newKey))
                continue;

            // Update existing state with new chat ID.
            stateKeyValuePair.Value.ChatIdTarget = newChatId;
            _states[newKey] = stateKeyValuePair.Value;
        }
    }
}
