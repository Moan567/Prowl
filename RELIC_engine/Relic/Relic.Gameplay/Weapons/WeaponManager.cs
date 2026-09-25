namespace Relic.Gameplay.Weapons;

public sealed class WeaponManager
{
    private readonly Dictionary<string, Weapon> _weapons = new(StringComparer.OrdinalIgnoreCase);
    public Weapon? Active { get; private set; }

    public void Register(Weapon weapon) => _weapons[weapon.Name] = weapon;
    public bool TryGet(string name, out Weapon? w) => _weapons.TryGetValue(name, out w);
    public void SetActive(string name) { if (_weapons.TryGetValue(name, out var w)) Active = w; }
    public void SetActive(Weapon weapon) { Register(weapon); Active = weapon; }
    public void Update(float dt) { foreach (var w in _weapons.Values) w.Update(dt); Active?.Update(dt); }
    public void Fire() => Active?.Fire();
    public void Reload() => Active?.Reload();
}
