// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// Entity browser: every TrenchBroom entity Relic understands, searchable.
// Brush entities are placed in TrenchBroom; point entities can be dropped into the open scene.

using System;
using System.Linq;

using Prowl.Editor.Core;
using Prowl.Editor.GUI.SceneView;
using Prowl.Editor.Theming;
using Prowl.OrigamiUI;
using Prowl.PaperUI;
using Prowl.PaperUI.LayoutEngine;
using Prowl.Runtime;
using Prowl.Runtime.Relic;
using Prowl.Runtime.Resources;

using static Prowl.Editor.GUI.EditorGUI;

namespace Prowl.Editor.GUI.Panels.Relic;

public class RelicEntityBrowserPanel : DockPanel
{
    [MenuItem("Window/Relic/Entity Browser", priority: 52)]
    static void Open() => EditorApplication.Instance?.OpenPanel(typeof(RelicEntityBrowserPanel));

    public override string Title => "Entity Browser";
    public override string Icon => EditorIcons.Cubes;

    private static string _search = string.Empty;

    private readonly struct Entry
    {
        public readonly string ClassName;
        public readonly string Category;
        public readonly string Description;
        public readonly bool PointEntity;
        public Entry(string cls, string cat, string desc, bool point = true)
        { ClassName = cls; Category = cat; Description = desc; PointEntity = point; }
    }

    private static readonly Entry[] All =
    {
        new("player_start", "Player", "Player spawn. Requires at least one per map."),
        new("enemy_soldier", "Enemies", "Rifle soldier (100 HP)."),
        new("enemy_dog", "Enemies", "Fast melee dog (50 HP)."),
        new("enemy_demon", "Enemies", "Heavy demon (300 HP)."),
        new("info_enemy_spawn", "Enemies", "Timed spawner. Set enemy + count."),
        new("path_corner", "Enemies", "Patrol waypoint. Link via target/targetname."),
        new("path_patrol", "Enemies", "Patrol route marker."),
        new("weapon_pickup", "Items", "Grants weapon (weapon key)."),
        new("ammo_pickup", "Items", "Ammo (ammo_type + amount)."),
        new("health_pickup", "Items", "Heals (heal amount)."),
        new("armor_pickup", "Items", "Armor (amount)."),
        new("keycard_pickup", "Items", "Key (key = red/blue/gold)."),
        new("light", "Environment", "Point light (color, intensity, radius)."),
        new("light_spot", "Environment", "Spot light (angles aim)."),
        new("light_sun", "Environment", "Directional sun (angles aim)."),
        new("env_fog", "Environment", "Fog color + density."),
        new("env_postprocess", "Environment", "Bloom, exposure, tonemap."),
        new("env_rain", "Environment", "Rain weather."),
        new("env_snow", "Environment", "Snow weather."),
        new("env_wind", "Environment", "Wind weather + direction."),
        new("env_lightning", "Environment", "Lightning weather."),
        new("env_skybox", "Environment", "Skybox selector."),
        new("env_particles", "Environment", "Particle effect + rate."),
        new("fx_smoke", "Environment", "Smoke puff emitter."),
        new("fx_fire", "Environment", "Fire emitter."),
        new("fx_sparks", "Environment", "Spark emitter."),
        new("fx_blood", "Environment", "Blood hit emitter."),
        new("fx_explosion", "Environment", "Explosion emitter."),
        new("fx_muzzleflash", "Environment", "Muzzle flash emitter."),
        new("ambient_sound", "Audio", "Looping ambient (sound, radius)."),
        new("sound_emitter", "Audio", "Interval sound emitter."),
        new("music_player", "Audio", "Music track player."),
        new("music_trigger", "Audio", "Music change on touch.", false),
        new("reverb_zone", "Audio", "Reverb region marker."),
        new("trigger_once", "Triggers", "Fires target a single time.", false),
        new("trigger_multiple", "Triggers", "Fires target every touch.", false),
        new("trigger_hurt", "Triggers", "Damage volume (damage, interval).", false),
        new("trigger_heal", "Triggers", "Heal volume (amount).", false),
        new("trigger_push", "Triggers", "Push volume (direction, force).", false),
        new("trigger_teleport", "Triggers", "Teleports to target.", false),
        new("trigger_music", "Triggers", "Music switch volume.", false),
        new("trigger_sound", "Triggers", "One-shot sound volume.", false),
        new("logic_timer", "Logic", "Fires target on interval."),
        new("logic_random", "Logic", "Random branch."),
        new("logic_relay", "Logic", "Target forwarding relay."),
        new("logic_counter", "Logic", "Fires after N inputs."),
        new("logic_compare", "Logic", "Compares A vs B."),
        new("func_door", "Movers", "Sliding door (brushes in TrenchBroom).", false),
        new("func_door_rotating", "Movers", "Rotating door.", false),
        new("func_button", "Movers", "Trigger button.", false),
        new("func_elevator", "Movers", "Elevator platform.", false),
        new("func_platform", "Movers", "Moving platform.", false),
        new("func_train", "Movers", "Path train.", false),
        new("objective", "Objectives", "Mission objective (id, description)."),
        new("checkpoint", "Objectives", "Checkpoint + respawn notify."),
        new("mission_start", "Objectives", "Mission start gate."),
        new("mission_end", "Objectives", "Mission end gate."),
    };

    public override void OnGUI(Paper paper, float width, float height)
    {
        var font = EditorTheme.DefaultFont;
        if (font == null) return;

        using (paper.Column("ent_root").Width(width).Height(height).Padding(0, 0, 8, 12).Gap(8).Enter())
        {
            SectionHeader(paper, "ent_h", "Relic Entity Browser", first: true);

            SettingsRow(paper, "ent_search", "Search", () =>
                Origami.TextField(paper, "ent_search_v", _search, v => _search = v).Show());

            var query = _search.Trim().ToLowerInvariant();
            var shown = All.Where(e => string.IsNullOrEmpty(query)
                || e.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

            Origami.ScrollView(paper, "ent_scroll", width - 24, Math.Max(120, height - 130)).Body(() =>
            {
                using (paper.Column("ent_list").Height(UnitValue.Auto).Gap(4).Enter())
                {
                    string lastCat = string.Empty;
                    foreach (var e in shown)
                    {
                        if (e.Category != lastCat)
                        {
                            lastCat = e.Category;
                            paper.Box($"ent_cat_{lastCat}").Height(22)
                                .Text(lastCat, font).TextColor(EditorTheme.Amber400)
                                .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);
                        }
                        using (paper.Row($"ent_{e.ClassName}").Height(UnitValue.Auto).MinHeight(26).Gap(8).Enter())
                        {
                            paper.Box($"ent_{e.ClassName}_t").Height(UnitValue.Auto)
                                .Text($"{e.ClassName} — {e.Description}", font)
                                .TextColor(EditorTheme.Ink400).FontSize(EditorTheme.FontSizeSmall)
                                .Alignment(TextAlignment.MiddleLeft);
                            if (e.PointEntity)
                                Origami.Button(paper, $"ent_{e.ClassName}_add", $"{EditorIcons.Plus} Add", () => AddPointEntity(e.ClassName)).Show();
                        }
                    }
                    if (shown.Length == 0)
                        paper.Box("ent_none").Height(24)
                            .Text("No entities match. Brush entities (func_*, trigger_*) are placed in TrenchBroom.", font)
                            .TextColor(EditorTheme.Ink300).FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleCenter);
                }
            });
        }
    }

    private static void AddPointEntity(string classname)
    {
        var scene = Scene.Current;
        if (scene == null || !scene.IsValid())
        {
            Toasts.Warning("Relic", "Open a scene first.");
            return;
        }
        var go = new GameObject(classname);
        switch (classname)
        {
            case "player_start": go.AddComponent<RelicPlayerSpawn>(); break;
            case "enemy_soldier": { var c = go.AddComponent<RelicEnemy>(); c.Kind = RelicEnemyKind.Soldier; break; }
            case "enemy_dog": { var c = go.AddComponent<RelicEnemy>(); c.Kind = RelicEnemyKind.Dog; c.MaxHealth = c.Health = 50; break; }
            case "enemy_demon": { var c = go.AddComponent<RelicEnemy>(); c.Kind = RelicEnemyKind.Demon; c.MaxHealth = c.Health = 300; break; }
            case "info_enemy_spawn": go.AddComponent<RelicEnemySpawn>(); break;
            case "path_corner": go.AddComponent<RelicPathCorner>(); break;
            case "path_patrol": go.AddComponent<RelicPathCorner>(); break;
            case "weapon_pickup": { var c = go.AddComponent<RelicPickup>(); c.Kind = RelicPickup.PickupKind.Weapon; break; }
            case "ammo_pickup": { var c = go.AddComponent<RelicPickup>(); c.Kind = RelicPickup.PickupKind.Ammo; break; }
            case "health_pickup": { var c = go.AddComponent<RelicPickup>(); c.Kind = RelicPickup.PickupKind.Health; break; }
            case "armor_pickup": { var c = go.AddComponent<RelicPickup>(); c.Kind = RelicPickup.PickupKind.Armor; break; }
            case "keycard_pickup": { var c = go.AddComponent<RelicPickup>(); c.Kind = RelicPickup.PickupKind.Keycard; break; }
            case "light": go.AddComponent<PointLight>(); break;
            case "light_spot": go.AddComponent<SpotLight>(); break;
            case "light_sun": go.AddComponent<DirectionalLight>(); break;
            case "env_fog": go.AddComponent<RelicFog>(); break;
            case "env_postprocess": go.AddComponent<RelicPostProcess>(); break;
            case "env_rain":
            case "env_snow":
            case "env_wind":
            case "env_lightning": go.AddComponent<RelicWeather>(); break;
            case "env_skybox": go.AddComponent<RelicSkybox>(); break;
            case "ambient_sound":
            case "sound_emitter": go.AddComponent<RelicAmbientSound>(); break;
            case "music_player": go.AddComponent<RelicMusicPlayerComp>(); break;
            case "objective": go.AddComponent<RelicObjective>(); break;
            case "checkpoint": go.AddComponent<RelicCheckpoint>(); break;
            case "mission_start":
            case "mission_end": { var c = go.AddComponent<RelicMissionGate>(); c.IsEnd = classname == "mission_end"; break; }
            case "logic_timer": go.AddComponent<RelicLogicTimer>(); break;
            case "logic_random": go.AddComponent<RelicLogicRandom>(); break;
            case "logic_relay": go.AddComponent<RelicLogicRelay>(); break;
            case "logic_counter": go.AddComponent<RelicLogicCounter>(); break;
            case "logic_compare": go.AddComponent<RelicLogicCompare>(); break;
            default: break;
        }
        scene.Add(go);
        EditorSceneManager.MarkDirty();
        Toasts.Success("Relic", $"Added {classname} to scene.");
    }
}
