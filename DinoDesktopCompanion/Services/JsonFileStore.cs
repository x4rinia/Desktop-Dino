using System.Text.Json;
using System.Text.Json.Serialization;

namespace DinoDesktopCompanion.Services;

/// <summary>Gemeinsame, atomare JSON-Persistenz für voneinander unabhängige Spielsysteme.</summary>
public sealed class JsonFileStore<T> where T : class
{
    private readonly string _path;
    private readonly FileLogger _logger;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonFileStore(string fileName, FileLogger logger, string? dataDirectory = null)
    {
        _path = Path.Combine(dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data"), fileName);
        _logger = logger;
    }

    public T Load(Func<T> createDefault)
    {
        lock (_gate)
        {
            try
            {
                return File.Exists(_path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(_path), _options) ?? createDefault() : createDefault();
            }
            catch (Exception ex)
            {
                _logger.Error($"Datendatei {Path.GetFileName(_path)} konnte nicht geladen werden.", ex);
                return createDefault();
            }
        }
    }

    public void Save(T value)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(value, _options));
                _ = JsonSerializer.Deserialize<T>(File.ReadAllText(temporary), _options)
                    ?? throw new InvalidDataException($"Validierung von {Path.GetFileName(_path)} fehlgeschlagen.");
                
                if (File.Exists(_path))
                {
                    CreateBackup();
                }
                
                File.Move(temporary, _path, true);
            }
            catch (Exception ex) { _logger.Error($"Datendatei {Path.GetFileName(_path)} konnte nicht gespeichert werden.", ex); }
        }
    }
    
    private void CreateBackup()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            var name = Path.GetFileNameWithoutExtension(_path);
            var ext = Path.GetExtension(_path);
            
            // Shift backups
            for (int i = 4; i >= 1; i--)
            {
                var src = Path.Combine(dir, $"{name}_bak{i}{ext}");
                var dest = Path.Combine(dir, $"{name}_bak{i+1}{ext}");
                if (File.Exists(src)) File.Move(src, dest, true);
            }
            
            var latestBak = Path.Combine(dir, $"{name}_bak1{ext}");
            File.Copy(_path, latestBak, true);
        }
        catch (Exception ex) { _logger.Error("Backup konnte nicht erstellt werden.", ex); }
    }
}
