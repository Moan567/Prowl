namespace Relic.Gameplay.Components;

public sealed class PlayerComponent : Component
{
    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool IsSprinting { get; set; }
}
