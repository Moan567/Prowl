namespace Relic.Content;

using Relic.Framework;

public sealed class MapEntity
{
    public string ClassName { get; set; } = "";
    public Dictionary<string, string> Properties { get; } = new();
    public Vector3 Position { get; set; }
}