namespace Relic.Gameplay.Components;

// Phase 10: audio entities. All playback is data-driven from .map properties.
// Wire OnPlaySound to AudioSystem in game init; headless tests observe PlayCount.
public sealed class AmbientSoundComponent : Component
{
    public string Sound { get; set; } = "forest_wind.wav";
    public float Volume { get; set; } = 1f;
    public float Radius { get; set; } = 512f;
    public bool Loop { get; set; } = true;
    public int PlayCount { get; private set; }
    public Action<string, float>? OnPlaySound;

    public void Play()
    {
        PlayCount++;
        OnPlaySound?.Invoke(Sound, Volume);
    }
}

public sealed class SoundEmitterComponent : Component
{
    public string Sound { get; set; } = "";
    public float Volume { get; set; } = 1f;
    public float Radius { get; set; } = 256f;
    public float Interval { get; set; } = 3f;
    public int PlayCount { get; private set; }
    public Action<string, float>? OnPlaySound;
    private float _timer;

    public override void Update(float deltaTime)
    {
        if (string.IsNullOrEmpty(Sound)) return;
        _timer += deltaTime;
        if (_timer >= Interval)
        {
            _timer = 0f;
            PlayCount++;
            OnPlaySound?.Invoke(Sound, Volume);
        }
    }
}

public sealed class SoundZoneComponent : Component
{
    public string Sound { get; set; } = "";
    public float Radius { get; set; } = 256f;
    public bool Loop { get; set; } = true;
    public bool IsInside { get; private set; }
    public int EnterCount { get; private set; }
    public Action<string>? OnEnter;

    public void SetInside(bool inside)
    {
        if (inside && !IsInside) { EnterCount++; OnEnter?.Invoke(Sound); }
        IsInside = inside;
    }
}

public sealed class MusicPlayerComponent : Component
{
    public string Track { get; set; } = "";
    public float Volume { get; set; } = 0.8f;
    public bool Loop { get; set; } = true;
    public bool IsPlaying { get; private set; }
    public int PlayCount { get; private set; }
    public Action<string, float>? OnPlayMusic;
    public Action? OnStopMusic;

    public void Play()
    {
        if (string.IsNullOrEmpty(Track)) return;
        IsPlaying = true;
        PlayCount++;
        OnPlayMusic?.Invoke(Track, Volume);
    }

    public void Stop()
    {
        IsPlaying = false;
        OnStopMusic?.Invoke();
    }
}

public sealed class MusicTriggerComponent : Component
{
    public string Track { get; set; } = "";
    public float Volume { get; set; } = 0.8f;
    public bool Triggered { get; private set; }
    public event Action<string>? OnTriggered;

    public void Trigger()
    {
        Triggered = true;
        OnTriggered?.Invoke(Track);
    }
}

public sealed class ReverbZoneComponent : Component
{
    public float Radius { get; set; } = 256f;
    public float Amount { get; set; } = 0.5f;
}
