using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.Statistics;

namespace DinoDesktopCompanion.Expeditions;

/// <summary>Lädt alte Expeditionsdaten nur noch kompatibel; das frühere Zeitsystem ist deaktiviert.</summary>
public sealed class ExpeditionService
{
    public ExpeditionData Current { get; }

    public ExpeditionService(FileLogger logger, ProgressService progress, StatisticsService stats, string? dataDirectory = null)
    {
        var store = new JsonFileStore<ExpeditionData>("expeditions.json", logger, dataDirectory);
        Current = store.Load(() => new ExpeditionData());
    }
}
