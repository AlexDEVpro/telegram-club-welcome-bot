using Newtonsoft.Json;

using TelegramClubWelcomeBot.Infrastructure.Persistence.Models;

internal class StorageService
{
    private readonly string _dataPath;
    private readonly object _lock = new();

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
                new JsonSerializerSettings
                {
                    StringEscapeHandling =
                        StringEscapeHandling.Default // Keep emojis and special characters as is.
                }));
        }
    }

    private BotData LoadInternal()
    {
        if (!File.Exists(_dataPath))
            return new BotData();

        var data = JsonConvert.DeserializeObject<BotData>(
            File.ReadAllText(_dataPath));

        return data ?? new BotData();
    }
}
