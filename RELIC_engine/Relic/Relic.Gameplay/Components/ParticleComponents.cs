namespace Relic.Gameplay.Components;

using Relic.Framework;

// Phase 10: particle effects. Data-driven emitters; simulation is
// counter-based so it stays headless-testable (rendering reads AliveCount).
public enum ParticleEffectKind
{
    Generic,
    Smoke,
    Fire,
    Sparks,
    Blood,
    Explosion,
    Muzzleflash,
}

public sealed class ParticleEffectComponent : Component
{
    public ParticleEffectKind Kind { get; set; } = ParticleEffectKind.Generic;
    public float SpawnRate { get; set; } = 10f;
    public float Lifetime { get; set; } = 1f;
    public Vector3 Velocity { get; set; } = new(0, 0, 32);
    public float Size { get; set; } = 4f;
    public Vector3 Color { get; set; } = new(1, 1, 1);

    public int TotalSpawned { get; private set; }
    public int AliveCount => _alive.Count;
    private readonly List<float> _alive = new();
    private float _accum;

    public override void Update(float deltaTime)
    {
        if (!Enabled) return;
        _accum += SpawnRate * deltaTime;
        while (_accum >= 1f)
        {
            _accum -= 1f;
            _alive.Add(Lifetime);
            TotalSpawned++;
        }
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            _alive[i] -= deltaTime;
            if (_alive[i] <= 0f) _alive.RemoveAt(i);
        }
    }

    public void Burst(int count)
    {
        for (int i = 0; i < count; i++) { _alive.Add(Lifetime); TotalSpawned++; }
    }
}
