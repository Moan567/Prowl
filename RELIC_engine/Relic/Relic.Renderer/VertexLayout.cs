namespace Relic.Renderer;

public enum VertexAttributeType
{
    Float,
    Float2,
    Float3,
    Float4,
    Byte4,
    UByte4,
    Short2,
    Short4,
    Int,
    UInt
}

public enum VertexAttributeUsage
{
    Position,
    Normal,
    Tangent,
    Bitangent,
    TexCoord,
    Color,
    BoneIndices,
    BoneWeights
}

public readonly struct VertexAttribute
{
    public readonly VertexAttributeUsage Usage;
    public readonly VertexAttributeType Type;
    public readonly int Offset;
    public readonly int Index;

    public VertexAttribute(VertexAttributeUsage usage, VertexAttributeType type, int offset, int index = 0)
    {
        Usage = usage;
        Type = type;
        Offset = offset;
        Index = index;
    }

    public int Size => Type switch
    {
        VertexAttributeType.Float => 4,
        VertexAttributeType.Float2 => 8,
        VertexAttributeType.Float3 => 12,
        VertexAttributeType.Float4 => 16,
        VertexAttributeType.Byte4 => 4,
        VertexAttributeType.UByte4 => 4,
        VertexAttributeType.Short2 => 4,
        VertexAttributeType.Short4 => 8,
        VertexAttributeType.Int => 4,
        VertexAttributeType.UInt => 4,
        _ => 0
    };
}

public sealed class VertexLayout
{
    private readonly List<VertexAttribute> _attributes = new();
    public int Stride { get; private set; }

    public IReadOnlyList<VertexAttribute> Attributes => _attributes;

    public VertexLayout Add(VertexAttributeUsage usage, VertexAttributeType type, int index = 0)
    {
        var attr = new VertexAttribute(usage, type, Stride, index);
        _attributes.Add(attr);
        Stride += attr.Size;
        return this;
    }

    public static VertexLayout Default => new VertexLayout()
        .Add(VertexAttributeUsage.Position, VertexAttributeType.Float3)
        .Add(VertexAttributeUsage.Normal, VertexAttributeType.Float3)
        .Add(VertexAttributeUsage.Tangent, VertexAttributeType.Float3)
        .Add(VertexAttributeUsage.TexCoord, VertexAttributeType.Float2);

    public static VertexLayout PosNormalTex => new VertexLayout()
        .Add(VertexAttributeUsage.Position, VertexAttributeType.Float3)
        .Add(VertexAttributeUsage.Normal, VertexAttributeType.Float3)
        .Add(VertexAttributeUsage.TexCoord, VertexAttributeType.Float2);
}