namespace Relic.Gameplay;

public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly uint Value;
    public EntityId(uint value) => Value = value;
    public bool Equals(EntityId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);
    public override int GetHashCode() => (int)Value;
    public override string ToString() => $"E{Value}";
    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    public static readonly EntityId Invalid = new(0);
}
