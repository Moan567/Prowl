namespace Relic.Gameplay.Components;

using Relic.Framework;

// Phase 10: trigger system. Kind comes from the entity classname
// (trigger_multiple, trigger_once, trigger_hurt, ...). All behavior is
// data-driven; Activate() is called by overlap checks in game code.
public enum TriggerKind
{
    Multiple,
    Once,
    Hurt,
    Heal,
    Push,
    Teleport,
    Sound,
    Music,
}

public sealed class TriggerComponent : Component
{
    public TriggerKind Kind { get; set; } = TriggerKind.Multiple;
    public float Damage { get; set; }
    public float Interval { get; set; } = 1f;
    public float Amount { get; set; }
    public float Force { get; set; }
    public Vector3 Direction { get; set; } = Vector3.Forward;
    public string Target { get; set; } = "";
    public string Sound { get; set; } = "";
    public string Track { get; set; } = "";
    public float Radius { get; set; } = 64f;

    public int FireCount { get; private set; }
    public bool Consumed { get; private set; }
    public event Action<Entity>? OnFired;
    private float _timer;

    public override void Update(float deltaTime)
    {
        if (Consumed || !Enabled) return;
        if (Kind != TriggerKind.Hurt) return;
        // trigger_hurt re-applies via Activate calls; timer gates repeats
        _timer += deltaTime;
    }

    public bool CanFire() => !Consumed && (Kind != TriggerKind.Hurt || _timer >= Interval);

    public void Activate(EntityManager manager, Entity activator)
    {
        if (!CanFire()) return;
        _timer = 0f;
        FireCount++;
        OnFired?.Invoke(activator);

        var health = activator.GetComponent<HealthComponent>();
        switch (Kind)
        {
            case TriggerKind.Hurt:
                health?.Damage(Damage);
                break;
            case TriggerKind.Heal:
                health?.Heal(Amount);
                break;
            case TriggerKind.Push:
                activator.Transform.Position += Direction * Force * 0.016f;
                break;
            case TriggerKind.Teleport:
                var dest = manager.FindByTag(Target).FirstOrDefault()
                    ?? manager.FindFirstByClassName(Target);
                if (dest != null) activator.Transform.Position = dest.Transform.Position;
                break;
            case TriggerKind.Sound:
            case TriggerKind.Music:
            case TriggerKind.Multiple:
                break;
            case TriggerKind.Once:
                break;
        }

        if (Kind == TriggerKind.Once)
        {
            Consumed = true;
            manager.DestroyEntity(Entity!);
        }
    }
}
