namespace Relic.Physics;

using Relic.Content;
using Relic.Framework;

public struct CollisionPlane
{
    public Vector3 Normal;
    public float Distance; // n·p = d

    public CollisionPlane(Vector3 n, float d) { Normal = n; Distance = d; }
    public float SignedDistance(Vector3 p) => Vector3.Dot(Normal, p) - Distance;
}

public struct SweepResult
{
    public bool Hit;
    public float Fraction; // 0..1, 1 = no hit
    public Vector3 HitNormal;
    public Vector3 HitPoint;
    public int HitBrushIndex;

    public static SweepResult NoHit => new() { Hit = false, Fraction = 1f };
}

public struct RaycastResult
{
    public bool Hit;
    public float Distance;
    public Vector3 Point;
    public Vector3 Normal;
    public int HitBrushIndex;
}

/// <summary>
/// Static brush collision world generated directly from MapData brushes.
/// No BSP, no navmesh, no external physics engine. Planes are outward normals.
/// </summary>
public sealed class PhysicsWorld
{
    // Each brush is convex: list of outward planes
    private readonly List<List<CollisionPlane>> _brushes = new();
    private readonly List<AABB> _brushBounds = new();

    public int BrushCount => _brushes.Count;
    public IReadOnlyList<AABB> BrushBounds => _brushBounds;

    private const float Epsilon = 0.001f;

    public static PhysicsWorld FromMapData(MapData map)
    {
        var world = new PhysicsWorld();
        foreach (var brush in map.Brushes)
        {
            if (brush.Faces.Count < 4) continue;
            var planes = new List<CollisionPlane>(brush.Faces.Count);
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);

            Vector3 centroid = Vector3.Zero;
            int cCount = 0;
            foreach (var f in brush.Faces)
                foreach (var v in f.Vertices) { centroid += v; cCount++; }
            if (cCount > 0) centroid /= cCount;

            foreach (var f in brush.Faces)
            {
                Vector3 n = f.Normal;
                if (n.LengthSquared() < 1e-6f) continue;
                n.Normalize();
                float d = Vector3.Dot(n, f.Vertices[0]);
                // Ensure outward: centroid should be behind plane (sd <0). If in front, flip.
                float sdCentroid = Vector3.Dot(n, centroid) - d;
                if (sdCentroid > 0)
                {
                    n = -n;
                    d = -d;
                }
                planes.Add(new CollisionPlane(n, d));
            }

            // Estimate bounds from face vertices for broadphase
            foreach (var f in brush.Faces)
            {
                foreach (var v in f.Vertices)
                {
                    min.X = MathF.Min(min.X, v.X);
                    min.Y = MathF.Min(min.Y, v.Y);
                    min.Z = MathF.Min(min.Z, v.Z);
                    max.X = MathF.Max(max.X, v.X);
                    max.Y = MathF.Max(max.Y, v.Y);
                    max.Z = MathF.Max(max.Z, v.Z);
                }
            }
            // Expand slightly for numerical robustness
            min -= new Vector3(0.5f);
            max += new Vector3(0.5f);

            // Validate convex by checking all vertices inside all planes (with tolerance)
            // If brush is malformed skip
            world._brushes.Add(planes);
            world._brushBounds.Add(new AABB(min, max));
        }
        return world;
    }

    public bool IsPointInsideBrush(Vector3 p, int brushIndex)
    {
        foreach (var pl in _brushes[brushIndex])
            if (pl.SignedDistance(p) > Epsilon) return false;
        return true;
    }

    public bool IsAABBInsideBrush(AABB box, int brushIndex)
    {
        // Check if AABB is fully inside brush (for penetration test)
        // For collision we test expanded planes vs center; here test corners? Simpler: test center expanded checked via Sweep logic.
        // For IsColliding we test if box intersects solid: box center vs expanded brush
        Vector3 center = box.Center;
        Vector3 half = box.HalfExtents;
        foreach (var pl in _brushes[brushIndex])
        {
            float expandedDist = Vector3.Dot(new Vector3(MathF.Abs(pl.Normal.X), MathF.Abs(pl.Normal.Y), MathF.Abs(pl.Normal.Z)), half);
            float sd = pl.SignedDistance(center);
            if (sd > expandedDist + Epsilon) return false; // outside this plane -> not inside
        }
        return true;
    }

    public bool CheckAABBCollision(AABB box)
    {
        for (int i = 0; i < _brushes.Count; i++)
        {
            // Broadphase
            if (!box.Intersects(_brushBounds[i])) continue;
            if (IsAABBInsideBrush(box, i)) return true;
        }
        return false;
    }

    // Raycast from start in direction dir (normalized) up to maxDist
    public RaycastResult Raycast(Vector3 start, Vector3 dir, float maxDist)
    {
        Vector3 end = start + dir * maxDist;
        // Treat as zero-extent sweep
        var sweep = SweepAABB(start, end, Vector3.Zero);
        if (!sweep.Hit) return new RaycastResult { Hit = false };
        float dist = sweep.Fraction * maxDist;
        return new RaycastResult { Hit = true, Distance = dist, Point = sweep.HitPoint, Normal = sweep.HitNormal, HitBrushIndex = sweep.HitBrushIndex };
    }

    // Sweep AABB from startCenter to endCenter with given halfExtents (player hull)
    // If halfExtents == Zero -> point sweep
    public SweepResult SweepAABB(Vector3 start, Vector3 end, Vector3 halfExtents)
    {
        // Early out if no movement
        Vector3 delta = end - start;
        if (delta.LengthSquared() < 1e-8f)
        {
            // Check static overlap
            var box = AABB.FromCenterExtents(start, halfExtents);
            for (int i = 0; i < _brushes.Count; i++)
            {
                if (!box.Intersects(_brushBounds[i])) continue;
                if (IsAABBInsideBrush(box, i))
                    return new SweepResult { Hit = true, Fraction = 0f, HitNormal = -delta, HitPoint = start, HitBrushIndex = i };
            }
            return SweepResult.NoHit;
        }

        SweepResult best = SweepResult.NoHit;
        float bestFraction = 1f;

        for (int i = 0; i < _brushes.Count; i++)
        {
            // Broadphase: swept AABB bounds
            Vector3 sweepMin = new(
                MathF.Min(start.X - halfExtents.X, end.X - halfExtents.X),
                MathF.Min(start.Y - halfExtents.Y, end.Y - halfExtents.Y),
                MathF.Min(start.Z - halfExtents.Z, end.Z - halfExtents.Z));
            Vector3 sweepMax = new(
                MathF.Max(start.X + halfExtents.X, end.X + halfExtents.X),
                MathF.Max(start.Y + halfExtents.Y, end.Y + halfExtents.Y),
                MathF.Max(start.Z + halfExtents.Z, end.Z + halfExtents.Z));
            var sweepBounds = new AABB(sweepMin, sweepMax);
            if (!sweepBounds.Intersects(_brushBounds[i])) continue;

            var hit = SweepPointAgainstExpandedBrush(start, delta, halfExtents, _brushes[i]);
            if (hit.Hit && hit.Fraction < bestFraction && hit.Fraction >= 0f)
            {
                bestFraction = hit.Fraction;
                best = hit;
                best.HitBrushIndex = i;
                if (bestFraction <= 0f) break; // cannot get earlier
            }
        }
        return best;
    }

    private static SweepResult SweepPointAgainstExpandedBrush(Vector3 start, Vector3 delta, Vector3 half, List<CollisionPlane> planes)
    {
        float tEnter = 0f;
        float tExit = 1f;
        Vector3 hitNormal = Vector3.Zero;
        bool hasHitNormal = false;

        for (int i = 0; i < planes.Count; i++)
        {
            var pl = planes[i];
            Vector3 absN = new(MathF.Abs(pl.Normal.X), MathF.Abs(pl.Normal.Y), MathF.Abs(pl.Normal.Z));
            float expandedDist = Vector3.Dot(absN, half);
            float planeDist = pl.Distance + expandedDist;

            float sdStart = Vector3.Dot(pl.Normal, start) - planeDist;
            float denom = Vector3.Dot(pl.Normal, delta);

            if (MathF.Abs(denom) < 1e-8f)
            {
                if (sdStart > 0) // parallel and outside
                    return SweepResult.NoHit;
                continue;
            }

            float t = -sdStart / denom;
            if (denom < 0) // entering
            {
                if (t > tEnter)
                {
                    tEnter = t;
                    hitNormal = pl.Normal;
                    hasHitNormal = true;
                }
            }
            else // exiting
            {
                if (t < tExit) tExit = t;
            }

            if (tEnter > tExit) return SweepResult.NoHit;
        }

        if (tEnter < 0f) tEnter = 0f;
        if (tEnter > 1f) return SweepResult.NoHit;
        if (!hasHitNormal && tEnter == 0f)
        {
            // Started inside expanded brush - find closest surface to push out (largest sd)
            float bestSd = float.NegativeInfinity;
            Vector3 bestN = Vector3.Zero;
            for (int i = 0; i < planes.Count; i++)
            {
                var pl = planes[i];
                Vector3 absN = new(MathF.Abs(pl.Normal.X), MathF.Abs(pl.Normal.Y), MathF.Abs(pl.Normal.Z));
                float expandedDist = Vector3.Dot(absN, half);
                float planeDist = pl.Distance + expandedDist;
                float sd = Vector3.Dot(pl.Normal, start) - planeDist;
                if (sd > bestSd) { bestSd = sd; bestN = pl.Normal; }
            }
            if (bestSd <= Epsilon)
            {
                // Only block if moving into the surface (penetration normal opposes motion)
                if (Vector3.Dot(bestN, delta) < -1e-6f)
                    return new SweepResult { Hit = true, Fraction = 0f, HitNormal = bestN, HitPoint = start };
                return SweepResult.NoHit;
            }
            return SweepResult.NoHit;
        }

        Vector3 p2 = start + delta * tEnter;
        return new SweepResult { Hit = true, Fraction = tEnter, HitNormal = hitNormal, HitPoint = p2 };
    }

    public bool IsOnGround(Vector3 center, Vector3 halfExtents, float groundCheckDistance = 2f)
    {
        // Cast slightly down
        Vector3 start = center;
        Vector3 end = center - new Vector3(0, 0, groundCheckDistance + halfExtents.Z);
        // Actually we want a sweep down by small distance; use expanded check
        // Simpler: sweep AABB down a short distance and see if hit normal is upward (Z >= 0.7)
        var res = SweepAABB(start, start - new Vector3(0, 0, groundCheckDistance), halfExtents);
        if (!res.Hit) return false;
        // Ground if hit normal Z is sufficiently upward (floor, not wall)
        return res.HitNormal.Z > 0.7f;
    }

    public float GroundDistance(Vector3 center, Vector3 halfExtents)
    {
        var res = SweepAABB(center, center - new Vector3(0, 0, 1000f), halfExtents);
        if (!res.Hit) return float.PositiveInfinity;
        return res.Fraction * 1000f;
    }
}
