namespace Relic.Gameplay;

using Relic.Content;
using Relic.Framework;
using Relic.Gameplay.Components;
using Relic.Gameplay;

public static partial class MapSpawner
{
    public static void SpawnMapEntities(EntityManager manager, MapData map, Dictionary<string, string>? globalProperties = null)
    {
        foreach (var me in map.Entities)
        {
            Spawn(manager, me);
        }
    }

    public static Entity Spawn(EntityManager manager, MapEntity mapEntity)
    {
        string className = mapEntity.ClassName.ToLowerInvariant();
        string tag = mapEntity.Properties.TryGetValue("targetname", out var tn) ? tn : string.Empty;
        var entity = manager.CreateEntity(mapEntity.ClassName, className, tag);
        entity.ClassName = className;
        entity.Tag = tag;
        // Store all map properties
        foreach (var kv in mapEntity.Properties)
            entity.Properties[kv.Key] = kv.Value;
        entity.Properties["classname"] = mapEntity.ClassName;
        // Position
        entity.Transform.Position = mapEntity.Position;
        // Angle -> yaw around Z
        if (mapEntity.Properties.TryGetValue("angle", out var angleStr) && float.TryParse(angleStr, out var ang))
        {
            var rot = entity.Transform.Rotation;
            rot.Y = ang * MathF.PI / 180f;
            entity.Transform.Rotation = rot;
        }

        // Automatic component mapping from map properties - no hardcoded entity creation required beyond this generic handling
        // Player
        if (className == "player_start" || className == "info_player_start")
        {
            entity.AddComponent(new PlayerComponent());
            entity.AddComponent(new HealthComponent { MaxHealth = 100, Health = 100 });
            entity.AddComponent(new WeaponComponent());
            entity.AddComponent(new InventoryComponent());
        }
        // Enemy - soldier, dog, demon (all use EnemyComponent + EnemyAIComponent + Health + AnimatedModel)
        if (className == "enemy_soldier" || className == "enemy_dog" || className == "enemy_demon")
        {
            var hc = entity.GetComponent<HealthComponent>() ?? entity.AddComponent(new HealthComponent());
            // Default health per type
            float defaultHealth = className == "enemy_dog" ? 50f : className == "enemy_demon" ? 300f : 100f;
            if (mapEntity.Properties.TryGetValue("health", out var hs) && float.TryParse(hs, out var h)) { hc.MaxHealth = h; hc.Health = h; }
            else { hc.MaxHealth = defaultHealth; hc.Health = defaultHealth; }
            if (entity.GetComponent<EnemyComponent>() == null) entity.AddComponent(new EnemyComponent());
            var ai = entity.GetComponent<Components.EnemyAIComponent>() ?? entity.AddComponent(new Components.EnemyAIComponent());
            ai.ConfigureFor(className);
            // Default model if not specified
            if (!mapEntity.Properties.ContainsKey("model"))
            {
                string defaultModel = className == "enemy_dog" ? "dog.mdl" : className == "enemy_demon" ? "demon.mdl" : "soldier.mdl";
                mapEntity.Properties["model"] = defaultModel;
                entity.Properties["model"] = defaultModel;
            }
        }
        // Weapon pickup
        if (className == "weapon_pickup")
        {
            var wp = entity.GetComponent<WeaponPickupComponent>() ?? entity.AddComponent(new WeaponPickupComponent());
            if (mapEntity.Properties.TryGetValue("weapon", out var wname)) wp.WeaponName = wname;
            else wp.WeaponName = "Rifle";
            if (entity.GetComponent<WeaponComponent>() == null) entity.AddComponent(new WeaponComponent());
        }
        // Health pickup
        if (className == "health_pickup")
        {
            var hp = entity.GetComponent<HealthPickupComponent>() ?? entity.AddComponent(new HealthPickupComponent());
            if (mapEntity.Properties.TryGetValue("heal", out var hs2) && float.TryParse(hs2, out var hv)) hp.HealAmount = hv;
            else if (mapEntity.Properties.TryGetValue("health", out var hs3) && float.TryParse(hs3, out var hv2)) hp.HealAmount = hv2;
        }
        // Generic model support for any entity with "model" key (prop_static, etc.)
        if (mapEntity.Properties.TryGetValue("model", out var modelPath))
        {
            var ext = Path.GetExtension(modelPath).ToLowerInvariant();
            if (ext == ".mdl")
            {
                var amc = entity.GetComponent<AnimatedModelComponent>() ?? entity.AddComponent(new AnimatedModelComponent());
                amc.ModelPath = modelPath;
                try
                {
                    var db = new AssetDatabase();
                    var mdl = db.LoadMDL(modelPath);
                    amc.SetModel(mdl, modelPath);
                }
                catch { }
            }
            else
            {
                var mc = entity.GetComponent<ModelComponent>() ?? entity.AddComponent(new ModelComponent());
                mc.ModelPath = modelPath;
                try
                {
                    var db = new AssetDatabase();
                    var data = db.LoadModel(modelPath);
                    mc.ModelData = data;
                    mc.IsLoaded = true;
                }
                catch { mc.IsLoaded = false; }
            }
        }
        // Light entities
        if (className == "light" || className == "light_spot" || className == "light_sun")
        {
            var light = entity.GetComponent<LightComponent>() ?? entity.AddComponent(new LightComponent());
            if (className == "light") light.Type = LightType.Point;
            else if (className == "light_spot") light.Type = LightType.Spot;
            else if (className == "light_sun") light.Type = LightType.Directional;

            if (mapEntity.Properties.TryGetValue("color", out var colStr))
            {
                var parts = colStr.Split(new[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
                    light.Color = new Vector3(r/255f, g/255f, b/255f);
            }
            if (mapEntity.Properties.TryGetValue("intensity", out var intStr) && float.TryParse(intStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var intensity))
                light.Intensity = intensity;
            if (mapEntity.Properties.TryGetValue("radius", out var radStr) && float.TryParse(radStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius))
                light.Range = radius;
            else if (mapEntity.Properties.TryGetValue("light", out var lightStr) && float.TryParse(lightStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lightVal))
                light.Range = lightVal;
            if (mapEntity.Properties.TryGetValue("falloff", out var fallStr) && float.TryParse(fallStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fall)) light.Falloff = fall;
            if (mapEntity.Properties.TryGetValue("castShadows", out var csStr)) light.CastShadows = csStr == "1" || csStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (className == "light_spot" || className == "light_sun")
            {
                // angles: pitch yaw roll? For spot, use angles property "-45 0 0" etc
                if (mapEntity.Properties.TryGetValue("angles", out var angStr) || mapEntity.Properties.TryGetValue("angle", out angStr))
                {
                    var parts = angStr.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pitch) &&
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var yaw))
                    {
                        float yawRad = yaw * MathF.PI / 180f;
                        float pitchRad = pitch * MathF.PI / 180f;
                        light.Direction = new Vector3(MathF.Cos(pitchRad)*MathF.Cos(yawRad), MathF.Cos(pitchRad)*MathF.Sin(yawRad), MathF.Sin(pitchRad));
                    }
                }
            }
            if (light.Type == LightType.Directional && light.Direction.LengthSquared() < 0.001f)
            {
                // Default sun direction from angles or default
                light.Direction = new Vector3(-0.5f, 0.5f, -0.7f);
                light.Direction.Normalize();
            }
        }
        // Environment
        if (className == "env_fog")
        {
            var fog = entity.GetComponent<FogComponent>() ?? entity.AddComponent(new FogComponent());
            if (mapEntity.Properties.TryGetValue("color", out var colStr))
            {
                var parts = colStr.Split(new[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
                    fog.Color = new Vector3(r/255f, g/255f, b/255f);
            }
            if (mapEntity.Properties.TryGetValue("density", out var densStr) && float.TryParse(densStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dens)) fog.Density = dens;
        }
        if (className == "env_postprocess")
        {
            var pp = entity.GetComponent<PostProcessComponent>() ?? entity.AddComponent(new PostProcessComponent());
            if (mapEntity.Properties.TryGetValue("bloom", out var bStr)) pp.BloomEnabled = bStr == "1" || bStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (mapEntity.Properties.TryGetValue("exposure", out var expStr) && float.TryParse(expStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var exp)) pp.Exposure = exp;
            if (mapEntity.Properties.TryGetValue("tonemap", out var tmStr)) pp.ToneMap = tmStr;
            if (mapEntity.Properties.TryGetValue("bloom_intensity", out var biStr) && float.TryParse(biStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bi)) pp.BloomIntensity = bi;
            if (mapEntity.Properties.TryGetValue("bloom_threshold", out var btStr) && float.TryParse(btStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bt)) pp.BloomThreshold = bt;
        }
        // Trigger volumes store target/targetname for future use
        // Phase 10 world systems (audio, triggers, doors, logic, paths,
        // particles, weather, items, missions) attach from .map properties.
        SpawnWorldSystems(entity, mapEntity);
        // Any additional classname is automatically instantiated as generic entity above - no code change required
        // Generic health/weapon properties still stored in dictionary for inspection
        return entity;
    }
}
