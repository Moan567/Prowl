namespace Relic.Gameplay.Weapons;

using Relic.Framework;
using Relic.Physics;
using Relic.Gameplay;

public class HitscanWeapon : Weapon
{
    public float Damage { get; set; }
    public float Range { get; set; }
    private readonly PhysicsWorld? _world;
    private readonly EntityManager? _entityManager;
    private readonly Func<Vector3>? _origin;
    private readonly Func<Vector3>? _forward;

    public HitscanWeapon(WeaponDefinition def, PhysicsWorld? world = null, EntityManager? entityManager = null, Func<Vector3>? origin = null, Func<Vector3>? forward = null)
        : base(def.Name, def.MaxAmmo)
    {
        Damage = def.Damage;
        FireRate = def.FireRate;
        Range = def.Range;
        MaxAmmo = def.MaxAmmo;
        ReserveAmmo = def.ReserveAmmo;
        Ammo = MaxAmmo;
        _world = world;
        _entityManager = entityManager;
        _origin = origin;
        _forward = forward;
    }

    public HitscanWeapon(string name, float damage, float fireRate, float range, int maxAmmo, PhysicsWorld? world = null, EntityManager? em = null, Func<Vector3>? origin = null, Func<Vector3>? forward = null)
        : base(name, maxAmmo)
    {
        Damage = damage;
        FireRate = fireRate;
        Range = range;
        _world = world;
        _entityManager = em;
        _origin = origin;
        _forward = forward;
    }

    protected override void OnFire()
    {
        Vector3 origin = _origin != null ? _origin() : Vector3.Zero;
        Vector3 dir = _forward != null ? _forward() : new Vector3(1, 0, 0);
        if (dir.LengthSquared() < 1e-6f) dir = new Vector3(1, 0, 0);
        dir.Normalize();
        Console.WriteLine($"[Weapon] {Name} Fire from {origin} dir {dir} Ammo {Ammo}/{MaxAmmo} Damage {Damage} Range {Range}");
        // Hitscan against enemies first (sphere check), then world
        if (_entityManager != null)
        {
            Entity? hitEnemy = null;
            float closest = float.MaxValue;
            Vector3 hitPoint = Vector3.Zero;
            foreach (var e in _entityManager.FindByClassName("enemy_soldier"))
            {
                var hp = e.GetComponent<Components.HealthComponent>();
                if (hp != null && !hp.IsAlive) continue;
                Vector3 pos = e.Transform.Position;
                // Sphere radius 24, center at pos + (0,0,24) approx enemy center
                Vector3 center = pos + new Vector3(0, 0, 24);
                float radius = 24f;
                // Ray-sphere
                Vector3 oc = origin - center;
                float b = Vector3.Dot(oc, dir);
                float c = Vector3.Dot(oc, oc) - radius * radius;
                float disc = b * b - c;
                if (disc < 0) continue;
                float sqrt = MathF.Sqrt(disc);
                float t0 = -b - sqrt;
                float t1 = -b + sqrt;
                float t = t0 >= 0 ? t0 : t1 >= 0 ? t1 : -1;
                if (t < 0 || t > Range) continue;
                if (t < closest)
                {
                    // Also check world obstruction between origin and t
                    if (_world != null)
                    {
                        var wallHit = _world.Raycast(origin, dir, t);
                        if (wallHit.Hit) continue; // wall blocks enemy
                    }
                    closest = t;
                    hitEnemy = e;
                    hitPoint = origin + dir * t;
                }
            }
            if (hitEnemy != null)
            {
                Console.WriteLine($"[Weapon] Hit Scan: Hit enemy {hitEnemy.Id} at {hitPoint} dist {closest:F1}");
                var health = hitEnemy.GetComponent<Components.HealthComponent>();
                var enemyComp = hitEnemy.GetComponent<Components.EnemyComponent>();
                if (enemyComp != null) enemyComp.TakeDamage(Damage);
                else if (health != null) { health.Damage(Damage); Console.WriteLine($"[Enemy] Damaged {Damage} health {health.Health:F0}"); if (!health.IsAlive) Console.WriteLine($"[Enemy] Killed {hitEnemy.Id}"); }
                if (health != null && !health.IsAlive)
                {
                    _entityManager?.DestroyEntity(hitEnemy);
                    Console.WriteLine($"[Enemy] Killed {hitEnemy.Id} removed");
                }
                return;
            }
        }
        if (_world != null)
        {
            var hit = _world.Raycast(origin, dir, Range);
            if (hit.Hit)
                Console.WriteLine($"[Weapon] Hit Scan: Hit at {hit.Point} normal {hit.Normal} dist {hit.Distance:F1} brush {hit.HitBrushIndex}");
            else
                Console.WriteLine($"[Weapon] Hit Scan: miss ({Range})");
        }
    }

    // Helper for tests without delegates
    public void Fire(Vector3 origin, Vector3 dir, EntityManager em, PhysicsWorld world)
    {
        // Direct fire for tests
        var tmp = new HitscanWeapon(new WeaponDefinition(Name){Damage=Damage, FireRate=0, Range=Range, MaxAmmo=MaxAmmo}, world, em, ()=>origin, ()=>dir);
        tmp.Ammo = Ammo;
        tmp.Fire();
        Ammo = tmp.Ammo;
    }
}
