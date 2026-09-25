namespace Relic.Gameplay.Weapons;

using Relic.Framework;
using Relic.Physics;

public abstract class Weapon
{
    public string Name { get; }
    public int Ammo { get; protected set; }
    public int MaxAmmo { get; protected set; }
    public int ReserveAmmo { get; protected set; }
    public float FireRate { get; protected set; } = 0.15f;
    private float _cooldown;

    protected Weapon(string name, int maxAmmo = 30)
    {
        Name = name;
        MaxAmmo = maxAmmo;
        Ammo = maxAmmo;
        ReserveAmmo = maxAmmo * 3;
    }

    public bool CanFire => Ammo > 0 && _cooldown <= 0f;

    public void Update(float dt)
    {
        if (_cooldown > 0f) _cooldown -= dt;
    }

    public void Fire()
    {
        if (!CanFire) { if (Ammo <= 0) Console.WriteLine($"[Weapon] {Name} click (empty)"); return; }
        Ammo--;
        _cooldown = FireRate;
        OnFire();
    }

    protected abstract void OnFire();

    public virtual void Reload()
    {
        if (ReserveAmmo <= 0 || Ammo == MaxAmmo) return;
        int needed = MaxAmmo - Ammo;
        int take = Math.Min(needed, ReserveAmmo);
        Ammo += take;
        ReserveAmmo -= take;
        Console.WriteLine($"[Weapon] {Name} reloaded Ammo {Ammo}/{MaxAmmo} Reserve {ReserveAmmo}");
    }
}

public sealed class DebugRifle : HitscanWeapon
{
    public DebugRifle(PhysicsWorld? world, Func<Vector3> origin, Func<Vector3> forward)
        : base(WeaponDatabase.Get("DebugRifle") ?? new WeaponDefinition("DebugRifle"), world, null, origin, forward) { }
    public DebugRifle(PhysicsWorld? world, EntityManager? em, Func<Vector3> origin, Func<Vector3> forward)
        : base(WeaponDatabase.Get("DebugRifle") ?? new WeaponDefinition("DebugRifle"), world, em, origin, forward) { }
}
