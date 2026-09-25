namespace Relic.Gameplay.Components;

public sealed class HealthComponent : Component
{
    public float MaxHealth { get; set; } = 100f;
    public float Health { get; set; } = 100f;
    public bool IsAlive => Health > 0f;
    public event Action<float>? OnDamaged;
    public event Action? OnDeath;
    public event Action<float>? OnHealed;

    public void Damage(float amount)
    {
        if (!IsAlive) return;
        Health = MathF.Max(0f, Health - amount);
        OnDamaged?.Invoke(amount);
        if (Health <= 0f) OnDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        float before = Health;
        Health = MathF.Min(MaxHealth, Health + amount);
        OnHealed?.Invoke(Health - before);
    }

    public void Kill()
    {
        if (!IsAlive) return;
        Health = 0f;
        OnDeath?.Invoke();
    }
}
