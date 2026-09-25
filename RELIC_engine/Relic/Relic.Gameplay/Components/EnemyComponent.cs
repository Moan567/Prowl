namespace Relic.Gameplay.Components;

public sealed class EnemyComponent : Component
{
    public float TouchDamage { get; set; } = 10f;

    public void TakeDamage(float amount)
    {
        var health = Entity?.GetComponent<HealthComponent>();
        if (health == null) return;
        health.Damage(amount);
        Console.WriteLine($"[Enemy] Damaged {amount} health {health.Health:F0} entity {Entity?.Id}");
        if (!health.IsAlive)
        {
            Console.WriteLine($"[Enemy] Killed {Entity?.Id}");
        }
    }

    public void Kill()
    {
        var health = Entity?.GetComponent<HealthComponent>();
        health?.Kill();
        Console.WriteLine($"[Enemy] Killed {Entity?.Id}");
    }

    public override void Initialize()
    {
        var health = Entity?.GetComponent<HealthComponent>();
        if (health != null)
        {
            health.OnDeath += () =>
            {
                Console.WriteLine($"[Enemy] Killed {Entity?.Id}");
                // Removal handled by system that checks health
            };
        }
    }
}
