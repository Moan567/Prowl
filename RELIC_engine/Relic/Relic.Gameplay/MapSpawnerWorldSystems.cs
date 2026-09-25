namespace Relic.Gameplay;

using System.Globalization;
using Relic.Content;
using Relic.Framework;
using Relic.Gameplay.Components;

// Phase 10: data-driven world-system spawning. Every new TrenchBroom entity
// class maps to components here purely from .map properties.
public static partial class MapSpawner
{
    private static float GetFloat(MapEntity e, string key, float fallback)
        => e.Properties.TryGetValue(key, out var s) &&
           float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int GetInt(MapEntity e, string key, int fallback)
        => e.Properties.TryGetValue(key, out var s) && int.TryParse(s, out var v) ? v : fallback;

    private static string GetString(MapEntity e, string key, string fallback)
        => e.Properties.TryGetValue(key, out var s) ? s : fallback;

    private static bool GetBool(MapEntity e, string key, bool fallback)
    {
        if (!e.Properties.TryGetValue(key, out var s)) return fallback;
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static Vector3 ParseVec3(MapEntity e, string key, Vector3 fallback)
    {
        if (!e.Properties.TryGetValue(key, out var s)) return fallback;
        var parts = s.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return new Vector3(x, y, z);
        return fallback;
    }

    private static void SpawnWorldSystems(Entity entity, MapEntity mapEntity)
    {
        string className = mapEntity.ClassName.ToLowerInvariant();
        var props = mapEntity.Properties;

        // ---- Audio entities ----
        if (className == "ambient_sound")
        {
            var c = entity.GetComponent<AmbientSoundComponent>() ?? entity.AddComponent(new AmbientSoundComponent());
            c.Sound = GetString(mapEntity, "sound", "forest_wind.wav");
            c.Volume = GetFloat(mapEntity, "volume", 1f);
            c.Radius = GetFloat(mapEntity, "radius", 512f);
            c.Loop = GetBool(mapEntity, "loop", true);
        }
        if (className == "sound_emitter")
        {
            var c = entity.GetComponent<SoundEmitterComponent>() ?? entity.AddComponent(new SoundEmitterComponent());
            c.Sound = GetString(mapEntity, "sound", "");
            c.Volume = GetFloat(mapEntity, "volume", 1f);
            c.Radius = GetFloat(mapEntity, "radius", 256f);
            c.Interval = GetFloat(mapEntity, "interval", 3f);
        }
        if (className == "sound_zone")
        {
            var c = entity.GetComponent<SoundZoneComponent>() ?? entity.AddComponent(new SoundZoneComponent());
            c.Sound = GetString(mapEntity, "sound", "");
            c.Radius = GetFloat(mapEntity, "radius", 256f);
            c.Loop = GetBool(mapEntity, "loop", true);
        }
        if (className == "music_player")
        {
            var c = entity.GetComponent<MusicPlayerComponent>() ?? entity.AddComponent(new MusicPlayerComponent());
            c.Track = GetString(mapEntity, "track", "");
            c.Volume = GetFloat(mapEntity, "volume", 0.8f);
            c.Loop = GetBool(mapEntity, "loop", true);
        }
        if (className == "music_trigger")
        {
            var c = entity.GetComponent<MusicTriggerComponent>() ?? entity.AddComponent(new MusicTriggerComponent());
            c.Track = GetString(mapEntity, "track", "");
            c.Volume = GetFloat(mapEntity, "volume", 0.8f);
        }
        if (className == "reverb_zone")
        {
            var c = entity.GetComponent<ReverbZoneComponent>() ?? entity.AddComponent(new ReverbZoneComponent());
            c.Radius = GetFloat(mapEntity, "radius", 256f);
            c.Amount = GetFloat(mapEntity, "amount", 0.5f);
        }

        // ---- Triggers ----
        TriggerKind? triggerKind = className switch
        {
            "trigger_multiple" => TriggerKind.Multiple,
            "trigger_once" => TriggerKind.Once,
            "trigger_hurt" => TriggerKind.Hurt,
            "trigger_heal" => TriggerKind.Heal,
            "trigger_push" => TriggerKind.Push,
            "trigger_teleport" => TriggerKind.Teleport,
            "trigger_sound" => TriggerKind.Sound,
            "trigger_music" => TriggerKind.Music,
            _ => null,
        };
        if (triggerKind.HasValue)
        {
            var c = entity.GetComponent<TriggerComponent>() ?? entity.AddComponent(new TriggerComponent());
            c.Kind = triggerKind.Value;
            c.Damage = GetFloat(mapEntity, "damage", 10f);
            c.Interval = GetFloat(mapEntity, "interval", 1f);
            c.Amount = GetFloat(mapEntity, "amount", 25f);
            c.Force = GetFloat(mapEntity, "force", 300f);
            c.Direction = ParseVec3(mapEntity, "direction", Vector3.Forward);
            c.Target = GetString(mapEntity, "target", "");
            c.Sound = GetString(mapEntity, "sound", "");
            c.Track = GetString(mapEntity, "track", "");
            c.Radius = GetFloat(mapEntity, "radius", 64f);
        }

        // ---- Doors & movers ----
        DoorKind? doorKind = className switch
        {
            "func_door" => DoorKind.Door,
            "func_door_rotating" => DoorKind.RotatingDoor,
            "func_button" => DoorKind.Button,
            "func_elevator" => DoorKind.Elevator,
            "func_platform" => DoorKind.Platform,
            "func_train" => DoorKind.Train,
            _ => null,
        };
        if (doorKind.HasValue)
        {
            var c = entity.GetComponent<DoorComponent>() ?? entity.AddComponent(new DoorComponent());
            c.Kind = doorKind.Value;
            c.Speed = GetFloat(mapEntity, "speed", 100f);
            c.Wait = GetFloat(mapEntity, "wait", 3f);
            c.Locked = GetBool(mapEntity, "locked", false);
            c.Distance = GetFloat(mapEntity, "distance", 64f);
            c.RotateAngle = GetFloat(mapEntity, "angle_amount", 90f);
            c.Direction = ParseVec3(mapEntity, "direction", new Vector3(0, 0, 1));
        }

        // ---- Logic ----
        if (className == "logic_timer")
        {
            var c = entity.GetComponent<LogicTimerComponent>() ?? entity.AddComponent(new LogicTimerComponent());
            c.Interval = GetFloat(mapEntity, "interval", 5f);
            c.StartOn = GetBool(mapEntity, "start_on", true);
        }
        if (className == "logic_random")
        {
            var c = entity.GetComponent<LogicRandomComponent>() ?? entity.AddComponent(new LogicRandomComponent());
            c.Options = GetInt(mapEntity, "options", 2);
            c.Seed = GetInt(mapEntity, "seed", 12345);
        }
        if (className == "logic_relay")
        {
            var c = entity.GetComponent<LogicRelayComponent>() ?? entity.AddComponent(new LogicRelayComponent());
            c.Target = GetString(mapEntity, "target", "");
        }
        if (className == "logic_counter")
        {
            var c = entity.GetComponent<LogicCounterComponent>() ?? entity.AddComponent(new LogicCounterComponent());
            c.TargetCount = GetInt(mapEntity, "count", 3);
        }
        if (className == "logic_compare")
        {
            var c = entity.GetComponent<LogicCompareComponent>() ?? entity.AddComponent(new LogicCompareComponent());
            c.A = GetFloat(mapEntity, "a", 0f);
            c.B = GetFloat(mapEntity, "b", 0f);
            c.Op = GetString(mapEntity, "op", "==");
        }

        // ---- AI pathing ----
        if (className == "path_corner")
        {
            var c = entity.GetComponent<PathCornerComponent>() ?? entity.AddComponent(new PathCornerComponent());
            c.Target = GetString(mapEntity, "target", "");
            c.Wait = GetFloat(mapEntity, "wait", 0f);
        }
        if (className == "path_patrol")
        {
            var c = entity.GetComponent<PatrolComponent>() ?? entity.AddComponent(new PatrolComponent());
            c.Speed = GetFloat(mapEntity, "speed", 120f);
            c.Loop = GetBool(mapEntity, "loop", true);
        }
        if (className == "info_enemy_spawn")
        {
            var c = entity.GetComponent<EnemySpawnComponent>() ?? entity.AddComponent(new EnemySpawnComponent());
            c.EnemyClass = GetString(mapEntity, "enemy", "enemy_soldier");
            c.Count = GetInt(mapEntity, "count", 1);
        }
        // Enemies with a patrol route get a PatrolComponent automatically.
        if (className is "enemy_soldier" or "enemy_dog" or "enemy_demon" &&
            props.TryGetValue("patrol", out var patrolTag) && !string.IsNullOrWhiteSpace(patrolTag))
        {
            var c = entity.GetComponent<PatrolComponent>() ?? entity.AddComponent(new PatrolComponent());
            c.Speed = GetFloat(mapEntity, "patrol_speed", 120f);
            entity.Properties["patrol_start"] = patrolTag;
        }

        // ---- Particles ----
        ParticleEffectKind? fxKind = className switch
        {
            "env_particles" => null, // kind comes from "effect" property below
            "fx_smoke" => ParticleEffectKind.Smoke,
            "fx_fire" => ParticleEffectKind.Fire,
            "fx_sparks" => ParticleEffectKind.Sparks,
            "fx_blood" => ParticleEffectKind.Blood,
            "fx_explosion" => ParticleEffectKind.Explosion,
            "fx_muzzleflash" => ParticleEffectKind.Muzzleflash,
            _ => null,
        };
        if (className == "env_particles" || fxKind.HasValue)
        {
            var c = entity.GetComponent<ParticleEffectComponent>() ?? entity.AddComponent(new ParticleEffectComponent());
            if (fxKind.HasValue) c.Kind = fxKind.Value;
            else if (props.TryGetValue("effect", out var fx) && Enum.TryParse(fx, true, out ParticleEffectKind parsed)) c.Kind = parsed;
            c.SpawnRate = GetFloat(mapEntity, "spawnrate", 10f);
            c.Lifetime = GetFloat(mapEntity, "lifetime", 1f);
            c.Velocity = ParseVec3(mapEntity, "velocity", new Vector3(0, 0, 32));
            c.Size = GetFloat(mapEntity, "size", 4f);
            c.Color = ParseVec3(mapEntity, "color", new Vector3(1, 1, 1));
        }

        // ---- Weather / environment ----
        WeatherKind? weatherKind = className switch
        {
            "env_rain" => WeatherKind.Rain,
            "env_snow" => WeatherKind.Snow,
            "env_lightning" => WeatherKind.Lightning,
            "env_wind" => WeatherKind.Wind,
            "env_skybox" => WeatherKind.Skybox,
            "env_ambient" => WeatherKind.Ambient,
            _ => null,
        };
        if (weatherKind.HasValue)
        {
            var c = entity.GetComponent<WeatherComponent>() ?? entity.AddComponent(new WeatherComponent());
            c.Kind = weatherKind.Value;
            c.Density = GetFloat(mapEntity, "density", 0.5f);
            c.Intensity = GetFloat(mapEntity, "intensity", 1f);
            c.Skybox = GetString(mapEntity, "skybox", "");
            c.Direction = ParseVec3(mapEntity, "direction", new Vector3(0, 0, -1));
        }

        // ---- Pickups ----
        if (className == "ammo_pickup")
        {
            var c = entity.GetComponent<AmmoPickupComponent>() ?? entity.AddComponent(new AmmoPickupComponent());
            c.AmmoType = GetString(mapEntity, "ammo_type", "rifle");
            c.Amount = GetInt(mapEntity, "amount", 10);
        }
        if (className == "armor_pickup")
        {
            var c = entity.GetComponent<ArmorPickupComponent>() ?? entity.AddComponent(new ArmorPickupComponent());
            c.Amount = GetInt(mapEntity, "amount", 50);
        }
        if (className == "keycard_pickup")
        {
            var c = entity.GetComponent<KeycardPickupComponent>() ?? entity.AddComponent(new KeycardPickupComponent());
            c.KeyId = GetString(mapEntity, "key", "red");
        }
        if (className == "weapon_spawn")
        {
            var c = entity.GetComponent<WeaponSpawnComponent>() ?? entity.AddComponent(new WeaponSpawnComponent());
            c.WeaponName = GetString(mapEntity, "weapon", "Shotgun");
            c.RespawnTime = GetFloat(mapEntity, "respawn", 10f);
        }

        // ---- Mission ----
        if (className == "objective")
        {
            var c = entity.GetComponent<ObjectiveComponent>() ?? entity.AddComponent(new ObjectiveComponent());
            c.ObjectiveId = GetString(mapEntity, "objective_id", "obj1");
            c.Description = GetString(mapEntity, "description", "");
            c.Required = GetInt(mapEntity, "required", 1);
        }
        if (className == "checkpoint")
        {
            var c = entity.GetComponent<CheckpointComponent>() ?? entity.AddComponent(new CheckpointComponent());
            c.CheckpointId = GetString(mapEntity, "checkpoint_id", "cp1");
        }
        if (className == "mission_start")
        {
            var c = entity.GetComponent<MissionStartComponent>() ?? entity.AddComponent(new MissionStartComponent());
            c.MissionName = GetString(mapEntity, "mission", "mission1");
        }
        if (className == "mission_end")
        {
            _ = entity.GetComponent<MissionEndComponent>() ?? entity.AddComponent(new MissionEndComponent());
        }
    }
}
