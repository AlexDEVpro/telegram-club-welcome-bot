using System.Text.Json.Serialization;

namespace TelegramClubWelcomeBot.Features.AddWelcome.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum WelcomeType
{
    Video,
    Animation,
    Image
}
