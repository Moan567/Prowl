// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using Prowl.Runtime.Relic.Map;

namespace Prowl.Runtime.Relic;

public sealed class RelicValidationIssue
{
    public string Severity = "warning"; // error | warning | info
    public string Message = string.Empty;
}

public static class RelicMapValidator
{
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        "worldspawn", "player_start", "info_player_start",
        "enemy_soldier", "enemy_dog", "enemy_demon", "info_enemy_spawn",
        "path_corner", "path_patrol",
        "weapon_pickup", "weapon_spawn", "ammo_pickup", "health_pickup", "armor_pickup", "keycard_pickup",
        "light", "light_spot", "light_sun",
        "env_fog", "env_ambient", "env_postprocess", "env_rain", "env_snow", "env_wind", "env_lightning", "env_skybox",
        "env_particles", "fx_smoke", "fx_fire", "fx_sparks", "fx_blood", "fx_explosion", "fx_muzzleflash",
        "ambient_sound", "sound_emitter", "sound_zone", "music_player", "music_trigger", "reverb_zone",
        "trigger_once", "trigger_multiple", "trigger_hurt", "trigger_heal", "trigger_push",
        "trigger_teleport", "trigger_music", "trigger_sound",
        "logic_timer", "logic_random", "logic_relay", "logic_counter", "logic_compare",
        "func_door", "func_door_rotating", "func_button", "func_elevator", "func_platform", "func_train",
        "objective", "checkpoint", "mission_start", "mission_end", "prop_static"
    };

    public static List<RelicValidationIssue> Validate(RelicMapData map)
    {
        var issues = new List<RelicValidationIssue>();
        void Add(string sev, string msg) => issues.Add(new RelicValidationIssue { Severity = sev, Message = msg });

        if (map.Brushes.Count == 0) Add("error", "Map has no brushes — worldspawn is empty.");
        if (!map.Entities.Any(e => e.ClassName.Equals("player_start", StringComparison.OrdinalIgnoreCase) ||
                                   e.ClassName.Equals("info_player_start", StringComparison.OrdinalIgnoreCase)))
            Add("error", "Missing player_start — Press Play will have nowhere to spawn.");
        if (!map.Entities.Any(e => e.ClassName.Equals("light", StringComparison.OrdinalIgnoreCase) ||
                                   e.ClassName.Equals("light_spot", StringComparison.OrdinalIgnoreCase) ||
                                   e.ClassName.Equals("light_sun", StringComparison.OrdinalIgnoreCase)))
            Add("warning", "No lights — the map will render black. Add light / light_sun in TrenchBroom.");

        foreach (var e in map.Entities)
        {
            if (!Known.Contains(e.ClassName))
                Add("warning", $"Unknown entity '{e.ClassName}' — will spawn as marker.");
            if (e.ClassName.StartsWith("trigger_", StringComparison.OrdinalIgnoreCase) && e.Brushes.Count == 0 && !e.HasOrigin)
                Add("warning", $"'{e.ClassName}' has no brushes and no origin — volume will default to 3m cube.");
            if (e.ClassName.StartsWith("func_", StringComparison.OrdinalIgnoreCase) && e.Brushes.Count == 0)
                Add("error", $"'{e.ClassName}' has no brushes — movers need solid geometry.");
            if (e.ClassName.StartsWith("trigger_teleport", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(e.GetString("target")))
                Add("error", "trigger_teleport without target — teleport goes nowhere.");
        }

        // Dangling targets.
        var names = new HashSet<string>(map.Entities.Select(e => e.GetString("targetname"))
            .Where(s => !string.IsNullOrEmpty(s)), StringComparer.OrdinalIgnoreCase);
        foreach (var e in map.Entities)
        {
            var t = e.GetString("target");
            if (!string.IsNullOrEmpty(t) && !names.Contains(t) &&
                !e.ClassName.Equals("path_corner", StringComparison.OrdinalIgnoreCase))
                Add("warning", $"'{e.ClassName}' targets missing '{t}'.");
        }

        if (issues.Count == 0) Add("info", "Map valid — Import Map, then press Play.");
        return issues;
    }
}
