namespace Relic.Gameplay.Weapons;

public static class WeaponDatabase
{
    private static readonly Dictionary<string, WeaponDefinition> _defs = new(StringComparer.OrdinalIgnoreCase);

    static WeaponDatabase()
    {
        Register(new WeaponDefinition("DebugRifle") { Damage = 25f, FireRate = 0.15f, Range = 2000f, MaxAmmo = 30, ReserveAmmo = 90 });
        Register(new WeaponDefinition("Rifle") { Damage = 25f, FireRate = 0.12f, Range = 2000f, MaxAmmo = 30, ReserveAmmo = 90 });
        Register(new WeaponDefinition("Pistol") { Damage = 15f, FireRate = 0.25f, Range = 1500f, MaxAmmo = 12, ReserveAmmo = 60 });
        Register(new WeaponDefinition("Shotgun") { Damage = 12f, FireRate = 0.7f, Range = 800f, MaxAmmo = 8, ReserveAmmo = 32 });
    }

    public static void Register(WeaponDefinition def) => _defs[def.Name] = def;
    public static WeaponDefinition? Get(string name) => _defs.TryGetValue(name, out var d) ? d : null;
    public static WeaponDefinition GetOrCreate(string name) => _defs.TryGetValue(name, out var d) ? d : new WeaponDefinition(name);
    public static IReadOnlyCollection<WeaponDefinition> All => _defs.Values;
}
