namespace Relic.Gameplay;

using Relic.Framework;
using Relic.Physics;

public static class InteractionSystem
{
    public static bool SphereOverlap(Vector3 a, float radiusA, Vector3 b, float radiusB)
        => (a - b).LengthSquared() <= (radiusA + radiusB) * (radiusA + radiusB);

    public static bool AABBOverlap(AABB a, AABB b) => a.Intersects(b);

    public static IEnumerable<Entity> QuerySphere(EntityManager manager, Vector3 center, float radius)
    {
        foreach (var e in manager.Entities)
        {
            float r = 24f; // default entity radius
            if ((e.Transform.Position - center).LengthSquared() <= (radius + r)*(radius+r))
                yield return e;
        }
    }

    public static IEnumerable<Entity> QueryAABB(EntityManager manager, AABB box)
    {
        foreach (var e in manager.Entities)
        {
            var pos = e.Transform.Position;
            var entBox = AABB.FromCenterExtents(pos, new Vector3(16,16,32));
            if (entBox.Intersects(box)) yield return e;
        }
    }

    // World sphere vs brush
    public static bool WorldOverlapSphere(PhysicsWorld world, Vector3 center, float radius)
        => world.CheckAABBCollision(AABB.FromCenterExtents(center, new Vector3(radius)));

    public static bool WorldOverlapAABB(PhysicsWorld world, AABB box)
        => world.CheckAABBCollision(box);
}
