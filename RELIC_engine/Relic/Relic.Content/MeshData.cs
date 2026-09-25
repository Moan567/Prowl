namespace Relic.Content;

using Relic.Framework;

public struct MeshVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Tangent;
    public Vector2 TexCoord;

    public MeshVertex(Vector3 pos, Vector3 normal, Vector2 uv, Vector3 tangent = default)
    {
        Position = pos;
        Normal = normal;
        TexCoord = uv;
        Tangent = tangent;
        if (tangent.LengthSquared() < 0.001f) Tangent = new Vector3(1, 0, 0);
    }

    public static int Stride => 11 * sizeof(float); // 3+3+3+2
}

public sealed class MeshData
{
    public List<MeshVertex> Vertices { get; } = new();
    public List<uint> Indices { get; } = new();

    public bool IsEmpty => Vertices.Count == 0 || Indices.Count == 0;
}

public sealed class GroupedMeshData
{
    public List<MeshGroup> Groups { get; } = new();
    public int TotalVertices => Groups.Sum(g => g.Vertices.Count);
    public int TotalIndices => Groups.Sum(g => g.Indices.Count);
}

public sealed class MeshGroup
{
    public string TextureName { get; set; } = "";
    public List<MeshVertex> Vertices { get; } = new();
    public List<uint> Indices { get; } = new();
}
