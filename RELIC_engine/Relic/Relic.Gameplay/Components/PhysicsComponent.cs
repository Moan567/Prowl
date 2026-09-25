namespace Relic.Gameplay.Components;

using Relic.Physics;
using Relic.Framework;

public sealed class PhysicsComponent : Component
{
    public CharacterController? Controller { get; set; }
    public Vector3 HalfExtents { get; set; } = new(16, 16, 36);
    public PhysicsWorld? World { get; set; }

    public Vector3 Position => Controller?.Position ?? Entity!.Transform.Position;
    public Vector3 Velocity => Controller?.Velocity ?? Vector3.Zero;
    public bool IsOnGround => Controller?.IsOnGround ?? false;

    public void Teleport(Vector3 pos) => Controller?.Teleport(pos);
}
