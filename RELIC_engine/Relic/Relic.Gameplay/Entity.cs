namespace Relic.Gameplay;

using Relic.Gameplay.Components;

public sealed class Entity
{
    public EntityId Id { get; }
    public string Name { get; set; }
    public string ClassName { get; set; }
    public string Tag { get; set; }
    public bool Active { get; set; } = true;
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<Component> _components = new();
    private readonly Dictionary<Type, Component> _lookup = new();

    public TransformComponent Transform { get; }

    internal Entity(EntityId id, string name, string className)
    {
        Id = id;
        Name = name;
        ClassName = className;
        Tag = string.Empty;
        Transform = AddComponent(new TransformComponent());
    }

    public T AddComponent<T>(T component) where T : Component
    {
        if (_lookup.ContainsKey(typeof(T))) throw new InvalidOperationException($"Entity {Id} already has {typeof(T).Name}");
        component.Entity = this;
        _components.Add(component);
        _lookup[typeof(T)] = component;
        component.Initialize();
        return component;
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var c = new T();
        return AddComponent(c);
    }

    public T? GetComponent<T>() where T : Component
        => _lookup.TryGetValue(typeof(T), out var c) ? (T)c : null;

    public bool HasComponent<T>() where T : Component => _lookup.ContainsKey(typeof(T));

    public void RemoveComponent<T>() where T : Component
    {
        if (_lookup.TryGetValue(typeof(T), out var c))
        {
            c.Shutdown();
            _components.Remove(c);
            _lookup.Remove(typeof(T));
            c.Entity = null;
        }
    }

    public IEnumerable<Component> GetComponents() => _components;

    internal void Update(float dt)
    {
        if (!Active) return;
        foreach (var c in _components) if (c.Enabled) c.Update(dt);
    }

    internal void Shutdown()
    {
        foreach (var c in _components) c.Shutdown();
        _components.Clear();
        _lookup.Clear();
    }
}
