namespace Relic.Physics;

using Relic.Framework;

/// <summary>
/// Axis-Aligned Bounding Box with Z-up convention (matches TrenchBroom: Z vertical).
/// </summary>
public struct AABB
{
    public Vector3 Min;
    public Vector3 Max;

    public AABB(Vector3 min, Vector3 max) { Min = min; Max = max; }

    public static AABB FromCenterExtents(Vector3 center, Vector3 halfExtents)
        => new(center - halfExtents, center + halfExtents);

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Extents => (Max - Min) * 0.5f;
    public Vector3 HalfExtents => Extents;
    public Vector3 Size => Max - Min;

    public bool Contains(Vector3 p)
        => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

    public bool Intersects(AABB other)
        => Min.X <= other.Max.X && Max.X >= other.Min.X
        && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
        && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    public AABB Translate(Vector3 offset) => new(Min + offset, Max + offset);
}
