namespace Relic.Content;

using Relic.Framework;

public sealed class MapBrush
{
    public List<MapFace> Faces { get; } = new();
    public Dictionary<string, string> Properties { get; } = new();
}

public sealed class MapFace
{
    public Vector3[] Vertices { get; set; } = Array.Empty<Vector3>();
    public Vector3 Normal { get; set; }
    public string Texture { get; set; } = "";
    public Vector2 TextureScale { get; set; } = Vector2.One;
    public float TextureRotation { get; set; }
    public int TextureShiftX { get; set; }
    public int TextureShiftY { get; set; }
}