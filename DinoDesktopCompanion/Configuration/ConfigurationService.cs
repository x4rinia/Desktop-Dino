using System.Text.Json;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Configuration;

public sealed class ConfigurationService
{
    private readonly FileLogger _logger;
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "Data", "config.json");
    public AppConfig Current { get; private set; } = new();
    public event EventHandler? Changed;

    public ConfigurationService(FileLogger logger) => _logger = logger;

    public void Load()
    {
        try
        {
            if (File.Exists(_path)) Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path)) ?? new AppConfig();
            if (Current.QuietMode)
            {
                Current.QuietMode = false;
                Save();
                _logger.Info("Veralteter Ruhemodus wurde deaktiviert.");
            }
            _logger.Info("Konfiguration geladen.");
        }
        catch (Exception ex) { _logger.Error("Konfiguration konnte nicht geladen werden.", ex); }
    }

    public void Save(bool notify = false)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
            if (notify) Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { _logger.Error("Konfiguration konnte nicht gespeichert werden.", ex); }
    }
}
