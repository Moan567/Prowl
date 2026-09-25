namespace Relic.Gameplay;

public sealed class EntityManager
{
    private readonly Dictionary<uint, Entity> _entities = new();
    private readonly List<Entity> _toAdd = new();
    private readonly List<Entity> _toRemove = new();
    private uint _nextId = 1;

    public IReadOnlyCollection<Entity> Entities => _entities.Values;
    public int Count => _entities.Count;

    public Entity CreateEntity(string? name = null, string className = "entity", string tag = "")
    {
        var id = new EntityId(_nextId++);
        var e = new Entity(id, name ?? $"Entity_{id.Value}", className);
        e.Tag = tag;
        _entities[id.Value] = e;
        return e;
    }

    public EntityHandle CreateHandle(Entity entity) => new(this, entity.Id);

    public void DestroyEntity(Entity entity) => _toRemove.Add(entity);
    public void DestroyEntity(EntityId id) { if (_entities.TryGetValue(id.Value, out var e)) _toRemove.Add(e); }

    public bool TryGetEntity(EntityId id, out Entity? entity) => _entities.TryGetValue(id.Value, out entity);
    public Entity? FindById(EntityId id) => _entities.TryGetValue(id.Value, out var e) ? e : null;

    public IEnumerable<Entity> FindByClassName(string className)
        => _entities.Values.Where(e => e.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<Entity> FindByTag(string tag)
        => _entities.Values.Where(e => e.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
    public Entity? FindFirstByClassName(string className)
        => _entities.Values.FirstOrDefault(e => e.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));

    public void Update(float deltaTime)
    {
        foreach (var e in _toAdd) _entities[e.Id.Value] = e;
        _toAdd.Clear();
        foreach (var e in _entities.Values) e.Update(deltaTime);
        foreach (var e in _toRemove) { e.Shutdown(); _entities.Remove(e.Id.Value); }
        _toRemove.Clear();
    }

    public void Shutdown()
    {
        foreach (var e in _entities.Values) e.Shutdown();
        _entities.Clear();
        _toAdd.Clear();
        _toRemove.Clear();
    }
}
