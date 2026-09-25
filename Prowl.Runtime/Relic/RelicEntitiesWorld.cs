// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// RELIC gameplay layer — triggers, logic, movers, objectives, environment, audio.

using System;
using System.Collections.Generic;
using System.Linq;

using Prowl.Echo;
using Prowl.Vector;

namespace Prowl.Runtime.Relic;

// ---------------------------------------------------------------- triggers ---

public enum RelicTriggerKind
{
    Once, Multiple, Hurt, Heal, Push, Teleport, Music, Sound
}

/// <summary>Trigger volume (trigger_once/_multiple/_hurt/_heal/_push/_teleport/_music/_sound).</summary>
[AddComponentMenu("Relic/Triggers/Trigger")]
[RequireComponent(typeof(TriggerVolume))]
public sealed class RelicTrigger : MonoBehaviour
{
    public RelicTriggerKind Kind = RelicTriggerKind.Once;
    public string Target = string.Empty;
    public string TriggerName = string.Empty;
    public float Damage = 10f;
    public float HealAmount = 25f;
    public float Interval = 1f;
    public Float3 PushDirection = new(1, 0, 0);
    public float PushForce = 300f;
    public string Sound = string.Empty;
    public string MusicTrack = string.Empty;

    [SerializeIgnore] private bool _fired;
    [SerializeIgnore] private float _tick;

    public TriggerVolume? Volume => GetComponent<TriggerVolume>();

    public override void OnTriggerEnter(Rigidbody3D other) => Handle(other, true);
    public override void OnTriggerStay(Rigidbody3D other) => Handle(other, false);

    private void Handle(Rigidbody3D other, bool entered)
    {
        if (Kind == RelicTriggerKind.Once && _fired) return;
        var player = other.GameObject.GetComponent<RelicPlayerController>();
        if (!player.IsValid()) player = other.GameObject.GetComponentInParent<RelicPlayerController>();
        if (!player.IsValid() && Kind != RelicTriggerKind.Sound) return;

        switch (Kind)
        {
            case RelicTriggerKind.Once:
            case RelicTriggerKind.Multiple:
                _fired = true;
                FireTarget();
                if (!string.IsNullOrEmpty(MusicTrack)) RelicMusicPlayer.Play(MusicTrack);
                break;
            case RelicTriggerKind.Hurt:
                _tick -= (float)Time.DeltaTime;
                if (entered || _tick <= 0) { if (player.IsValid()) player.Damage(Damage); _tick = Interval; }
                break;
            case RelicTriggerKind.Heal:
                _tick -= (float)Time.DeltaTime;
                if (entered || _tick <= 0) { if (player.IsValid()) player.Heal(HealAmount); _tick = Math.Max(0.2f, Interval); }
                break;
            case RelicTriggerKind.Push:
                other.ApplyImpulse(Float3.NormalizeSafe(PushDirection, Float3.UnitX) * PushForce * 0.02f);
                break;
            case RelicTriggerKind.Teleport:
                Teleport(player);
                break;
            case RelicTriggerKind.Music:
                if (entered && !string.IsNullOrEmpty(MusicTrack)) RelicMusicPlayer.Play(MusicTrack);
                break;
            case RelicTriggerKind.Sound:
                if (entered && !string.IsNullOrEmpty(Sound)) RelicAudio.PlayOneShot(Sound, Transform.Position);
                break;
        }
    }

    private void FireTarget()
    {
        if (string.IsNullOrEmpty(Target)) return;
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return;
        foreach (var relay in scene.FindObjectsOfType<RelicLogicRelay>())
            if (relay.IsValid() && relay.RelayName.Equals(Target, StringComparison.OrdinalIgnoreCase)) relay.Fire();
        foreach (var door in scene.FindObjectsOfType<RelicMover>())
            if (door.IsValid() && door.MoverName.Equals(Target, StringComparison.OrdinalIgnoreCase)) door.Toggle();
        foreach (var obj in scene.FindObjectsOfType<RelicObjective>())
            if (obj.IsValid() && obj.ObjectiveId.Equals(Target, StringComparison.OrdinalIgnoreCase)) obj.Complete();
    }

    private void Teleport(RelicPlayerController? player)
    {
        if (!player.IsValid() || string.IsNullOrEmpty(Target)) return;
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return;
        var dest = scene.AllObjects.FirstOrDefault(o => o.Name.Equals(Target, StringComparison.OrdinalIgnoreCase));
        if (dest.IsValid()) player.Transform.Position = dest.Transform.Position;
    }
}

// ------------------------------------------------------------------- logic ---

/// <summary>Relay / counter / timer / random / compare (logic_*).</summary>
[AddComponentMenu("Relic/Logic/Relay")]
public sealed class RelicLogicRelay : MonoBehaviour
{
    public string RelayName = string.Empty;
    public string Target = string.Empty;
    public float Delay;
    [SerializeIgnore] private float _pending = -1;

    public void Fire()
    {
        if (Delay > 0) _pending = Delay;
        else FireNow();
    }

    public override void Update()
    {
        if (_pending < 0) return;
        _pending -= (float)Time.DeltaTime;
        if (_pending <= 0) { _pending = -1; FireNow(); }
    }

    private void FireNow()
    {
        if (string.IsNullOrEmpty(Target)) return;
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return;
        foreach (var relay in scene.FindObjectsOfType<RelicLogicRelay>())
            if (relay.IsValid() && relay != this && relay.RelayName.Equals(Target, StringComparison.OrdinalIgnoreCase)) relay.Fire();
        foreach (var door in scene.FindObjectsOfType<RelicMover>())
            if (door.IsValid() && door.MoverName.Equals(Target, StringComparison.OrdinalIgnoreCase)) door.Toggle();
    }
}

[AddComponentMenu("Relic/Logic/Counter")]
public sealed class RelicLogicCounter : MonoBehaviour
{
    public string CounterName = string.Empty;
    public string Target = string.Empty;
    public int RequiredCount = 3;
    [SerializeIgnore] public int Current;
    public void Increment()
    {
        Current++;
        var scene = GameObject.Scene;
        if (Current >= RequiredCount && scene.IsValid())
            foreach (var r in scene.FindObjectsOfType<RelicLogicRelay>())
                if (r.IsValid() && r.RelayName.Equals(Target, StringComparison.OrdinalIgnoreCase)) r.Fire();
    }
    public void Reset() => Current = 0;
}

[AddComponentMenu("Relic/Logic/Timer")]
public sealed class RelicLogicTimer : MonoBehaviour
{
    public string TimerName = string.Empty;
    public string Target = string.Empty;
    public float IntervalSeconds = 5f;
    public bool StartOn = true;
    [SerializeIgnore] private float _t;
    public override void Update()
    {
        if (!StartOn) return;
        _t += (float)Time.DeltaTime;
        if (_t < IntervalSeconds) return;
        _t = 0;
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return;
        foreach (var r in scene.FindObjectsOfType<RelicLogicRelay>())
            if (r.IsValid() && r.RelayName.Equals(Target, StringComparison.OrdinalIgnoreCase)) r.Fire();
    }
}

[AddComponentMenu("Relic/Logic/Random")]
public sealed class RelicLogicRandom : MonoBehaviour
{
    public int Options = 2;
    public int Seed = 12345;
    public List<string> Targets = new();
    [SerializeIgnore] private Random? _rng;
    public string Roll()
    {
        _rng ??= new Random(Seed);
        if (Targets.Count == 0) return string.Empty;
        return Targets[_rng.Next(Targets.Count)];
    }
}

[AddComponentMenu("Relic/Logic/Compare")]
public sealed class RelicLogicCompare : MonoBehaviour
{
    public float A, B;
    public string Operator = "==";
    public string Target = string.Empty;
    public bool Evaluate() => Operator switch
    {
        "==" => A == B, "!=" => A != B,
        ">" => A > B, ">=" => A >= B,
        "<" => A < B, "<=" => A <= B,
        _ => false
    };
}

// ------------------------------------------------------------------ movers ---

public enum RelicMoverKind { Door, DoorRotating, Button, Elevator, Platform, Train }

/// <summary>Moving geometry (func_door/_rotating/_button/_elevator/_platform/_train).</summary>
[AddComponentMenu("Relic/Movers/Mover")]
public sealed class RelicMover : MonoBehaviour
{
    public RelicMoverKind Kind = RelicMoverKind.Door;
    public string MoverName = string.Empty;
    public string Target = string.Empty;
    public float Speed = 3f;
    public float WaitSeconds = 3f;
    public bool Locked;
    public Float3 MoveOffset = new(0, 3, 0);
    public float RotateDegrees = 90f;

    [SerializeIgnore] private Float3 _closedPos;
    [SerializeIgnore] private Float3 _openPos;
    [SerializeIgnore] private bool _open;
    [SerializeIgnore] private bool _moving;
    [SerializeIgnore] private float _waitT;
    [SerializeIgnore] private bool _init;

    public override void Start()
    {
        _closedPos = Transform.Position;
        _openPos = _closedPos + MoveOffset;
        _init = true;
    }

    public void Toggle()
    {
        if (Locked || _moving) return;
        _open = !_open;
        _moving = true;
    }

    public void Open() { if (!_open) Toggle(); }
    public void Close() { if (_open) Toggle(); }

    public override void Update()
    {
        if (!_init) { _closedPos = Transform.Position; _openPos = _closedPos + MoveOffset; _init = true; }
        if (!_moving)
        {
            if (_open && WaitSeconds > 0 && Kind is RelicMoverKind.Door or RelicMoverKind.DoorRotating)
            {
                _waitT += (float)Time.DeltaTime;
                if (_waitT >= WaitSeconds) { _waitT = 0; Toggle(); }
            }
            return;
        }
        var goal = _open ? _openPos : _closedPos;
        var to = goal - Transform.Position;
        float step = Speed * (float)Time.DeltaTime;
        if (Float3.Length(to) <= Math.Max(0.02f, step))
        {
            Transform.Position = goal;
            _moving = false;
            _waitT = 0;
        }
        else Transform.Position += Float3.NormalizeSafe(to, Float3.Zero) * step;
    }
}

// --------------------------------------------------------------- objectives ---

[AddComponentMenu("Relic/Mission/Objective")]
public sealed class RelicObjective : MonoBehaviour
{
    public string ObjectiveId = "obj1";
    public string Description = string.Empty;
    public int Required = 1;
    [SerializeIgnore] public int Progress;
    [SerializeIgnore] public bool Completed;
    public void Complete()
    {
        Progress = Required;
        Completed = true;
        RelicObjectiveManager.Notify(ObjectiveId, true);
    }
    public void AddProgress(int amount = 1)
    {
        if (Completed) return;
        Progress += amount;
        if (Progress >= Required) Complete();
    }
}

public static class RelicObjectiveManager
{
    public static event Action<string, bool>? Changed;
    public static void Notify(string id, bool completed) => Changed?.Invoke(id, completed);
}

[AddComponentMenu("Relic/Mission/Checkpoint")]
public sealed class RelicCheckpoint : MonoBehaviour
{
    public string CheckpointId = "cp1";
    public override void OnTriggerEnter(Rigidbody3D other)
    {
        if (!other.GameObject.GetComponent<RelicPlayerController>().IsValid()) return;
        RelicObjectiveManager.Notify(CheckpointId, true);
    }
}

[AddComponentMenu("Relic/Mission/Mission Gate")]
public sealed class RelicMissionGate : MonoBehaviour
{
    public bool IsEnd;
    public string Mission = "mission1";
}

// -------------------------------------------------------------- environment ---

[AddComponentMenu("Relic/Environment/Fog")]
public sealed class RelicFog : MonoBehaviour
{
    public enum RelicFogMode { Off, Linear, Exponential, ExponentialSquared }

    // ---- Analytic (distance) fog: written straight into Scene.Fog ----
    public RelicFogMode Mode = RelicFogMode.ExponentialSquared;
    public Color FogColor = new(0.47f, 0.55f, 0.7f, 1f);
    public float Density = 0.02f;
    public float FogStart = 8f;
    public float FogEnd = 90f;

    // ---- Volumetric (ray-marched) fog: managed on every scene camera ----
    /// <summary>When true, RelicFog keeps a VolumetricFogEffect on each camera in sync.</summary>
    public bool Volumetric = true;
    /// <summary>Base world-space density marched everywhere (volumes add on top).</summary>
    public float VolumetricDensity = 0.02f;
    /// <summary>Henyey-Greenstein anisotropy: 0 = isotropic, &gt;0 = forward-scattering.</summary>
    public float VolumetricScattering = 0.5f;
    /// <summary>How far the march reaches, in meters.</summary>
    public float VolumetricMaxDistance = 100f;
    /// <summary>Sky-bounce ambient so shadowed fog keeps color instead of going black.</summary>
    public float VolumetricAmbientIntensity = 0.3f;
    /// <summary>Uncheck to leave camera effects entirely alone (manual setup).</summary>
    public bool ManageVolumetricEffect = true;

    /// <summary>True while this component owns auto-added camera effects.</summary>
    public bool AutoVolumetric;

    public override void OnEnable() => Apply();
    public override void OnValidate() => Apply();

    /// <summary>
    /// Push everything to the scene: analytic fog params plus the volumetric
    /// effect on every camera. Idempotent — safe to call on every edit.
    /// </summary>
    public void Apply()
    {
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return;

        var fog = scene.Fog;
        fog.Mode = Mode switch
        {
            RelicFogMode.Off => Resources.Scene.FogParams.FogMode.Off,
            RelicFogMode.Linear => Resources.Scene.FogParams.FogMode.Linear,
            RelicFogMode.Exponential => Resources.Scene.FogParams.FogMode.Exponential,
            _ => Resources.Scene.FogParams.FogMode.ExponentialSquared
        };
        fog.Color = FogColor;
        fog.Density = Math.Max(0f, Density);
        fog.Start = Math.Max(0f, FogStart);
        fog.End = Math.Max(fog.Start + 0.01f, FogEnd);
        scene.Fog = fog;

        if (ManageVolumetricEffect)
            SyncVolumetricEffects(scene);
    }

    private void SyncVolumetricEffects(Resources.Scene scene)
    {
        foreach (var cam in scene.FindObjectsOfType<Camera>())
        {
            if (!cam.IsValid()) continue;
            Rendering.VolumetricFogEffect? existing = null;
            foreach (var fx in cam.Effects)
                if (fx is Rendering.VolumetricFogEffect vf) { existing = vf; break; }

            if (Volumetric && Mode != RelicFogMode.Off)
            {
                var effect = existing;
                if (effect == null)
                {
                    effect = new Rendering.VolumetricFogEffect();
                    cam.Effects.Add(effect);
                    AutoVolumetric = true;
                }
                effect.GlobalDensity = Math.Max(0f, VolumetricDensity);
                effect.GlobalColorTint = FogColor;
                effect.Scattering = VolumetricScattering;
                effect.MaxDistance = Math.Max(0.1f, VolumetricMaxDistance);
                effect.AmbientIntensity = Math.Max(0f, VolumetricAmbientIntensity);
            }
            else if (existing != null && AutoVolumetric)
            {
                cam.Effects.Remove(existing);
                try { existing.OnDisable(); } catch { }
                AutoVolumetric = false;
            }
        }
    }
}

[AddComponentMenu("Relic/Environment/Post Process")]
public sealed class RelicPostProcess : MonoBehaviour
{
    public bool BloomEnabled = true;
    public float Exposure = 1.1f;
    public float BloomIntensity = 1f;
    public float BloomThreshold = 1f;
    public string ToneMap = "ACES";
}

[AddComponentMenu("Relic/Environment/Weather")]
public sealed class RelicWeather : MonoBehaviour
{
    public enum WeatherKind { Rain, Snow, Wind, Lightning }
    public WeatherKind Kind = WeatherKind.Rain;
    public float Density = 0.5f;
    public float Intensity = 1f;
    public Float3 Direction = new(1, 0, 0);
}

[AddComponentMenu("Relic/Environment/Skybox")]
public sealed class RelicSkybox : MonoBehaviour
{
    public string SkyboxName = string.Empty;
}

[AddComponentMenu("Relic/Environment/Particle")]
public sealed class RelicParticle : MonoBehaviour
{
    public string Effect = "smoke";
    public float SpawnRate = 10f;
    public float Lifetime = 1f;
    public Float3 Velocity = new(0, 0, 1);
    public float Size = 4f;
    public Color Tint = Color.White;
}

// ------------------------------------------------------------------- audio ---

[AddComponentMenu("Relic/Audio/Ambient Sound")]
[RequireComponent(typeof(AudioSource))]
public sealed class RelicAmbientSound : MonoBehaviour
{
    public string Sound = string.Empty;
    public float Volume = 1f;
    public float Radius = 16f;
    public bool Loop = true;
    public override void Start()
    {
        var src = GetComponent<AudioSource>();
        if (!src.IsValid()) return;
        // Clip is resolved by path at play time via RelicAudio helper.
        src.Loop = Loop;
    }
}

[AddComponentMenu("Relic/Audio/Music Player")]
public sealed class RelicMusicPlayerComp : MonoBehaviour
{
    public string Track = string.Empty;
    public float Volume = 0.8f;
    public bool Loop = true;
    public bool PlayOnStart = true;
    public override void Start()
    {
        if (PlayOnStart && !string.IsNullOrEmpty(Track)) RelicMusicPlayer.Play(Track, Volume);
    }
}

public static class RelicAudio
{
    public static void PlayOneShot(string sound, Float3 position) =>
        Debug.Log($"[RelicAudio] one-shot '{sound}' at {position}");
}

public static class RelicMusicPlayer
{
    public static string? CurrentTrack { get; private set; }
    public static void Play(string track, float volume = 0.8f)
    {
        CurrentTrack = track;
        Debug.Log($"[RelicMusic] playing '{track}' vol={volume}");
    }
    public static void Stop() => CurrentTrack = null;
}
