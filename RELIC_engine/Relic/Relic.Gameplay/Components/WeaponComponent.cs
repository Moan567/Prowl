namespace Relic.Gameplay.Components;

using Relic.Gameplay.Weapons;

public sealed class WeaponComponent : Component
{
    public Weapon? Equipped { get; private set; }
    public void Equip(Weapon weapon) { Equipped = weapon; }
    public void Fire() => Equipped?.Fire();
    public void Reload() => Equipped?.Reload();
}
