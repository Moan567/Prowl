namespace Relic.Gameplay.Components;

using Relic.Framework;

// Phase 10: AI pathing. path_corner entities chain via "target" tags;
// PatrolComponent walks the route (used by dogs, soldiers, demons).
public sealed class PathCornerComponent : Component
{
    public string Target { get; set; } = "";
    public float Wait { get; set; }
}

public sealed class PatrolComponent : Component
{
    public float Speed { get; set; } = 120f;
    public bool Loop { get; set; } = true;
    public int WaypointIndex { get; private set; }
    public int WaypointsVisited { get; private set; }
    public IReadOnlyList<Vector3> Waypoints => _waypoints;
    private readonly List<Vector3> _waypoints = new();
    private float _waitTimer;

    public void Resolve(EntityManager manager, string startTag)
    {
        _waypoints.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? tag = startTag;
        while (!string.IsNullOrEmpty(tag) && seen.Add(tag))
        {
            Entity? node = manager.FindByTag(tag).FirstOrDefault()
                ?? manager.FindFirstByClassName(tag);
            if (node == null) break;
            _waypoints.Add(node.Transform.Position);
            var corner = node.GetComponent<PathCornerComponent>();
            tag = corner?.Target;
            if (_waypoints.Count > 64) break;
        }
        WaypointIndex = 0;
    }

    public override void Update(float deltaTime)
    {
        if (Entity == null || _waypoints.Count == 0) return;
        if (WaypointIndex >= _waypoints.Count)
        {
            if (!Loop) return;
            WaypointIndex = 0;
        }
        if (_waitTimer > 0f) { _waitTimer -= deltaTime; return; }
        Vector3 target = _waypoints[WaypointIndex];
        Vector3 pos = Entity.Transform.Position;
        Vector3 delta = target - pos;
        float len = delta.Length();
        if (len < 4f)
        {
            WaypointsVisited++;
            WaypointIndex++;
            return;
        }
        Entity.Transform.Position = pos + delta / len * MathF.Min(Speed * deltaTime, len);
    }
}

public sealed class EnemySpawnComponent : Component
{
    public string EnemyClass { get; set; } = "enemy_soldier";
    public int Count { get; set; } = 1;
    public int SpawnedCount { get; private set; }
    public event Action<Entity>? OnSpawned;

    public void SpawnAll(EntityManager manager)
    {
        for (int i = 0; i < Count; i++)
        {
            var e = manager.CreateEntity(EnemyClass, EnemyClass, string.Empty);
            e.Transform.Position = Entity?.Transform.Position ?? Vector3.Zero;
            e.AddComponent(new HealthComponent { MaxHealth = 100, Health = 100 });
            e.AddComponent(new EnemyComponent());
            SpawnedCount++;
            OnSpawned?.Invoke(e);
        }
    }
}
