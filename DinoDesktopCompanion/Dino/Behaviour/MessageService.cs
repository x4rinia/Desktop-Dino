using System.Text.Json;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Dino.Behaviour;

public sealed class MessageService
{
    private readonly Dictionary<string, string[]> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();

    public MessageService(FileLogger logger)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Messages.json");
            if (File.Exists(path)) _messages = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path)) ?? _messages;
        }
        catch (Exception ex) { logger.Error("Nachrichten konnten nicht geladen werden.", ex); }
    }

    public string Get(string category)
    {
        if (_messages.TryGetValue(category, out var values) && values.Length > 0) return values[_random.Next(values.Length)];
        return "Ich bin noch da. 🦕";
    }

    public string GetForTimeOfDay()
    {
        var hour = DateTime.Now.Hour;
        return Get(hour < 11 ? "morning" : hour >= 19 ? "evening" : "idle");
    }
}
