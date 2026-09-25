namespace Relic.Content;

public sealed class MapData
{
    public List<MapBrush> Brushes { get; } = new();
    public List<MapEntity> Entities { get; } = new();
}