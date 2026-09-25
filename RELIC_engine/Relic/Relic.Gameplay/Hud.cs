namespace Relic.Gameplay;

public sealed class Hud
{
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public int Ammo { get; set; }
    public int MaxAmmo { get; set; }
    public string WeaponName { get; set; } = "None";
    public int Reserve { get; set; }

    public void Update(float health, float maxHealth, int ammo, int maxAmmo, string weapon, int reserve)
    {
        Health = health;
        MaxHealth = maxHealth;
        Ammo = ammo;
        MaxAmmo = maxAmmo;
        WeaponName = weapon;
        Reserve = reserve;
    }

    public string GetText()
        => $"Health: {Health:F0}/{MaxHealth:F0}\nAmmo: {Ammo}/{MaxAmmo} ({Reserve})\nWeapon: {WeaponName}";
}
