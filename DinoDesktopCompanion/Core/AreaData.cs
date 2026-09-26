namespace DinoDesktopCompanion.Core;

public sealed class AreaData
{
    public List<Area> Areas { get; set; } = new();
    public string SelectedAreaId { get; set; } = "garten";
}

public sealed class Area
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MinLevel { get; set; } = 1;
    public DigSiteVisualType DigSiteVisual { get; set; } = DigSiteVisualType.Earth;
}

public enum DigSiteVisualType
{
    Earth,
    MossAndRoots,
    Sand,
    StonesAndCrystals,
    Snow
}
