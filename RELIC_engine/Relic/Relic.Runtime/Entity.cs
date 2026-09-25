namespace Relic.Runtime;

using Relic.Framework;

public class Entity
{
    public string Name { get; set; }
    public Guid Id { get; } = Guid.NewGuid();
    public bool Active { get; set; } = true;
    
    private readonly List<Component> _components = new();
    private readonly Dictionary<Type, Component> _componentLookup = new();
    
    public TransformComponent Transform { get; private set; }

    public Entity(string? name = null)
    {
        Name = name ?? "Entity";
        Transform = AddComponent(new TransformComponent());
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var component = new T();
        return AddComponent(component);
    }

    public T AddComponent<T>(T component) where T : Component
    {
        if (_componentLookup.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"Entity already has component of type {typeof(T).Name}");

        component.Entity = this;
        _components.Add(component);
        _componentLookup[typeof(T)] = component;
        component.Initialize();
        return component;
    }

    public T? GetComponent<T>() where T : Component
    {
        return _componentLookup.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }

    public bool HasComponent<T>() where T : Component
    {
        return _componentLookup.ContainsKey(typeof(T));
    }

    public void RemoveComponent<T>() where T : Component
    {
        if (_componentLookup.TryGetValue(typeof(T), out var component))
        {
            component.Shutdown();
            _components.Remove(component);
            _componentLookup.Remove(typeof(T));
            component.Entity = null;
        }
    }

    public IEnumerable<Component> GetComponents() => _components;

    public void Update(float deltaTime)
    {
        if (!Active) return;
        
        foreach (var component in _components)
        {
            if (component.Enabled)
                component.Update(deltaTime);
        }
    }

    public void Shutdown()
    {
        foreach (var component in _components)
            component.Shutdown();
        _components.Clear();
        _componentLookup.Clear();
    }
}