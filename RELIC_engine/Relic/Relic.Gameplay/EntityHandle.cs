namespace Relic.Gameplay;

public readonly struct EntityHandle : IEquatable<EntityHandle>
{
    private readonly EntityManager? _manager;
    public readonly EntityId Id;
    public EntityHandle(EntityManager manager, EntityId id) { _manager = manager; Id = id; }
    public bool IsValid => _manager != null && _manager.TryGetEntity(Id, out _);
    public Entity? Entity => _manager != null && _manager.TryGetEntity(Id, out var e) ? e : null;
    public bool Equals(EntityHandle other) => Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is EntityHandle other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(EntityHandle a, EntityHandle b) => a.Id == b.Id;
    public static bool operator !=(EntityHandle a, EntityHandle b) => a.Id != b.Id;
}
