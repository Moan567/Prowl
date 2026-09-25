namespace Relic.Renderer;

using Relic.Framework;

public sealed class Frustum
{
    private readonly Plane[] _planes = new Plane[6];

    public struct Plane { public Vector3 Normal; public float Distance; public float SignedDistance(Vector3 p) => Vector3.Dot(Normal, p) - Distance; }

    public static Frustum FromViewProjection(Matrix4x4 vp)
    {
        var f = new Frustum();
        // Extract planes from VP matrix (simplified)
        // Left
        f._planes[0] = new Plane { Normal = new Vector3(vp.M41 + vp.M11, vp.M42 + vp.M12, vp.M43 + vp.M13), Distance = vp.M44 + vp.M14 };
        // Right
        f._planes[1] = new Plane { Normal = new Vector3(vp.M41 - vp.M11, vp.M42 - vp.M12, vp.M43 - vp.M13), Distance = vp.M44 - vp.M14 };
        // Bottom
        f._planes[2] = new Plane { Normal = new Vector3(vp.M41 + vp.M21, vp.M42 + vp.M22, vp.M43 + vp.M23), Distance = vp.M44 + vp.M24 };
        // Top
        f._planes[3] = new Plane { Normal = new Vector3(vp.M41 - vp.M21, vp.M42 - vp.M22, vp.M43 - vp.M23), Distance = vp.M44 - vp.M24 };
        // Near
        f._planes[4] = new Plane { Normal = new Vector3(vp.M41 + vp.M31, vp.M42 + vp.M32, vp.M43 + vp.M33), Distance = vp.M44 + vp.M34 };
        // Far
        f._planes[5] = new Plane { Normal = new Vector3(vp.M41 - vp.M31, vp.M42 - vp.M32, vp.M43 - vp.M33), Distance = vp.M44 - vp.M34 };
        for (int i = 0; i < 6; i++)
        {
            float len = f._planes[i].Normal.Length();
            if (len > 0.001f) { f._planes[i].Normal /= len; f._planes[i].Distance /= len; }
        }
        return f;
    }

    public bool IsBoxVisible(Vector3 min, Vector3 max)
    {
        foreach (var p in _planes)
        {
            Vector3 positive = new(
                p.Normal.X >= 0 ? max.X : min.X,
                p.Normal.Y >= 0 ? max.Y : min.Y,
                p.Normal.Z >= 0 ? max.Z : min.Z);
            if (p.SignedDistance(positive) < 0) return false;
        }
        return true;
    }

    public bool IsSphereVisible(Vector3 center, float radius)
    {
        foreach (var p in _planes)
        {
            if (p.SignedDistance(center) < -radius) return false;
        }
        return true;
    }

    public bool IsPointVisible(Vector3 point)
    {
        foreach (var p in _planes) if (p.SignedDistance(point) < 0) return false;
        return true;
    }
}
