using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Core;

public sealed class AreaService
{
    private readonly JsonFileStore<AreaData> _store;
    public AreaData Current { get; }

    public AreaService(FileLogger logger, string? dataDirectory = null)
    {
        _store = new JsonFileStore<AreaData>("areas.json", logger, dataDirectory);
        Current = _store.Load(() => new AreaData());
        Current.Areas ??= new List<Area>();
        
        if (Current.Areas.Count == 0)
        {
            Current.Areas.AddRange(new[]
            {
                new Area { Id = "garten", Name = "Garten", Description = "Ein friedlicher Garten direkt vor Dinos Haus.", MinLevel = 1, DigSiteVisual = DigSiteVisualType.Earth },
                new Area { Id = "wald", Name = "Wald", Description = "Ein großer Wald mit vielen Geheimnissen.", MinLevel = 5, DigSiteVisual = DigSiteVisualType.MossAndRoots },
                new Area { Id = "strand", Name = "Strand", Description = "Hier gibt es Muscheln und viel Sand.", MinLevel = 10, DigSiteVisual = DigSiteVisualType.Sand },
                new Area { Id = "hoehle", Name = "Höhle", Description = "Tief unter der Erde leuchten seltsame Kristalle.", MinLevel = 15, DigSiteVisual = DigSiteVisualType.StonesAndCrystals },
                new Area { Id = "schneeland", Name = "Schneegebiet", Description = "Zieh dich warm an!", MinLevel = 20, DigSiteVisual = DigSiteVisualType.Snow }
            });
        }

        foreach (var area in Current.Areas)
        {
            area.DigSiteVisual = area.Id switch
            {
                "wald" => DigSiteVisualType.MossAndRoots,
                "strand" => DigSiteVisualType.Sand,
                "hoehle" => DigSiteVisualType.StonesAndCrystals,
                "schneeland" => DigSiteVisualType.Snow,
                _ => DigSiteVisualType.Earth
            };
        }

        if (!string.IsNullOrWhiteSpace(Current.SelectedAreaId) &&
            Current.Areas.All(area => area.Id != Current.SelectedAreaId))
            Current.SelectedAreaId = Current.Areas.First().Id;
        _store.Save(Current);
    }

    public Area? SelectedArea => Current.Areas.FirstOrDefault(area => area.Id == Current.SelectedAreaId);

    public bool SelectArea(string id, int currentLevel)
    {
        var area = Current.Areas.FirstOrDefault(candidate => candidate.Id == id);
        if (area is null || area.MinLevel > currentLevel) return false;
        Current.SelectedAreaId = area.Id;
        _store.Save(Current);
        return true;
    }

    public void DisableArea()
    {
        Current.SelectedAreaId = "";
        _store.Save(Current);
    }
}
