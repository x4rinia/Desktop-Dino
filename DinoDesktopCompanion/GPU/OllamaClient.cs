using System.Net.Http;
using System.Text;
using System.Text.Json;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.GPU;

public sealed class OllamaClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly FileLogger _logger;
    public OllamaClient(FileLogger logger) => _logger = logger;

    public async Task<(bool Success, string Message, string[] Models)> TestAsync(string baseUrl)
    {
        try
        {
            using var response = await _http.GetAsync($"{baseUrl.TrimEnd('/')}/api/tags");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var models = json.RootElement.TryGetProperty("models", out var list)
                ? list.EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "").Where(x => x.Length > 0).ToArray()
                : [];
            return (true, models.Length > 0 ? $"Dino kann antworten. {models.Length} Modell(e) gefunden." : "Die lokale Verbindung antwortet, aber es wurde kein Modell gefunden.", models);
        }
        catch (Exception ex)
        {
            _logger.Error("Ollama-Verbindung fehlgeschlagen.", ex);
            return (false, "Ich kann gerade nicht nachdenken.", []);
        }
    }

    public async Task<string> AskAsync(string baseUrl, string model, string prompt)
    {
        if (string.IsNullOrWhiteSpace(model)) return "Bitte wähle zuerst in den Einstellungen ein lokales Modell aus.";
        try
        {
            var body = JsonSerializer.Serialize(new { model, prompt, stream = false });
            using var response = await _http.PostAsync($"{baseUrl.TrimEnd('/')}/api/generate", new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("response").GetString()?.Trim() ?? "Dino ist gerade sprachlos.";
        }
        catch (Exception ex)
        {
            _logger.Error("Lokale Dino-Anfrage fehlgeschlagen.", ex);
            return "Ich kann gerade nicht antworten.";
        }
    }
}
