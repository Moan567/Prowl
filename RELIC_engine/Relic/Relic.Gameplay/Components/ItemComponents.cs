namespace Relic.Gameplay.Components;

// Phase 10: pickups (ammo / armor / keycard / weapon spawns).
public sealed class AmmoPickupComponent : Component
{
    public string AmmoType { get; set; } = "rifle"; // shotgun, rifle, rocket
    public int Amount { get; set; } = 10;
    public bool Collected { get; private set; }
    public event Action? OnCollected;

    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        OnCollected?.Invoke();
    }
}

public sealed class ArmorPickupComponent : Component
{
    public int Amount { get; set; } = 50;
    public bool Collected { get; private set; }
    public event Action? OnCollected;

    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        OnCollected?.Invoke();
    }
}

public sealed class KeycardPickupComponent : Component
{
    public string KeyId { get; set; } = "red";
    public bool Collected { get; private set; }
    public event Action? OnCollected;

    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        OnCollected?.Invoke();
    }
}

public sealed class WeaponSpawnComponent : Component
{
    public string WeaponName { get; set; } = "Shotgun";
    public float RespawnTime { get; set; } = 10f;
    public bool Available { get; private set; } = true;
    public int TakeCount { get; private set; }
    public event Action? OnTaken;
    private float _timer;

    public bool Take()
    {
        if (!Available) return false;
        Available = false;
        TakeCount++;
        _timer = 0f;
        OnTaken?.Invoke();
        return true;
    }

    public override void Update(float deltaTime)
    {
        if (Available) return;
        _timer += deltaTime;
        if (_timer >= RespawnTime) Available = true;
    }
}
