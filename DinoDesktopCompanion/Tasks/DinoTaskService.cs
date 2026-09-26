using System.Text.Json;
using System.Text.Json.Serialization;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Tasks;

public sealed class DinoTaskService
{
    private readonly JsonFileStore<TaskProgressData> _store;
    private readonly ProgressService _progress;
    private readonly TaskProgressData _data;
    public IReadOnlyList<DinoTask> Tasks => _data.Tasks;
    public event EventHandler<DinoTaskEventArgs>? TaskStarted;
    public event EventHandler<DinoTaskEventArgs>? TaskCompleted;
    public event EventHandler<DinoTaskEventArgs>? TaskClaimed;

    public DinoTaskService(ProgressService progress, FileLogger logger, string? dataDirectory = null, string? definitionDirectory = null)
    {
        _progress = progress;
        _store = new JsonFileStore<TaskProgressData>("task-progress.json", logger, dataDirectory);
        _data = _store.Load(() => new TaskProgressData());
        MergeDefinitions(LoadDefinitions(definitionDirectory ?? Path.Combine(AppContext.BaseDirectory, "GameData"), logger));
        RefreshDueTasks();
        _store.Save(_data);
    }

    public bool Start(string id, DateTimeOffset? now = null)
    {
        var task = _data.Tasks.FirstOrDefault(t => t.Id == id);
        if (task is null || task.Status != DinoTaskStatus.Available) return false;
        task.Status = DinoTaskStatus.Running;
        task.StartedAt = now ?? DateTimeOffset.Now;
        task.CompletedAt = null;
        _store.Save(_data);
        TaskStarted?.Invoke(this, new DinoTaskEventArgs(task));
        return true;
    }

    public int RefreshDueTasks(DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.Now;
        var completed = _data.Tasks.Where(t => t.Status == DinoTaskStatus.Running && t.StartedAt.HasValue && t.StartedAt.Value + t.Duration <= timestamp).ToArray();
        foreach (var task in completed)
        {
            task.Status = DinoTaskStatus.Completed;
            task.CompletedAt = timestamp;
            TaskCompleted?.Invoke(this, new DinoTaskEventArgs(task));
        }
        if (completed.Length > 0) _store.Save(_data);
        return completed.Length;
    }

    public bool Claim(string id)
    {
        RefreshDueTasks();
        var task = _data.Tasks.FirstOrDefault(t => t.Id == id);
        if (task is null || task.Status != DinoTaskStatus.Completed) return false;

        // Status zuerst persistieren, damit ein abgebrochener Claim keine doppelten Belohnungen erzeugt.
        task.Status = DinoTaskStatus.Claimed;
        _store.Save(_data);
        if (task.XPReward > 0) _progress.AddXP(task.XPReward, $"Task:{task.Id}");
        if (task.CoinReward > 0) _progress.AddCoins(task.CoinReward, $"Task:{task.Id}");
        TaskClaimed?.Invoke(this, new DinoTaskEventArgs(task));
        return true;
    }

    private void MergeDefinitions(IEnumerable<DinoTask> definitions)
    {
        foreach (var definition in definitions)
        {
            var existing = _data.Tasks.FirstOrDefault(t => t.Id == definition.Id);
            if (existing is null) { _data.Tasks.Add(definition); continue; }
            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.Duration = definition.Duration;
            existing.XPReward = definition.XPReward;
            existing.CoinReward = definition.CoinReward;
        }
    }

    private static IReadOnlyList<DinoTask> LoadDefinitions(string definitionDirectory, FileLogger logger)
    {
        try
        {
            var path = Path.Combine(definitionDirectory, "tasks.json");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            return File.Exists(path) ? JsonSerializer.Deserialize<List<DinoTask>>(File.ReadAllText(path), options) ?? [] : [];
        }
        catch (Exception ex) { logger.Error("Aufgabendefinitionen konnten nicht geladen werden.", ex); return []; }
    }
}

public sealed class DinoTaskEventArgs : EventArgs
{
    public DinoTask Task { get; }
    public DinoTaskEventArgs(DinoTask task) => Task = task;
}
