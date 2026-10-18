using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using TelegramClubWelcomeBot.Infrastructure.Persistence.Models;

namespace TelegramClubWelcomeBot.Infrastructure.Persistence.Services;

internal class StorageService
{
    private readonly string _dataPath;
    private readonly object _lock = new();

    private static JsonSerializerSettings JsonSettings => new()
    {
        StringEscapeHandling = StringEscapeHandling.Default, // Keep emojis and special characters as is.
        Converters = [new StringEnumConverter(new SnakeCaseNamingStrategy())] // Telegram entities support.
    };

    public BotData Data { get; private set; }

    public StorageService()
    {
        _dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "data.json");

        var directoryName = Path.GetDirectoryName(_dataPath);
        if (directoryName == null)
            throw new InvalidOperationException("Cannot determine data directory.");

        Directory.CreateDirectory(directoryName);

        Data = LoadInternal();
    }

    public void CleanupRecentJoins(int retentionMins)
    {
        lock (_lock)
        {
            var border = DateTime.UtcNow.AddMinutes(-retentionMins);

            Data.RecentJoins.RemoveAll(x => x.JoinedAtUtc <= border);

            Save();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            File.WriteAllText(
            _dataPath,
            JsonConvert.SerializeObject(
                Data,
                Formatting.Indented,
                JsonSettings));
        }
    }

    private BotData LoadInternal()
    {
        if (!File.Exists(_dataPath))
            return new BotData();

        var data = JsonConvert.DeserializeObject<BotData>(
            File.ReadAllText(_dataPath),
            JsonSettings);

        return data ?? new BotData();
    }

    public void MigrateChatId(long oldChatId, long newChatId)
    {
        if (oldChatId == newChatId)
            return;

        lock (_lock)
        {
            // Migrate welcome settings.
            if (Data.WelcomesByChatId.Remove(oldChatId, out var oldWelcomes))
            {
                if (!Data.WelcomesByChatId.TryGetValue(newChatId, out var newWelcomes))
                {
                    Data.WelcomesByChatId[newChatId] = oldWelcomes;
                }
                else
                {
                    foreach (var welcome in oldWelcomes)
                    {
                        var isDuplicate = newWelcomes.Any(existing =>
                            existing.Name == welcome.Name &&
                            existing.Url == welcome.Url &&
                            existing.FileId == welcome.FileId &&
                            existing.Type == welcome.Type &&
                            existing.Message == welcome.Message);

                        if (!isDuplicate)
                            newWelcomes.Add(welcome);
                    }
                }
            }

            // Migrate pending welcomes.
            foreach (var pending in Data.PendingWelcomes
                .Where(x => x.ChatId == oldChatId)
                .ToList())
            {
                var isDuplicate = Data.PendingWelcomes.FirstOrDefault(x =>
                    x.ChatId == newChatId &&
                    x.UserId == pending.UserId);

                if (isDuplicate is null)
                {
                    pending.ChatId = newChatId;
                }
                else
                {
                    // Prefer the pending welcome already associated with the new chat.
                    Data.PendingWelcomes.Remove(pending);
                }
            }

            // Migrate recent joins, preserving the most recent timestamp.
            foreach (var recentJoin in Data.RecentJoins
                .Where(x => x.ChatId == oldChatId)
                .ToList())
            {
                var existing = Data.RecentJoins.FirstOrDefault(x =>
                    x.ChatId == newChatId &&
                    x.UserId == recentJoin.UserId);

                if (existing is null)
                {
                    recentJoin.ChatId = newChatId;
                }
                else
                {
                    if (recentJoin.JoinedAtUtc > existing.JoinedAtUtc)
                        existing.JoinedAtUtc = recentJoin.JoinedAtUtc;

                    Data.RecentJoins.Remove(recentJoin);
                }
            }

            Save();
        }
    }
}
