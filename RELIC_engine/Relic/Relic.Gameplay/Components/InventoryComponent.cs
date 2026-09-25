namespace Relic.Gameplay.Components;

using Relic.Gameplay.Weapons;

public sealed class InventoryComponent : Component
{
    private readonly List<Weapon> _weapons = new();
    public IReadOnlyList<Weapon> Weapons => _weapons;
    public int ActiveIndex { get; private set; } = -1;
    public Weapon? ActiveWeapon => ActiveIndex >= 0 && ActiveIndex < _weapons.Count ? _weapons[ActiveIndex] : null;

    public bool AddWeapon(Weapon weapon)
    {
        if (_weapons.Any(w => w.Name.Equals(weapon.Name, StringComparison.OrdinalIgnoreCase))) return false;
        _weapons.Add(weapon);
        if (ActiveIndex == -1) ActiveIndex = 0;
        Console.WriteLine($"[Inventory] Added {weapon.Name} count {_weapons.Count}");
        return true;
    }

    public bool RemoveWeapon(string name)
    {
        int idx = _weapons.FindIndex(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;
        _weapons.RemoveAt(idx);
        if (_weapons.Count == 0) ActiveIndex = -1;
        else if (ActiveIndex >= _weapons.Count) ActiveIndex = _weapons.Count - 1;
        else if (ActiveIndex == idx) ActiveIndex = Math.Min(idx, _weapons.Count - 1);
        return true;
    }

    public bool SwitchWeapon(int index)
    {
        if (index < 0 || index >= _weapons.Count) return false;
        ActiveIndex = index;
        Console.WriteLine($"[Inventory] Switched to {ActiveWeapon?.Name} [{index}]");
        return true;
    }

    public bool SwitchWeapon(string name)
    {
        int idx = _weapons.FindIndex(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;
        return SwitchWeapon(idx);
    }

    public override void Update(float dt)
    {
        ActiveWeapon?.Update(dt);
    }
}
