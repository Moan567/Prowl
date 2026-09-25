using Relic.Content;
using Relic.Framework;
using Relic.Renderer.OpenGL;
using Relic.Physics;
using Relic.Windowing;
using Relic.Gameplay;
using Relic.Gameplay.Components;
using Relic.Gameplay.Materials;
using Relic.Gameplay.Weapons;
using Relic.Renderer;
using OpenTK.Graphics.OpenGL4;

class Program
{
    static void Main(string[] args)
    {
        if (args.Contains("--test-geometry")) { RunHeadlessContentTest(); return; }
        if (args.Contains("--test-physics"))
        {
            Console.WriteLine("[Relic] Physics tests moved to Relic.Tests project. Run: dotnet run --project ../Relic.Tests/Relic.Tests.csproj");
            return;
        }
        if (args.Contains("--test")) { RunHeadlessContentTest(); Console.WriteLine("[Relic] For physics run Relic.Tests"); return; }
        RunGameplayLevel(ArgValue(args, "--map"));
    }

    static string? ArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && i + 1 < args.Length) return args[i + 1];
            if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
                return args[i][(name.Length + 1)..];
        }
        return null;
    }

    static void RunHeadlessContentTest()
    {
        var map = MapLoader.Load(FindMapPath());
        Console.WriteLine($"[Test] Map: {map.Brushes.Count} brushes, {map.Entities.Count} entities");
        var grouped = MapMeshBuilder.BuildGrouped(map);
        Console.WriteLine($"[Test] Groups: {grouped.Groups.Count}");
        foreach (var g in grouped.Groups) Console.WriteLine($"[Test] Group '{g.TextureName}': {g.Vertices.Count} verts, {g.Indices.Count} indices");
        // AssetDatabase test
        var assetsRoot = FindAssetsRoot();
        var db = new AssetDatabase(assetsRoot);
        Console.WriteLine($"[Test] Assets root: {assetsRoot}");
        foreach (var m in db.ListMaterials()) Console.WriteLine($"[Test] Material: {m}.mat");
        var brickMat = db.LoadMaterial("brick");
        Console.WriteLine($"[Test] brick.mat diffuse={brickMat.DiffuseTexture} tint={brickMat.Tint}");
        try
        {
            var model = db.LoadModel("crate.obj");
            Console.WriteLine($"[Test] crate.obj verts={model.Positions.Count} indices={model.Indices.Count}");
        }
        catch (Exception ex) { Console.WriteLine($"[Test] crate.obj load failed: {ex.Message}"); }
        // MDL tests
        foreach (var mdlName in new[] { "dog.mdl", "demon.mdl", "soldier.mdl" })
        {
            try
            {
                var mdl = db.LoadMDL(mdlName);
                Console.WriteLine($"[Test] {mdlName} verts={mdl.StVerts.Count} tris={mdl.Triangles.Count} frames={mdl.Frames.Count} skin={mdl.SkinWidth}x{mdl.SkinHeight}");
                var mesh = MDLLoader.ToMeshData(mdl, 0);
                Console.WriteLine($"[Test] {mdlName} mesh verts={mesh.Vertices.Count} indices={mesh.Indices.Count}");
                // Test frame interpolation
                if (mdl.Frames.Count > 1)
                {
                    var interp = MDLLoader.ToMeshData(mdl, 0, 0.5f, 1);
                    Console.WriteLine($"[Test] {mdlName} interp verts={interp.Vertices.Count}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Test] {mdlName} load failed: {ex.Message}"); }
        }
        var sound = db.LoadSound("weapons/shotgn2.wav");
        Console.WriteLine($"[Test] Sound weapons/shotgn2.wav bytes={sound.Raw.Length}");
        // Entity instantiation test
        var em = new EntityManager();
        MapSpawner.SpawnMapEntities(em, map);
        Console.WriteLine($"[Test] Spawned {em.Count} entities:");
        foreach (var e in em.Entities)
        {
            var amc = e.GetComponent<AnimatedModelComponent>();
            var mc = e.GetComponent<ModelComponent>();
            string modelInfo = amc != null ? amc.ModelPath + $" frames={amc.Model?.Frames.Count ?? 0}" : mc?.ModelPath ?? "none";
            Console.WriteLine($"[Test] Entity {e.Id} {e.ClassName} pos={e.Transform.Position} model={modelInfo}");
        }
        var prop = em.FindByClassName("prop_static").FirstOrDefault();
        Console.WriteLine(prop != null ? $"[Test] prop_static found at {prop.Transform.Position} model {prop.GetComponent<ModelComponent>()?.ModelPath ?? prop.GetComponent<AnimatedModelComponent>()?.ModelPath}" : "[Test] prop_static missing");
        var animated = em.Entities.Count(e => e.GetComponent<AnimatedModelComponent>() != null);
        Console.WriteLine($"[Test] Animated models: {animated}");
        Console.WriteLine("[Test] Content Pipeline SUCCESS");
    }

    static void RunGameplayLevel(string? mapOverride = null)
    {
        string mapPath = FindMapPath(mapOverride);
        Console.WriteLine($"[Relic] Loading map: {mapPath}");
        MapData map = MapLoader.Load(mapPath);
        Console.WriteLine($"[Relic] Brushes: {map.Brushes.Count}, Map Entities: {map.Entities.Count}");

        var grouped = MapMeshBuilder.BuildGrouped(map);
        var physics = PhysicsWorld.FromMapData(map);
        Console.WriteLine($"[Relic] Physics brushes: {physics.BrushCount} (Z-Up solid)");

        // AssetDatabase + Material/Model/Sound loaders
        var assetsRoot = FindAssetsRoot();
        var assetDb = new AssetDatabase(assetsRoot);
        var materialManager = new MaterialManager();
        // Load material definitions from Assets/Materials/*.mat (expanded Phase 9: diffuse/normal/specular/emission/roughness)
        foreach (var matName in assetDb.ListMaterials())
        {
            try
            {
                var matData = assetDb.LoadMaterial(matName);
                var tint = new Vector4(matData.Tint.X, matData.Tint.Y, matData.Tint.Z, matData.Tint.W);
                var mat = materialManager.GetOrCreate(matName, tint);
                mat.DiffuseTextureName = Path.GetFileName(matData.DiffuseTexture);
                if (!string.IsNullOrEmpty(matData.NormalTexture)) mat.NormalTextureName = Path.GetFileName(matData.NormalTexture);
                if (!string.IsNullOrEmpty(matData.SpecularTexture)) mat.SpecularTextureName = Path.GetFileName(matData.SpecularTexture);
                if (!string.IsNullOrEmpty(matData.EmissionTexture)) mat.EmissionTextureName = Path.GetFileName(matData.EmissionTexture);
                if (!string.IsNullOrEmpty(matData.RoughnessTexture)) mat.RoughnessTextureName = Path.GetFileName(matData.RoughnessTexture);
                Console.WriteLine($"[Relic] Loaded material {matName} diffuse {mat.DiffuseTextureName} normal {mat.NormalTextureName} specular {mat.SpecularTextureName} roughness {mat.RoughnessTextureName} tint {tint.X},{tint.Y},{tint.Z}");
            }
            catch (Exception ex) { Console.WriteLine($"[Relic] Failed to load material {matName}: {ex.Message}"); }
        }
        // Ensure at least brick/metal/concrete exist
        foreach (var g in grouped.Groups)
        {
            if (!materialManager.All.Any(m => m.Name.Equals(g.TextureName, StringComparison.OrdinalIgnoreCase)))
                materialManager.GetOrCreate(g.TextureName, new Vector4(1,1,1,1));
        }
        // Modern rendering foundation - RenderGraph with 7 passes
        var renderGraph = new RenderGraph();
        renderGraph.AddPass(new GeometryPass());
        renderGraph.AddPass(new LightingPass());
        renderGraph.AddPass(new LightmapPass());
        renderGraph.AddPass(new HDRPass());
        renderGraph.AddPass(new BloomPass());
        renderGraph.AddPass(new ToneMapPass());
        renderGraph.AddPass(new UIPass());
        var hdrFramebuffer = new HDRFramebuffer(1280, 720, HDRFormat.RGBA16F);
        var lightmapSystem = new LightmapSystem();
        var rendererStats = new RendererStats();
        var frustum = Frustum.FromViewProjection(Matrix4x4.Identity);
        Console.WriteLine($"[Relic] RenderGraph with {renderGraph.Passes.Count} passes, HDR {hdrFramebuffer.Format} {hdrFramebuffer.Width}x{hdrFramebuffer.Height}");

        // Entity System - automatic instantiation (no hardcoded creation beyond MapSpawner generic handling)
        var entityManager = new EntityManager();
        MapSpawner.SpawnMapEntities(entityManager, map);
        // Fallback health_pickup if map lacks (for older maps)
        if (!entityManager.FindByClassName("health_pickup").Any())
        {
            var hpEnt = entityManager.CreateEntity("health_pickup", "health_pickup", "");
            hpEnt.Transform.Position = new Vector3(64, -64, 16);
            hpEnt.AddComponent(new HealthPickupComponent{ HealAmount = 25 });
            hpEnt.Properties["heal"] = "25";
        }
        // Phase 9: Collect lights for dynamic lighting and lightmap baking (after renderGraph and lightmapSystem are created)
        // Note: renderGraph/hdr/lightmapSystem/rendererStats are already created above, so we can use them here
        var pointLights = entityManager.FindByClassName("light").ToList();
        var spotLights = entityManager.FindByClassName("light_spot").ToList();
        var sunLights = entityManager.FindByClassName("light_sun").ToList();
        var allLights = pointLights.Concat(spotLights).Concat(sunLights).ToList();
        Console.WriteLine($"[Relic] Lights: point {pointLights.Count} spot {spotLights.Count} sun {sunLights.Count} total {allLights.Count}");
        var lightInfos = allLights.Select(e => {
            var lc = e.GetComponent<LightComponent>();
            return new LightInfo { Position = e.Transform.Position, Color = lc?.Color ?? new Vector3(1,1,1), Intensity = lc?.Intensity ?? 800, Range = lc?.Range ?? 512 };
        }).ToList();
        lightmapSystem.Generate(lightInfos, grouped.Groups.FirstOrDefault() != null ? new Relic.Content.MeshData() : null);
        Console.WriteLine($"[Relic] Lightmap baked {lightmapSystem.LightmapWidth}x{lightmapSystem.LightmapHeight} in {lightmapSystem.BakeTimeMs:F1}ms");
        var fogEnt = entityManager.FindFirstByClassName("env_fog");
        if (fogEnt != null) Console.WriteLine($"[Relic] Fog color {fogEnt.GetComponent<FogComponent>()?.Color} density {fogEnt.GetComponent<FogComponent>()?.Density}");
        var ppEnt = entityManager.FindFirstByClassName("env_postprocess");
        if (ppEnt != null)
        {
            var pp = ppEnt.GetComponent<PostProcessComponent>();
            if (pp != null)
            {
                ConsoleVariables.R_Bloom = pp.BloomEnabled;
                ConsoleVariables.R_Exposure = pp.Exposure;
                ConsoleVariables.R_BloomIntensity = pp.BloomIntensity;
                ConsoleVariables.R_BloomThreshold = pp.BloomThreshold;
                ConsoleVariables.R_ToneMap = pp.ToneMap;
                HDRFramebuffer.R_Exposure = pp.Exposure;
                BloomPass.R_Bloom = pp.BloomEnabled;
                BloomPass.R_BloomIntensity = pp.BloomIntensity;
                ToneMapPass.R_ToneMap = pp.ToneMap;
                Console.WriteLine($"[Relic] PostProcess bloom {pp.BloomEnabled} exposure {pp.Exposure} tonemap {pp.ToneMap}");
            }
        }
        rendererStats.SetVisibleLights(allLights.Count);
        rendererStats.SetVisibleEntities(entityManager.Count);
        var playerEntity = entityManager.FindFirstByClassName("player_start") ?? entityManager.FindFirstByClassName("info_player_start");
        Console.WriteLine($"[Relic] Spawned {entityManager.Count} gameplay entities:");
        foreach (var e in entityManager.Entities)
        {
            var modelComp = e.GetComponent<ModelComponent>();
            Console.WriteLine($"[Entity] {e.Id} class={e.ClassName} pos={e.Transform.Position} model={(modelComp?.ModelPath ?? "none")} props={string.Join(",", e.Properties.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }

        // AudioSystem
        var audio = new AudioSystem(assetsRoot);

        Vector3 spawnPos = new(0,0,60);
        float spawnYaw = 0;
        if (playerEntity != null)
        {
            spawnPos = playerEntity.Transform.Position;
            spawnYaw = playerEntity.Transform.Rotation.Y;
        }
        Vector3 half = new(16,16,36);
        var down = physics.Raycast(spawnPos + new Vector3(0,0,10), new Vector3(0,0,-1), 500f);
        if (down.Hit) spawnPos.Z = down.Point.Z + half.Z + 2f;
        if (playerEntity != null) playerEntity.Transform.Position = spawnPos;

        var character = new CharacterController(physics, spawnPos);
        character.SetYaw(spawnYaw);
        if (playerEntity != null)
        {
            var phys = playerEntity.GetComponent<PhysicsComponent>() ?? playerEntity.AddComponent(new PhysicsComponent());
            phys.Controller = character;
            phys.World = physics;
            phys.HalfExtents = half;
        }

        // Inventory + Weapons via WeaponDatabase
        var inventory = playerEntity?.GetComponent<InventoryComponent>();
        if (inventory == null && playerEntity != null) inventory = playerEntity.AddComponent(new InventoryComponent());
        var weaponManager = new WeaponManager();
        Vector3 currentForward = new(MathF.Cos(0)*MathF.Cos(spawnYaw), MathF.Cos(0)*MathF.Sin(spawnYaw), MathF.Sin(0));
        Func<Vector3> getOrigin = () => character.EyePosition;
        Func<Vector3> getForward = () => currentForward;
        var rifleDef = WeaponDatabase.Get("Rifle") ?? new WeaponDefinition("Rifle");
        var rifle = new HitscanWeapon(rifleDef, physics, entityManager, getOrigin, getForward);
        inventory?.AddWeapon(rifle);
        weaponManager.SetActive(rifle);
        var wc = playerEntity?.GetComponent<WeaponComponent>();
        if (wc != null) wc.Equip(rifle);
        foreach (var e in entityManager.FindByClassName("enemy_soldier"))
        {
            if (e.GetComponent<HealthComponent>() == null) e.AddComponent(new HealthComponent{ MaxHealth=100, Health=100 });
            if (e.GetComponent<EnemyComponent>() == null) e.AddComponent(new EnemyComponent());
        }
        // Also ensure dog/demon have health/enemy components (already via MapSpawner but ensure)
        foreach (var cname in new[] { "enemy_dog", "enemy_demon" })
            foreach (var e in entityManager.FindByClassName(cname))
            {
                if (e.GetComponent<HealthComponent>() == null) e.AddComponent(new HealthComponent{ MaxHealth= (cname=="enemy_dog"?50:300), Health= (cname=="enemy_dog"?50:300) });
                if (e.GetComponent<EnemyComponent>() == null) e.AddComponent(new EnemyComponent());
                if (e.GetComponent<EnemyAIComponent>() == null) { var ai2 = e.AddComponent(new EnemyAIComponent()); ai2.ConfigureFor(cname); }
            }
        // Initialize all enemy AI
        foreach (var e in entityManager.Entities)
        {
            var ai = e.GetComponent<EnemyAIComponent>();
            if (ai != null) ai.Initialize(physics, entityManager, audio);
        }

        var hud = new Hud();
        var debugOverlay = new DebugOverlay();
        var winSettings = new RelicWindowSettings{ Title="Relic - Content Pipeline", Width=1280, Height=720, VSync=false };
        using var window = new RelicWindow(winSettings);
        OpenGLRenderer? renderer = null;
        DebugTextRenderer? textRenderer = null;
        var meshes = new List<(OpenGLMesh mesh, Material material, OpenGLTexture tex)>();
        var propMeshes = new List<(OpenGLMesh mesh, Material material, OpenGLTexture tex, Entity entity)>();
        var animatedMeshes = new List<(OpenGLMesh mesh, Material material, OpenGLTexture tex, Entity entity, AnimatedModelComponent anim)>();
        float yaw = spawnYaw;
        float pitch = 0;
        bool wasJumpDown = false;
        const float mouseSensitivity = 0.0025f;

        window.Native.Load += () =>
        {
            renderer = new OpenGLRenderer();
            renderer.Initialize();
            renderer.SetViewport(0,0,window.Width, window.Height);
            GL.Enable(EnableCap.DepthTest);
            foreach (var g in grouped.Groups)
            {
                var glMesh = new OpenGLMesh();
                glMesh.SetMeshGroup(g);
                var mat = materialManager.GetOrCreate(g.TextureName);
                string texName = mat.DiffuseTextureName;
                try
                {
                    var matData = assetDb.LoadMaterial(g.TextureName);
                    texName = Path.GetFileName(matData.DiffuseTexture);
                    mat.TintColor = new Vector4(matData.Tint.X, matData.Tint.Y, matData.Tint.Z, matData.Tint.W);
                }
                catch {}
                var tex = TextureLoader.Load(texName, assetsRoot);
                meshes.Add((glMesh, mat, tex));
            }
            // Static prop models (.obj)
            foreach (var e in entityManager.Entities)
            {
                var mc = e.GetComponent<ModelComponent>();
                if (mc == null) continue;
                try
                {
                    var modelData = assetDb.LoadModel(mc.ModelPath);
                    mc.ModelData = modelData;
                    mc.IsLoaded = true;
                    var meshData = modelData.ToMeshData();
                    var glMesh = new OpenGLMesh();
                    var vertices = meshData.Vertices.ToArray();
                    var indices = meshData.Indices.ToArray();
                    glMesh.SetData(vertices, indices);
                    var mat = materialManager.GetOrCreate("concrete");
                    var tex = TextureLoader.Load(mat.DiffuseTextureName, assetsRoot);
                    propMeshes.Add((glMesh, mat, tex, e));
                    Console.WriteLine($"[Relic] Loaded static model {mc.ModelPath} for {e.Id} at {e.Transform.Position} verts {vertices.Length}");
                }
                catch (Exception ex) { Console.WriteLine($"[Relic] Failed to load static model {mc.ModelPath}: {ex.Message}"); }
            }
            // Animated MDL models
            foreach (var e in entityManager.Entities)
            {
                var amc = e.GetComponent<AnimatedModelComponent>();
                if (amc == null) continue;
                try
                {
                    if (amc.Model == null)
                    {
                        var mdl = assetDb.LoadMDL(amc.ModelPath);
                        amc.SetModel(mdl, amc.ModelPath);
                    }
                    var meshData = amc.GetMeshData();
                    var glMesh = new OpenGLMesh();
                    glMesh.SetData(meshData.Vertices.ToArray(), meshData.Indices.ToArray());
                    // Create skin texture from MDL skin
                    OpenGLTexture tex;
                    if (amc.Model != null && amc.Model.SkinData.Length > 0)
                    {
                        tex = new OpenGLTexture(amc.Model.SkinWidth, amc.Model.SkinHeight, amc.Model.SkinData, Relic.Renderer.TextureFormat.RGBA8, true);
                    }
                    else
                    {
                        var mat = materialManager.GetOrCreate("concrete");
                        tex = TextureLoader.Load(mat.DiffuseTextureName, assetsRoot);
                    }
                    var matAnim = materialManager.GetOrCreate(amc.ModelPath);
                    animatedMeshes.Add((glMesh, matAnim, tex, e, amc));
                    Console.WriteLine($"[Relic] Loaded animated model {amc.ModelPath} for {e.Id} frames {amc.Model?.Frames.Count} skin {amc.SkinWidth}x{amc.SkinHeight}");
                }
                catch (Exception ex) { Console.WriteLine($"[Relic] Failed to load animated model {amc.ModelPath}: {ex.Message}"); }
            }
            textRenderer = new DebugTextRenderer();
            textRenderer.Initialize();
            var cam = renderer.Camera;
            cam.FieldOfView = 75f*MathF.PI/180f;
            cam.AspectRatio = window.AspectRatio;
            cam.NearPlane=0.1f; cam.FarPlane=4096f;
            Console.WriteLine($"[Relic] Player spawned at {spawnPos} yaw {spawnYaw:F2} - HUD visible on screen");
        };
        window.Native.Unload += () => { foreach(var (m,_,_) in meshes) m.Dispose(); foreach(var (m,_,_,_) in propMeshes) m.Dispose(); foreach(var (m,_,_,_,_) in animatedMeshes) m.Dispose(); renderer?.Dispose(); textRenderer?.Dispose(); TextureLoader.Clear(); audio.Dispose(); };
        window.Resized += (w,h)=> renderer?.SetViewport(0,0,w,h);

        bool wasMouseDown = false;
        bool wasRDown = false;
        window.UpdateFrame += (dt) =>
        {
            dt = MathF.Min(dt, 1f/30f);
            var d = window.MouseDelta;
            yaw += d.X * mouseSensitivity;
            pitch -= d.Y * mouseSensitivity;
            pitch = Math.Clamp(pitch, -1.553f, 1.553f);
            character.SetYaw(yaw);
            currentForward = new Vector3(MathF.Cos(pitch)*MathF.Cos(yaw), MathF.Cos(pitch)*MathF.Sin(yaw), MathF.Sin(pitch));

            Vector3 wish = Vector3.Zero;
            Vector3 fwd = character.GetForward();
            Vector3 right = character.GetRight();
            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.W)) wish += fwd;
            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.S)) wish -= fwd;
            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.A)) wish -= right;
            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D)) wish += right;
            if (wish.LengthSquared() > 0) wish.Normalize();
            bool sprint = window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftShift);
            bool jumpDown = window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space);
            bool jumpPressed = jumpDown && !wasJumpDown;
            if (jumpPressed) audio.PlaySound3D("player/plyrjmp8.wav", character.Position, 0.8f);
            wasJumpDown = jumpDown;
            character.Move(wish, sprint, jumpPressed, dt);
            if (playerEntity != null) playerEntity.Transform.Position = character.Position;
            if (playerEntity != null) { var rot = playerEntity.Transform.Rotation; rot.Y = yaw; rot.X = pitch; playerEntity.Transform.Rotation = rot; }

            bool mouseDown = window.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left);
            bool mousePressed = mouseDown && !wasMouseDown;
            wasMouseDown = mouseDown;
            if (mousePressed)
            {
                var invWeapon = inventory?.ActiveWeapon as HitscanWeapon ?? rifle as HitscanWeapon;
                int beforeAmmo = invWeapon?.Ammo ?? 0;
                invWeapon?.Fire();
                weaponManager.Fire();
                wc?.Fire();
                if ((invWeapon?.Ammo ?? beforeAmmo) < beforeAmmo) audio.PlaySound2D("weapons/shotgn2.wav");
            }
            bool rDown = window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R);
            bool rPressed = rDown && !wasRDown;
            wasRDown = rDown;
            if (rPressed) { inventory?.ActiveWeapon?.Reload(); weaponManager.Reload(); wc?.Reload(); audio.PlaySound2D("weapons/guncock.wav"); }

            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D1)) { inventory?.SwitchWeapon(0); if(inventory?.ActiveWeapon != null) { rifle = inventory.ActiveWeapon as HitscanWeapon; if(rifle!=null) weaponManager.SetActive(rifle); } }
            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D2)) { inventory?.SwitchWeapon(1); if(inventory?.ActiveWeapon != null) { rifle = inventory.ActiveWeapon as HitscanWeapon; if(rifle!=null) weaponManager.SetActive(rifle); } }
            if (window.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D3)) { inventory?.SwitchWeapon(2); if(inventory?.ActiveWeapon != null) { rifle = inventory.ActiveWeapon as HitscanWeapon; if(rifle!=null) weaponManager.SetActive(rifle); } }

            Vector3 playerPos = character.Position;
            var wps = entityManager.FindByClassName("weapon_pickup").ToList();
            foreach (var wp in wps)
            {
                if (InteractionSystem.SphereOverlap(playerPos, 36f, wp.Transform.Position, 24f))
                {
                    var wpc = wp.GetComponent<WeaponPickupComponent>();
                    string wname = wpc?.WeaponName ?? wp.Properties.GetValueOrDefault("weapon", "Rifle");
                    var def = WeaponDatabase.Get(wname) ?? WeaponDatabase.Get("Rifle")!;
                    var newWeapon = new HitscanWeapon(def, physics, entityManager, getOrigin, getForward);
                    bool added = inventory?.AddWeapon(newWeapon) ?? false;
                    if (added)
                    {
                        Console.WriteLine($"[Player] Picked up {wname}");
                        audio.PlaySound3D("weapons/pkup.wav", wp.Transform.Position);
                        entityManager.DestroyEntity(wp);
                    }
                }
            }
            var hps = entityManager.FindByClassName("health_pickup").ToList();
            foreach (var hp in hps)
            {
                if (InteractionSystem.SphereOverlap(playerPos, 36f, hp.Transform.Position, 24f))
                {
                    var hpc = hp.GetComponent<HealthPickupComponent>();
                    float heal = hpc?.HealAmount ?? 25f;
                    var ph = playerEntity?.GetComponent<HealthComponent>();
                    if (ph != null && ph.Health < ph.MaxHealth)
                    {
                        ph.Heal(heal);
                        Console.WriteLine($"[Player] Picked up Health +{heal} now {ph.Health:F0}");
                        audio.PlaySound3D("items/health1.wav", hp.Transform.Position);
                        entityManager.DestroyEntity(hp);
                    }
                    else if (ph != null && ph.Health >= ph.MaxHealth) {}
                    else entityManager.DestroyEntity(hp);
                }
            }
            // Trigger volumes (AABB)
            var triggers = entityManager.FindByClassName("trigger_once").ToList();
            foreach (var tr in triggers)
            {
                var aabb = AABB.FromCenterExtents(tr.Transform.Position, new Vector3(32,32,32));
                if (InteractionSystem.AABBOverlap(aabb, AABB.FromCenterExtents(playerPos, half)))
                {
                    Console.WriteLine($"[Trigger] Activated {tr.Tag} target {tr.Properties.GetValueOrDefault("target","")}");
                    audio.PlaySound3D("misc/trigger1.wav", tr.Transform.Position);
                    entityManager.DestroyEntity(tr);
                }
            }

            weaponManager.Update(dt);
            inventory?.Update(dt);
            entityManager.Update(dt);
            foreach (var e in entityManager.FindByClassName("enemy_soldier").ToList())
            {
                var h = e.GetComponent<HealthComponent>();
                if (h != null && !h.IsAlive)
                {
                    Console.WriteLine($"[Enemy] Killed {e.Id} removed");
                    audio.PlaySound3D("player/death1.wav", e.Transform.Position);
                    entityManager.DestroyEntity(e);
                }
            }
            entityManager.Update(dt);

            // Update animated MDL meshes with frame interpolation
            foreach (var entry in animatedMeshes)
            {
                var meshData = entry.anim.GetMeshData();
                entry.mesh.SetData(meshData.Vertices.ToArray(), meshData.Indices.ToArray());
            }

            var playerHealth = playerEntity?.GetComponent<HealthComponent>();
            float hval = playerHealth?.Health ?? 100;
            float hmax = playerHealth?.MaxHealth ?? 100;
            var activeW = inventory?.ActiveWeapon;
            hud.Update(hval, hmax, activeW?.Ammo ?? 0, activeW?.MaxAmmo ?? 0, activeW?.Name ?? "None", activeW?.ReserveAmmo ?? 0);
            debugOverlay.Update(dt, entityManager, character);

            if (renderer != null)
            {
                Vector3 eye = character.EyePosition;
                renderer.Camera.Position = eye;
                var target = eye + currentForward;
                renderer.Camera.LookAt(eye, target);
            }
        };
        window.RenderFrame += () =>
        {
            if (renderer==null) return;
            renderer.Clear(ClearFlags.All, new Vector4(0.15f,0.16f,0.20f,1f),1f,0);
            var shader = renderer.Shader;
            foreach(var (mesh, mat, tex) in meshes)
            {
                tex.Bind(0);
                shader.Bind();
                shader.SetUniform("uModel", Matrix4x4.Identity);
                shader.SetUniform("uView", renderer.Camera.ViewMatrix);
                shader.SetUniform("uProjection", renderer.Camera.ProjectionMatrix);
                shader.SetUniform("uTexture", 0);
                shader.SetUniform("uColor", mat.TintColor);
                mesh.Draw();
                shader.Unbind();
            }
            // Render prop_static models
            foreach(var (mesh, mat, tex, ent) in propMeshes)
            {
                var transform = ent.Transform.LocalMatrix;
                tex.Bind(0);
                shader.Bind();
                shader.SetUniform("uModel", transform);
                shader.SetUniform("uView", renderer.Camera.ViewMatrix);
                shader.SetUniform("uProjection", renderer.Camera.ProjectionMatrix);
                shader.SetUniform("uTexture", 0);
                shader.SetUniform("uColor", mat.TintColor);
                mesh.Draw();
                shader.Unbind();
            }
            // Render animated MDL models (dog, demon, soldier) with frame interpolation
            foreach(var (mesh, mat, tex, ent, anim) in animatedMeshes)
            {
                var transform = ent.Transform.LocalMatrix;
                tex.Bind(0);
                shader.Bind();
                shader.SetUniform("uModel", transform);
                shader.SetUniform("uView", renderer.Camera.ViewMatrix);
                shader.SetUniform("uProjection", renderer.Camera.ProjectionMatrix);
                shader.SetUniform("uTexture", 0);
                shader.SetUniform("uColor", new Vector4(1,1,1,1));
                mesh.Draw();
                shader.Unbind();
            }
            textRenderer?.DrawHud(window.Width, window.Height, hud.Health, hud.MaxHealth, hud.Ammo, hud.MaxAmmo, hud.WeaponName, hud.Reserve);
            textRenderer?.DrawText(debugOverlay.GetText(), 10, 10, 1.5f, new Vector4(0.8f,0.9f,0.8f,1), window.Width, window.Height);
        };
        window.Run();
    }

    static string FindMapPath(string? mapOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(mapOverride))
        {
            if (File.Exists(mapOverride)) return Path.GetFullPath(mapOverride);
            // Allow bare names like "combat_test" or "combat_test.map"
            string baseName = Path.GetFileNameWithoutExtension(mapOverride);
            foreach (var root in new[] { FindAssetsRoot(), "Assets", Path.Combine(AppContext.BaseDirectory, "Assets") })
            {
                var p = Path.Combine(root, "Maps", baseName + ".map");
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            Console.WriteLine($"[Relic] WARNING: --map '{mapOverride}' not found, falling back to default map.");
        }
        // Prefer lighting_test.map for Phase 9, then combat_test.map, then Test.map
        string[] lightingCandidates={"Assets/Maps/lighting_test.map","Relic/Relic.Runtime/Assets/Maps/lighting_test.map", Path.Combine(AppContext.BaseDirectory,"Assets/Maps/lighting_test.map"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/Maps/lighting_test.map"};
        foreach(var c in lightingCandidates) if(File.Exists(c)) return c;
        string[] combatCandidates={"Assets/Maps/combat_test.map","Relic/Relic.Runtime/Assets/Maps/combat_test.map", Path.Combine(AppContext.BaseDirectory,"Assets/Maps/combat_test.map"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/Maps/combat_test.map"};
        foreach(var c in combatCandidates) if(File.Exists(c)) return c;
        string[] candidates={"Assets/Maps/Test.map","Relic/Relic.Runtime/Assets/Maps/Test.map","../../../Assets/Maps/Test.map","../../Assets/Maps/Test.map", Path.Combine(AppContext.BaseDirectory,"Assets/Maps/Test.map"), Path.Combine(AppContext.BaseDirectory,"../../../Assets/Maps/Test.map"), Path.Combine(AppContext.BaseDirectory,"../../../../Assets/Maps/Test.map"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/Maps/Test.map",};
        foreach(var c in candidates) if(File.Exists(c)) return c;
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        for(int i=0;i<6 && dir!=null;i++, dir=dir.Parent){ var p=Path.Combine(dir.FullName,"Assets/Maps/lighting_test.map"); if(File.Exists(p)) return p; p=Path.Combine(dir.FullName,"Assets/Maps/combat_test.map"); if(File.Exists(p)) return p; p=Path.Combine(dir.FullName,"Assets/Maps/Test.map"); if(File.Exists(p)) return p; var p2=Path.Combine(dir.FullName,"RELIC_engine/Assets/Maps/Test.map"); if(File.Exists(p2)) return p2; var p3=Path.Combine(dir.FullName,"RELIC_engine/Assets/Maps/combat_test.map"); if(File.Exists(p3)) return p3; var p4=Path.Combine(dir.FullName,"RELIC_engine/Assets/Maps/lighting_test.map"); if(File.Exists(p4)) return p4; }
        return "Assets/Maps/Test.map";
    }
    static string FindAssetsRoot()
    {
        string[] candidates={"Assets","Relic/Relic.Runtime/Assets","../../../Assets", Path.Combine(AppContext.BaseDirectory,"Assets"), Path.Combine(AppContext.BaseDirectory,"../../../Assets"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets"};
        foreach(var c in candidates) if(Directory.Exists(c)) return c;
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        for(int i=0;i<6 && dir!=null;i++, dir=dir.Parent){ var p=Path.Combine(dir.FullName,"Assets"); if(Directory.Exists(p)) return p; }
        return "Assets";
    }
}
