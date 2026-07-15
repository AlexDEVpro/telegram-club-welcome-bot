using TelegramClubWelcomeBot.Features.AddWelcome.Models;
using TelegramClubWelcomeBot.Features.NewMember.Models;

namespace TelegramClubWelcomeBot.Infrastructure.Persistence.Models
{
    internal class BotData
    {
        private Dictionary<long, List<Welcome>>? _welcomesByChatId;
        private List<RecentJoin>? _recentJoins;

        public Dictionary<long, List<Welcome>> WelcomesByChatId
        {
            get => _welcomesByChatId ??= [];
            set => _welcomesByChatId = value;
        }

        public List<RecentJoin> RecentJoins
        {
            get => _recentJoins ??= [];
            set => _recentJoins = value;
        }

        public void AddWelcome(long chatId, Welcome welcome)
        {
            if (!WelcomesByChatId.TryGetValue(
            chatId,
            out var welcomes))
            {
                welcomes = [];
                WelcomesByChatId[chatId] = welcomes;
            }

            welcomes.Add(welcome);
        }

        public List<Welcome> GetWelcomes(long chatId)
        {
            return WelcomesByChatId.TryGetValue(
                chatId,
                out var welcomes)
                    ? welcomes
                    : [];
        }
    }
}
