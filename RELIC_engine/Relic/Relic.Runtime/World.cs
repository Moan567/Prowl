namespace Relic.Runtime;

public sealed class World
{
    private readonly List<Entity> _entities = new();
    private readonly List<Entity> _toAdd = new();
    private readonly List<Entity> _toRemove = new();

    public IReadOnlyList<Entity> Entities => _entities;

    public Entity CreateEntity(string? name = null)
    {
        var entity = new Entity(name);
        _entities.Add(entity);
        return entity;
    }

    public void AddEntity(Entity entity)
    {
        _entities.Add(entity);
    }

    public void DestroyEntity(Entity entity)
    {
        _toRemove.Add(entity);
    }

    public void Update(float deltaTime)
    {
        foreach (var entity in _toAdd)
        {
            _entities.Add(entity);
        }
        _toAdd.Clear();

        foreach (var entity in _entities)
        {
            entity.Update(deltaTime);
        }

        foreach (var entity in _toRemove)
        {
            entity.Shutdown();
            _entities.Remove(entity);
        }
        _toRemove.Clear();
    }

    public void Shutdown()
    {
        foreach (var entity in _entities)
            entity.Shutdown();
        _entities.Clear();
        _toAdd.Clear();
        _toRemove.Clear();
    }

    public T? FindEntity<T>() where T : class
    {
        return _entities.OfType<T>().FirstOrDefault();
    }

    public IEnumerable<T> FindEntities<T>() where T : class
    {
        return _entities.OfType<T>();
    }
}