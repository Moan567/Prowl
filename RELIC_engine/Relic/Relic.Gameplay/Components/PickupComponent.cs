namespace Relic.Gameplay.Components;

public sealed class WeaponPickupComponent : Component
{
    public string WeaponName { get; set; } = "Rifle";
    public int AmmoAmount { get; set; } = 30;
}

public sealed class HealthPickupComponent : Component
{
    public float HealAmount { get; set; } = 25f;
}
