// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// RELIC scene generator: .map (source of truth) -> Prowl scene.
// Pipeline: TrenchBroom -> Valve .map -> RelicMapParser -> RelicBrushBuilder
//           -> RelicSceneBuilder -> Prowl Runtime -> Play.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Prowl.Runtime.Relic.Map;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime.Relic;

public sealed class RelicBuildOptions
{
    /// <summary>Meters per Quake unit. 32 units = 1m (Quake player ~56u = 1.75m).</summary>
    public float UnitScale = RelicBrushBuilder.DefaultUnitScale;
    public bool BuildCollision = true;
    public bool BuildMaterials = true;
    public bool SpawnEntities = true;
    public bool SpawnPlayerRig = true;
    /// <summary>Load real face textures (TB Textures/ layout). Off = procedural colors.</summary>
    public bool LoadTextures = true;
    /// <summary>Game project root; texture search roots derive from it when TextureSearchRoots is empty.</summary>
    public string? ProjectRoot;
    /// <summary>Explicit texture search roots (checked in order). Empty = derived from ProjectRoot.</summary>
    public List<string> TextureSearchRoots { get; } = new();
    public Float2 FallbackTextureSize = new(64, 64);
    /// <summary>Brush entity classes that are volumes, never rendered as solid.</summary>
    public HashSet<string> NonSolidBrushOwners { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "trigger_once", "trigger_multiple", "trigger_hurt", "trigger_heal",
        "trigger_push", "trigger_teleport", "trigger_music", "trigger_sound"
    };
}

public sealed class RelicBuildReport
{
    public int WorldMeshes;
    public int WorldTriangles;
    public int EntitiesSpawned;
    public int Warnings;
    public List<string> Messages { get; } = new();
    public bool HasPlayerSpawn;
    /// <summary>Winding/normal defects found in generated geometry (empty = culling-safe).</summary>
    public List<string> GeometryIssues { get; } = new();
    /// <summary>Face texture names with no image file found (procedural fallback used).</summary>
    public List<string> MissingTextures { get; } = new();
    public int TexturedMaterials;
    public int FallbackMaterials;
}

public static class RelicSceneBuilder
{
    /// <summary>Convert Quake origin (inches, Z-up) to Prowl (meters, Y-up).</summary>
    public static Float3 ToProwl(Float3 quakeOrigin, float unitScale) =>
        RelicBrushBuilder.QuakeToProwl(quakeOrigin, unitScale);

    /// <summary>Quake yaw degrees -> Prowl Euler Y degrees (handedness flip).</summary>
    public static float QuakeYawToProwl(float quakeYaw) => -quakeYaw;

    public static RelicBuildReport Build(RelicMapData map, Scene scene, RelicBuildOptions? options = null)
    {
        options ??= new RelicBuildOptions();
        var report = new RelicBuildReport();

        // Snapshot before building so every GameObject created below can be tagged
        // as map-generated. Hot-reload clears exactly those on the next import.
        var before = new HashSet<GameObject>(scene.AllObjects);
        string? sourcePath = map.SourcePath;

        // ---- worldspawn geometry (skip trigger volumes + mover brushes) ----
        var moverOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "func_door", "func_door_rotating", "func_button",
            "func_elevator", "func_platform", "func_train"
        };
        var worldBrushes = map.Brushes
            .Where(b => !options.NonSolidBrushOwners.Contains(b.OwnerClassName)
                     && !moverOwners.Contains(b.OwnerClassName))
            .ToList();

        var worldRoot = new GameObject("Relic Worldspawn");
        worldRoot.IsStatic = true;
        scene.Add(worldRoot);

        // ---- textures: resolve once so UVs use real texel sizes ----
        RelicTextureSet? textures = null;
        if (options.LoadTextures)
        {
            var roots = options.TextureSearchRoots.Count > 0
                ? options.TextureSearchRoots
                : RelicTextureLibrary.GetDefaultSearchRoots(options.ProjectRoot);
            textures = RelicTextureLibrary.LoadForMap(map, roots, options.FallbackTextureSize);
            foreach (var m in textures.Missing)
            {
                report.MissingTextures.Add(m);
                report.Messages.Add($"Texture '{m}' not found — procedural fallback color used.");
            }
            if (textures.Missing.Count > 0) report.Warnings += textures.Missing.Count;
        }

        Float2 TexSize(string name) => textures != null ? textures.GetSize(name) : options.FallbackTextureSize;

        var geo = RelicBrushBuilder.BuildBrushes(worldBrushes, options.UnitScale, TexSize);
        // Self-check: generated triangles must be culling-safe (winding agrees
        // with normals). Any issue here is logged, never silently swallowed.
        foreach (var issue in RelicGeometryValidator.ValidateMesh(geo))
        {
            report.Warnings++;
            report.GeometryIssues.Add($"Worldspawn {issue}");
        }
        var groups = RelicBrushBuilder.GroupByTexture(geo);
        foreach (var g in groups)
        {
            var mesh = RelicBrushBuilder.BuildMesh(g, $"Relic_{g.TextureName}");
            var go = new GameObject($"Brush_{g.TextureName}");
            go.IsStatic = true;
            var mr = go.AddComponent<MeshRenderer>();
            mr.Mesh = new AssetRef<Mesh>(mesh);
            if (options.BuildMaterials)
            {
                var mat = RelicMaterialLibrary.Get(g.TextureName, textures);
                mr.Materials.Add(new AssetRef<Material>(mat));
                if (RelicMaterialLibrary.IsTextured(g.TextureName, textures)) report.TexturedMaterials++;
                else report.FallbackMaterials++;
            }
            if (options.BuildCollision)
            {
                // RelicBrushCollider: same collision, no wireframe spam in Scene view.
                var mc = go.AddComponent<RelicBrushCollider>();
                mc.Mesh = new AssetRef<Mesh>(mesh);
            }
            go.Transform.LocalPosition = Float3.Zero;
            var fn = go.AddComponent<RelicFaceNormal>();
            fn.Mesh = new AssetRef<Mesh>(mesh);
            foreach (var r in g.Ranges)
                fn.Ranges.Add(new RelicFaceNormal.FaceInfo
                {
                    FaceIndex = r.FaceIndex,
                    Texture = r.Texture,
                    StartVertex = r.StartVertex,
                    VertexCount = r.VertexCount,
                    BaseNormal = r.Normal
                });
            scene.Add(go);
            try { go.SetParent(worldRoot); } catch { }
            report.WorldMeshes++;
            report.WorldTriangles += g.Indices.Count / 3;
        }

        // ---- entities ----
        if (options.SpawnEntities)
        {
            foreach (var e in map.Entities)
            {
                try
                {
                    if (SpawnEntity(e, scene, options, report, textures))
                        report.EntitiesSpawned++;
                }
                catch (Exception ex)
                {
                    report.Warnings++;
                    report.Messages.Add($"Entity '{e.ClassName}': {ex.Message}");
                }
            }
        }

        if (!report.HasPlayerSpawn)
        {
            report.Warnings++;
            report.Messages.Add("No player_start found — add one in TrenchBroom to walk around on Play.");
        }

        TagGeneratedObjects(scene, before, sourcePath);

        return report;
    }

    /// <summary>Tag everything <see cref="Build"/> just added so hot-reload can clear it later.</summary>
    private static void TagGeneratedObjects(Scene scene, HashSet<GameObject> before, string? sourcePath)
    {
        foreach (var go in scene.AllObjects.ToArray())
        {
            if (go.IsNotValid() || before.Contains(go)) continue;
            try
            {
                if (go.GetComponent<RelicMapGenerated>().IsNotValid())
                {
                    var marker = go.AddComponent<RelicMapGenerated>();
                    if (marker.IsValid()) marker.SourceMapPath = sourcePath ?? string.Empty;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Remove all map-generated objects from the scene (hot-reload / clean reimport).
    /// Only touches objects tagged by <see cref="Build"/> plus the legacy "Relic Worldspawn"
    /// root from before tagging existed. User-placed objects are never removed.
    /// </summary>
    /// <param name="onlyMapPath">When set, only clear objects built from this .map path.</param>
    /// <returns>Number of root objects removed.</returns>
    public static int ClearGeneratedObjects(Scene scene, string? onlyMapPath = null)
    {
        var tagged = new HashSet<GameObject>();
        foreach (var marker in scene.FindObjectsOfType<RelicMapGenerated>())
        {
            if (marker.IsNotValid()) continue;
            var go = marker.GameObject;
            if (go.IsNotValid()) continue;
            if (!string.IsNullOrEmpty(onlyMapPath) &&
                !string.IsNullOrEmpty(marker.SourceMapPath) &&
                !string.Equals(marker.SourceMapPath, onlyMapPath, StringComparison.OrdinalIgnoreCase))
                continue;
            tagged.Add(go);
        }

        // Legacy scenes imported before tagging: the world root was never marked.
        if (tagged.Count == 0)
        {
            foreach (var go in scene.AllObjects.ToArray())
            {
                if (go.IsNotValid()) continue;
                if (go.Parent.IsValid()) continue;
                if (string.Equals(go.Name, "Relic Worldspawn", StringComparison.Ordinal))
                    tagged.Add(go);
            }
        }

        if (tagged.Count == 0) return 0;

        // Remove topmost tagged roots only — children go with their parent via Remove().
        var roots = tagged.Where(go =>
        {
            var p = go.Parent;
            while (p.IsValid())
            {
                if (tagged.Contains(p)) return false;
                p = p.Parent;
            }
            return true;
        }).ToList();

        int removed = 0;
        foreach (var root in roots)
        {
            try
            {
                if (root.IsNotValid() || root.Scene != scene) continue;
                scene.Remove(root);
                root.Destroy();
                removed++;
            }
            catch { }
        }
        return removed;
    }

    /// <summary>
    /// Full reimport: parse the .map, clear previously generated objects, rebuild.
    /// This is what the TrenchBroom auto-reload calls on every save.
    /// A scene holds one map: objects generated from a different .map path are kept.
    /// </summary>
    public static RelicBuildReport Reimport(string mapPath, Scene scene, RelicBuildOptions? options = null)
    {
        var map = Map.RelicMapParser.Load(mapPath);
        ClearGeneratedObjects(scene, mapPath);
        return Build(map, scene, options);
    }

    // Returns true if an entity GameObject was created.
    private static bool SpawnEntity(RelicMapEntity e, Scene scene, RelicBuildOptions options, RelicBuildReport report, RelicTextureSet? textures = null)
    {
        string cls = e.ClassName.ToLowerInvariant();
        Float3 pos = ToProwl(e.OriginQuake, options.UnitScale);
        float yawQ = e.GetFloat("angle", 0f);

        GameObject New(string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(e.GetString("targetname")) ? name : e.GetString("targetname"));
            if (e.HasOrigin) go.Transform.Position = pos;
            scene.Add(go);
            return go;
        }

        switch (cls)
        {
            // ---------------- player ----------------
            case "player_start":
            case "info_player_start":
            {
                var go = New("PlayerStart");
                var spawn = go.AddComponent<RelicPlayerSpawn>();
                spawn.YawDegrees = QuakeYawToProwl(yawQ);
                spawn.SpawnName = e.GetString("targetname");
                report.HasPlayerSpawn = true;
                if (options.SpawnPlayerRig && scene.FindObjectsOfType<RelicPlayerController>().Length == 0)
                    BuildPlayerRig(scene, pos, spawn.YawDegrees);
                return true;
            }

            // ---------------- enemies ----------------
            case "enemy_soldier":
            case "enemy_dog":
            case "enemy_demon":
            {
                var go = New(cls);
                var enemy = go.AddComponent<RelicEnemy>();
                enemy.Kind = cls.Contains("dog") ? RelicEnemyKind.Dog
                    : cls.Contains("demon") ? RelicEnemyKind.Demon : RelicEnemyKind.Soldier;
                enemy.MaxHealth = enemy.Health = e.GetFloat("health", enemy.Kind switch
                {
                    RelicEnemyKind.Dog => 50f,
                    RelicEnemyKind.Demon => 300f,
                    _ => 100f
                });
                enemy.TargetName = e.GetString("targetname");
                enemy.PatrolPath = e.GetString("target");
                go.Transform.EulerAngles = new Float3(0, QuakeYawToProwl(yawQ), 0);
                return true;
            }
            case "info_enemy_spawn":
            {
                var go = New("EnemySpawn");
                var s = go.AddComponent<RelicEnemySpawn>();
                s.Kind = e.GetString("enemy", "enemy_soldier").Contains("dog") ? RelicEnemyKind.Dog
                    : e.GetString("enemy").Contains("demon") ? RelicEnemyKind.Demon : RelicEnemyKind.Soldier;
                s.Count = Math.Max(1, e.GetInt("count", 1));
                s.SpawnName = e.GetString("targetname");
                return true;
            }
            case "path_corner":
            case "path_patrol":
            {
                var go = New("PathCorner");
                var p = go.AddComponent<RelicPathCorner>();
                p.PathName = e.GetString("targetname");
                p.NextTarget = e.GetString("target");
                p.WaitSeconds = e.GetFloat("wait", 0f);
                return true;
            }

            // ---------------- items ----------------
            case "weapon_pickup":
            case "weapon_spawn":
            case "ammo_pickup":
            case "health_pickup":
            case "armor_pickup":
            case "keycard_pickup":
            {
                var go = New(cls);
                var pk = go.AddComponent<RelicPickup>();
                pk.Kind = cls.StartsWith("weapon") ? RelicPickup.PickupKind.Weapon
                    : cls.StartsWith("ammo") ? RelicPickup.PickupKind.Ammo
                    : cls.StartsWith("armor") ? RelicPickup.PickupKind.Armor
                    : cls.StartsWith("keycard") ? RelicPickup.PickupKind.Keycard
                    : RelicPickup.PickupKind.Health;
                pk.ItemName = e.GetString("weapon", e.GetString("ammo_type", e.GetString("key", "Rifle")));
                pk.Amount = e.Properties.ContainsKey("amount") ? e.GetFloat("amount", 25f)
                    : pk.Kind == RelicPickup.PickupKind.Health ? e.GetFloat("heal", 25f) : 25f;
                pk.RespawnSeconds = e.GetFloat("respawn", 10f);
                AddBoxTrigger(go, new Float3(1f, 1f, 1f));
                return true;
            }

            // ---------------- lights ----------------
            case "light":
            {
                var go = New("Light");
                var l = go.AddComponent<PointLight>();
                ApplyLightCommon(e, l, options);
                l.Range = e.Properties.ContainsKey("radius") ? e.GetFloat("radius") * options.UnitScale
                    : e.GetFloat("light", 300f) * options.UnitScale * 0.05f;
                return true;
            }
            case "light_spot":
            {
                var go = New("SpotLight");
                var l = go.AddComponent<SpotLight>();
                ApplyLightCommon(e, l, options);
                l.Range = e.GetFloat("radius", 1024f) * options.UnitScale;
                AimFromAngles(go, e.GetString("angles", $"-45 {yawQ} 0"));
                return true;
            }
            case "light_sun":
            {
                var go = New("Sun");
                var l = go.AddComponent<DirectionalLight>();
                ApplyLightCommon(e, l, options);
                AimFromAngles(go, e.GetString("angles", "-45 45 0"));
                return true;
            }

            // ---------------- environment ----------------
            case "env_fog":
            case "env_ambient":
            {
                var go = New("Fog");
                var f = go.AddComponent<RelicFog>();
                f.Mode = e.GetString("mode", "exp2").ToLowerInvariant() switch
                {
                    "off" or "none" => RelicFog.RelicFogMode.Off,
                    "linear" => RelicFog.RelicFogMode.Linear,
                    "exp" or "exponential" => RelicFog.RelicFogMode.Exponential,
                    _ => RelicFog.RelicFogMode.ExponentialSquared
                };
                f.FogColor = ParseColor01(e.GetString("color", "120 140 180"));
                f.Density = e.GetFloat("density", 0.02f);
                f.FogStart = e.GetFloat("start", 8f);
                f.FogEnd = e.GetFloat("end", 90f);
                f.Volumetric = e.GetBool("volumetric", true);
                f.VolumetricDensity = e.GetFloat("volumetric_density", f.Density);
                f.VolumetricScattering = e.GetFloat("scattering", 0.5f);
                f.VolumetricMaxDistance = e.GetFloat("maxdistance", 100f);
                f.VolumetricAmbientIntensity = e.GetFloat("ambient", 0.3f);
                // No FogVolume: RelicFog.Apply drives Scene.Fog + the volumetric
                // effect's global density directly, so nothing double-adds.
                return true;
            }
            case "env_postprocess":
            {
                var go = New("PostProcess");
                var pp = go.AddComponent<RelicPostProcess>();
                pp.BloomEnabled = e.GetBool("bloom", true);
                pp.Exposure = e.GetFloat("exposure", 1.1f);
                pp.BloomIntensity = e.GetFloat("bloom_intensity", 1f);
                pp.BloomThreshold = e.GetFloat("bloom_threshold", 1f);
                pp.ToneMap = e.GetString("tonemap", "ACES");
                return true;
            }
            case "env_rain":
            case "env_snow":
            case "env_wind":
            case "env_lightning":
            {
                var go = New("Weather");
                var w = go.AddComponent<RelicWeather>();
                w.Kind = cls.Contains("snow") ? RelicWeather.WeatherKind.Snow
                    : cls.Contains("wind") ? RelicWeather.WeatherKind.Wind
                    : cls.Contains("lightning") ? RelicWeather.WeatherKind.Lightning
                    : RelicWeather.WeatherKind.Rain;
                w.Density = e.GetFloat("density", 0.5f);
                w.Intensity = e.GetFloat("intensity", 1f);
                if (e.Properties.TryGetValue("direction", out var dir))
                    w.Direction = RelicBrushBuilder.QuakeNormalToProwl(RelicMapParser.ParseVector3(dir));
                return true;
            }
            case "env_skybox":
            {
                var go = New("Skybox");
                go.AddComponent<RelicSkybox>().SkyboxName = e.GetString("skybox");
                return true;
            }

            // ---------------- particles ----------------
            case "env_particles":
            case "fx_smoke":
            case "fx_fire":
            case "fx_sparks":
            case "fx_blood":
            case "fx_explosion":
            case "fx_muzzleflash":
            {
                var go = New("Particles");
                var p = go.AddComponent<RelicParticle>();
                p.Effect = cls.StartsWith("fx_") ? cls[3..] : e.GetString("effect", "smoke");
                p.SpawnRate = e.GetFloat("spawnrate", 10f);
                p.Lifetime = e.GetFloat("lifetime", 1f);
                p.Size = e.GetFloat("size", 4f);
                if (e.Properties.TryGetValue("velocity", out var vel))
                    p.Velocity = RelicBrushBuilder.QuakeNormalToProwl(RelicMapParser.ParseVector3(vel));
                if (e.Properties.TryGetValue("color", out var col)) p.Tint = ParseColor01(col);
                return true;
            }

            // ---------------- audio ----------------
            case "ambient_sound":
            case "sound_emitter":
            case "sound_zone":
            {
                var go = New("AmbientSound");
                var a = go.AddComponent<RelicAmbientSound>();
                a.Sound = e.GetString("sound");
                a.Volume = e.GetFloat("volume", 1f);
                a.Radius = e.GetFloat("radius", 512f) * options.UnitScale;
                a.Loop = e.GetBool("loop", true);
                go.AddComponent<AudioSource>();
                return true;
            }
            case "music_player":
            {
                var go = New("MusicPlayer");
                var m = go.AddComponent<RelicMusicPlayerComp>();
                m.Track = e.GetString("track");
                m.Volume = e.GetFloat("volume", 0.8f);
                m.Loop = e.GetBool("loop", true);
                return true;
            }
            case "music_trigger":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Music, pos);
            case "trigger_music":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Music, pos);
            case "trigger_sound":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Sound, pos);
            case "trigger_once":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Once, pos);
            case "trigger_multiple":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Multiple, pos);
            case "trigger_hurt":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Hurt, pos);
            case "trigger_heal":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Heal, pos);
            case "trigger_push":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Push, pos);
            case "trigger_teleport":
                return SpawnTriggerAs(e, scene, options, RelicTriggerKind.Teleport, pos);

            // ---------------- logic ----------------
            case "logic_timer":
            {
                var go = New("LogicTimer");
                var t = go.AddComponent<RelicLogicTimer>();
                t.TimerName = e.GetString("targetname");
                t.Target = e.GetString("target");
                t.IntervalSeconds = e.GetFloat("interval", 5f);
                t.StartOn = e.GetBool("start_on", true);
                return true;
            }
            case "logic_random":
            {
                var go = New("LogicRandom");
                var r = go.AddComponent<RelicLogicRandom>();
                r.Options = e.GetInt("options", 2);
                r.Seed = e.GetInt("seed", 12345);
                return true;
            }
            case "logic_relay":
            {
                var go = New("LogicRelay");
                var r = go.AddComponent<RelicLogicRelay>();
                r.RelayName = e.GetString("targetname");
                r.Target = e.GetString("target");
                return true;
            }
            case "logic_counter":
            {
                var go = New("LogicCounter");
                var c = go.AddComponent<RelicLogicCounter>();
                c.CounterName = e.GetString("targetname");
                c.Target = e.GetString("target");
                c.RequiredCount = e.GetInt("count", 3);
                return true;
            }
            case "logic_compare":
            {
                var go = New("LogicCompare");
                var c = go.AddComponent<RelicLogicCompare>();
                c.A = e.GetFloat("a", 0f);
                c.B = e.GetFloat("b", 0f);
                c.Operator = e.GetString("op", "==");
                c.Target = e.GetString("target");
                return true;
            }

            // ---------------- movers ----------------
            case "func_door":
            case "func_door_rotating":
            case "func_button":
            case "func_elevator":
            case "func_platform":
            case "func_train":
            {
                var go = New(cls);
                var m = go.AddComponent<RelicMover>();
                m.Kind = cls.Contains("rotating") ? RelicMoverKind.DoorRotating
                    : cls.Contains("button") ? RelicMoverKind.Button
                    : cls.Contains("elevator") ? RelicMoverKind.Elevator
                    : cls.Contains("platform") ? RelicMoverKind.Platform
                    : cls.Contains("train") ? RelicMoverKind.Train : RelicMoverKind.Door;
                m.MoverName = e.GetString("targetname");
                m.Target = e.GetString("target");
                m.Speed = e.GetFloat("speed", 100f) * options.UnitScale;
                m.WaitSeconds = e.GetFloat("wait", 3f);
                m.Locked = e.GetBool("locked", false);
                // Build mover geometry from its own brushes.
                Float2 MTexSize(string name) => textures != null ? textures.GetSize(name) : options.FallbackTextureSize;
                var mgeo = RelicBrushBuilder.BuildBrushes(e.Brushes, options.UnitScale, MTexSize);
                foreach (var issue in RelicGeometryValidator.ValidateMesh(mgeo))
                {
                    report.Warnings++;
                    report.GeometryIssues.Add($"{cls} '{m.MoverName}' {issue}");
                }
                // TrenchBroom normal overrides: _normal (all faces) / _normalN (face N).
                // Applied after validation — a deliberate override is not a defect.
                RelicFaceNormal.ApplyMapOverrides(mgeo, RelicFaceNormal.ParseMapKeys(e.Properties));
                var mgroups = RelicBrushBuilder.GroupByTexture(mgeo);
                foreach (var g in mgroups)
                {
                    var mesh = RelicBrushBuilder.BuildMesh(g, $"Relic_{cls}_{g.TextureName}");
                    var part = new GameObject($"Part_{g.TextureName}");
                    var mr = part.AddComponent<MeshRenderer>();
                    mr.Mesh = new AssetRef<Mesh>(mesh);
                    if (options.BuildMaterials)
                    {
                        var mmat = RelicMaterialLibrary.Get(g.TextureName, textures);
                        mr.Materials.Add(new AssetRef<Material>(mmat));
                        if (RelicMaterialLibrary.IsTextured(g.TextureName, textures)) report.TexturedMaterials++;
                        else report.FallbackMaterials++;
                    }
                    if (options.BuildCollision)
                    {
                        var mc = part.AddComponent<RelicBrushCollider>();
                        mc.Mesh = new AssetRef<Mesh>(mesh);
                    }
                    var mfn = part.AddComponent<RelicFaceNormal>();
                    mfn.Mesh = new AssetRef<Mesh>(mesh);
                    foreach (var r in g.Ranges)
                        mfn.Ranges.Add(new RelicFaceNormal.FaceInfo
                        {
                            FaceIndex = r.FaceIndex,
                            Texture = r.Texture,
                            StartVertex = r.StartVertex,
                            VertexCount = r.VertexCount,
                            BaseNormal = r.Normal
                        });
                    scene.Add(part);
                    try { part.SetParent(go); } catch { }
                    report.WorldMeshes++;
                    report.WorldTriangles += g.Indices.Count / 3;
                }
                if (Float3.LengthSquared(mgeo.Bounds.Size) > 0.0001f)
                    go.Transform.Position = mgeo.Bounds.Center;
                return true;
            }

            // ---------------- objectives ----------------
            case "objective":
            {
                var go = New("Objective");
                var o = go.AddComponent<RelicObjective>();
                o.ObjectiveId = e.GetString("objective_id", e.GetString("targetname", "obj1"));
                o.Description = e.GetString("description");
                o.Required = Math.Max(1, e.GetInt("required", 1));
                return true;
            }
            case "checkpoint":
            {
                var go = New("Checkpoint");
                var c = go.AddComponent<RelicCheckpoint>();
                c.CheckpointId = e.GetString("checkpoint_id", e.GetString("targetname", "cp1"));
                AddBoxTrigger(go, new Float3(3f, 3f, 3f));
                return true;
            }
            case "mission_start":
            {
                var go = New("MissionStart");
                var g2 = go.AddComponent<RelicMissionGate>();
                g2.IsEnd = false;
                g2.Mission = e.GetString("mission", "mission1");
                return true;
            }
            case "mission_end":
            {
                var go = New("MissionEnd");
                var g2 = go.AddComponent<RelicMissionGate>();
                g2.IsEnd = true;
                g2.Mission = e.GetString("mission", "mission1");
                AddBoxTrigger(go, new Float3(3f, 3f, 3f));
                return true;
            }

            default:
            {
                // Future-proof: unknown classnames still spawn a marker so no map data is lost.
                var go = New(e.ClassName);
                report.Messages.Add($"Unknown entity '{e.ClassName}' spawned as marker.");
                return true;
            }
        }
    }

    private static bool SpawnTriggerAs(RelicMapEntity e, Scene scene, RelicBuildOptions options, RelicTriggerKind kind, Float3 pos)
    {
        var go = new GameObject(string.IsNullOrEmpty(e.GetString("targetname")) ? kind.ToString() : e.GetString("targetname"));
        if (e.HasOrigin) go.Transform.Position = pos;
        scene.Add(go);
        var t = go.AddComponent<RelicTrigger>();
        t.Kind = kind;
        t.Target = e.GetString("target");
        t.TriggerName = e.GetString("targetname");
        t.Damage = e.GetFloat("damage", 10f);
        t.HealAmount = e.GetFloat("amount", e.GetFloat("heal", 25f));
        t.Interval = e.GetFloat("interval", 1f);
        t.PushForce = e.GetFloat("force", 300f);
        t.Sound = e.GetString("sound");
        t.MusicTrack = e.GetString("track");
        if (e.Properties.TryGetValue("direction", out var dir))
            t.PushDirection = RelicBrushBuilder.QuakeNormalToProwl(RelicMapParser.ParseVector3(dir));

        // Size from brushes when present, else a 3m cube at origin.
        Float3 size = new(3f, 3f, 3f);
        if (e.Brushes.Count > 0)
        {
            var b = RelicBrushBuilder.ComputeBrushBounds(e.Brushes, options.UnitScale);
            var s = b.Size;
            if (Float3.LengthSquared(s) > 0.001f) size = new Float3(
                Math.Max(0.5f, s.X), Math.Max(0.5f, s.Y), Math.Max(0.5f, s.Z));
            go.Transform.Position = b.Center;
        }
        AddBoxTrigger(go, size);
        return true;
    }

    private static void AddBoxTrigger(GameObject go, Float3 size)
    {
        var v = go.AddComponent<TriggerVolume>();
        v.Shape = TriggerShape.Box;
        v.Size = size;
    }

    private static void ApplyLightCommon(RelicMapEntity e, Light l, RelicBuildOptions options)
    {
        if (e.Properties.TryGetValue("color", out var col)) l.Color = ParseColor01(col);
        float raw = e.GetFloat("intensity", 800f);
        l.Intensity = Math.Max(0.1f, raw / 100f);
        l.CastShadows = e.GetBool("castShadows", false);
    }

    private static void AimFromAngles(GameObject go, string angles)
    {
        // angles: "pitch yaw roll" (Quake) -> Prowl euler.
        var parts = angles.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        float pitch = 0, yaw = 0;
        if (parts.Length >= 1) float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out pitch);
        if (parts.Length >= 2) float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out yaw);
        go.Transform.EulerAngles = new Float3(pitch, -yaw, 0);
    }

    private static Color ParseColor01(string s)
    {
        var parts = s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
        {
            // Accept 0-255 or 0-1.
            if (r > 1f || g > 1f || b > 1f) { r /= 255f; g /= 255f; b /= 255f; }
            return new Color(
                Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(b, 0f, 1f), 1f);
        }
        return Color.White;
    }

    private static void BuildPlayerRig(Scene scene, Float3 spawnPos, float yawDeg)
    {
        var player = new GameObject("Player");
        player.Transform.Position = spawnPos + new Float3(0, 0.9f, 0); // eye above floor
        player.Transform.EulerAngles = new Float3(0, yawDeg, 0);
        player.AddComponent<CharacterController>();
        player.AddComponent<RelicPlayerController>();
        player.AddComponent<RelicInventory>();
        scene.Add(player);

        var camGo = new GameObject("PlayerCamera");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        scene.Add(camGo);
        try { camGo.SetParent(player); } catch { }
        camGo.Transform.LocalPosition = new Float3(0, 0.7f, 0);
    }
}
