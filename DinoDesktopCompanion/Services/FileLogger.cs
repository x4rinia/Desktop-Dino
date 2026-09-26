namespace DinoDesktopCompanion.Services;

public sealed class FileLogger
{
    private readonly string _path;
    private readonly object _gate = new();
    private const long MaxBytes = 512 * 1024;

    public FileLogger(string? baseDirectory = null)
    {
        var folder = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "dino.log");
        RotateIfNeeded();
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message} {ex.Message}");

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            RotateIfNeeded();
            File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_path) || new FileInfo(_path).Length < MaxBytes) return;
            File.Move(_path, Path.Combine(Path.GetDirectoryName(_path)!, "dino.previous.log"), true);
        }
        catch { }
    }
}
