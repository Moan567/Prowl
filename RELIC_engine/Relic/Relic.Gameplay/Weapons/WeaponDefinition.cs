namespace Relic.Gameplay.Weapons;

public sealed class WeaponDefinition
{
    public string Name { get; }
    public float Damage { get; set; } = 25f;
    public float FireRate { get; set; } = 0.15f;
    public float Range { get; set; } = 2000f;
    public int MaxAmmo { get; set; } = 30;
    public int ReserveAmmo { get; set; } = 90;
    public string MuzzleEffect { get; set; } = string.Empty;

    public WeaponDefinition(string name) => Name = name;
}
