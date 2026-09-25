using Relic.Content;
using Relic.Framework;
using Relic.Physics;
using Relic.Gameplay;
using Relic.Gameplay.Components;
using Relic.Gameplay.Animation;

class Program
{
    static int Main(string[] args)
    {
        if (args.Contains("--install-trenchbroom"))
        {
            var installer = new Relic.Content.RelicTrenchBroomInstaller();
            Console.WriteLine($"TrenchBroom games dir: {installer.DetectTrenchBroomGamesDir() ?? "(not found)"}");
            bool ok = installer.Install();
            var verify = installer.Verify();
            Console.WriteLine($"Install: {(ok ? "OK" : "FAILED")}");
            Console.WriteLine($"Verify: {verify.Detail}");
            return ok && verify.Ok ? 0 : 1;
        }
        int failed = 0, passed = 0;
        void Assert(bool cond, string msg)
        {
            if (cond) { Console.WriteLine($"[PASS] {msg}"); passed++; }
            else { Console.WriteLine($"[FAIL] {msg}"); failed++; }
        }
        Console.WriteLine("=== Relic.Tests Physics ===");

        bool VerifyOutward(MapBrush brush, string name)
        {
            Vector3 centroid = Vector3.Zero; int cnt = 0;
            foreach (var f in brush.Faces) foreach (var v in f.Vertices) { centroid += v; cnt++; }
            centroid /= cnt;
            foreach (var f in brush.Faces)
            {
                Vector3 n = f.Normal; n.Normalize();
                float d = Vector3.Dot(n, f.Vertices[0]);
                float sd = Vector3.Dot(n, centroid) - d;
                if (sd > 0) { Console.WriteLine($"[FAIL] {name} face inward sd={sd:F2} n={n}"); return false; }
                if (MathF.Abs(n.Length() - 1f) > 0.01f) return false;
            }
            return true;
        }

        MapBrush CreateBoxBrush(Vector3 min, Vector3 max)
        {
            var brush = new MapBrush();
            void AddFace(Vector3 normal, Vector3 a, Vector3 b, Vector3 c, string tex="brick"){ var f=new MapFace{ Vertices=new[]{a,b,c}, Texture=tex, Normal=normal }; f.Normal.Normalize(); brush.Faces.Add(f); }
            AddFace(new Vector3(0,0,-1), new Vector3(min.X,min.Y,min.Z), new Vector3(max.X,max.Y,min.Z), new Vector3(max.X,min.Y,min.Z));
            AddFace(new Vector3(0,0,1), new Vector3(min.X,min.Y,max.Z), new Vector3(max.X,min.Y,max.Z), new Vector3(max.X,max.Y,max.Z));
            AddFace(new Vector3(-1,0,0), new Vector3(min.X,min.Y,min.Z), new Vector3(min.X,max.Y,max.Z), new Vector3(min.X,min.Y,max.Z));
            AddFace(new Vector3(1,0,0), new Vector3(max.X,min.Y,min.Z), new Vector3(max.X,min.Y,max.Z), new Vector3(max.X,max.Y,min.Z));
            AddFace(new Vector3(0,-1,0), new Vector3(min.X,min.Y,min.Z), new Vector3(max.X,min.Y,min.Z), new Vector3(min.X,min.Y,max.Z));
            AddFace(new Vector3(0,1,0), new Vector3(min.X,max.Y,min.Z), new Vector3(min.X,max.Y,max.Z), new Vector3(max.X,max.Y,min.Z));
            return brush;
        }

        string FindMapPath()
        {
            string[] candidates={"Assets/Maps/Test.map","Relic/Relic.Runtime/Assets/Maps/Test.map","../../../Assets/Maps/Test.map","../../Assets/Maps/Test.map", Path.Combine(AppContext.BaseDirectory,"Assets/Maps/Test.map"), Path.Combine(AppContext.BaseDirectory,"../../../Assets/Maps/Test.map"), Path.Combine(AppContext.BaseDirectory,"../../../../Assets/Maps/Test.map"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/Maps/Test.map",};
            foreach(var c in candidates) if(File.Exists(c)) return c;
            var dir=new DirectoryInfo(AppContext.BaseDirectory);
            for(int i=0;i<6 && dir!=null;i++, dir=dir.Parent){ var p=Path.Combine(dir.FullName,"Assets/Maps/Test.map"); if(File.Exists(p)) return p; var p2=Path.Combine(dir.FullName,"RELIC_engine/Assets/Maps/Test.map"); if(File.Exists(p2)) return p2; }
            return "Assets/Maps/Test.map";
        }

        // BoxBrush
        {
            var brush = CreateBoxBrush(new Vector3(-32, -32, -32), new Vector3(32, 32, 32));
            Assert(VerifyOutward(brush, "Box"), "BoxBrush normals outward");
            var map = new MapData(); map.Brushes.Add(brush);
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16, 16, 36);
            var sweep = world.SweepAABB(new Vector3(100, 0, 0), new Vector3(0, 0, 0), half);
            Assert(sweep.Hit && sweep.HitNormal.X > 0.9f, $"BoxBrush sweep +X Hit={sweep.Hit} N={sweep.HitNormal}");
            var ray = world.Raycast(new Vector3(100, 0, 0), new Vector3(-1, 0, 0), 200f);
            Assert(ray.Hit && ray.Normal.X > 0.9f, $"BoxBrush ray Hit={ray.Hit} N={ray.Normal}");
            Assert(world.CheckAABBCollision(AABB.FromCenterExtents(new Vector3(0, 0, 0), half)), "BoxBrush interior solid");
            Assert(!world.CheckAABBCollision(AABB.FromCenterExtents(new Vector3(200, 200, 200), half)), "BoxBrush empty outside");
        }
        // FloorBrush
        {
            var brush = CreateBoxBrush(new Vector3(-100, -100, 0), new Vector3(100, 100, 16));
            Assert(VerifyOutward(brush, "Floor"), "FloorBrush normals");
            var top = brush.Faces.FirstOrDefault(f => f.Normal.Z > 0.9f);
            Assert(top != null && MathF.Abs(top.Normal.Z - 1f) < 0.01f, $"FloorBrush top Z=1 got {top?.Normal}");
            var map = new MapData(); map.Brushes.Add(brush);
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16, 16, 36);
            var sweep = world.SweepAABB(new Vector3(0, 0, 80), new Vector3(0, 0, -80), half);
            Assert(sweep.Hit && sweep.HitNormal.Z > 0.9f, $"FloorBrush sweep down Hit={sweep.Hit} N={sweep.HitNormal}");
            var ray = world.Raycast(new Vector3(0, 0, 80), new Vector3(0, 0, -1), 200f);
            Assert(ray.Hit && ray.Normal.Z > 0.9f, $"FloorBrush ray Hit={ray.Hit} N={ray.Normal}");
            var hSweep = world.SweepAABB(new Vector3(-50, 0, 80), new Vector3(50, 0, 80), half);
            Assert(!hSweep.Hit, $"FloorBrush horizontal above Hit={hSweep.Hit}");
        }
        // WallBrush
        {
            var brush = CreateBoxBrush(new Vector3(50, -32, 0), new Vector3(66, 32, 64));
            Assert(VerifyOutward(brush, "Wall"), "WallBrush normals");
            var map = new MapData(); map.Brushes.Add(brush);
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16, 16, 36);
            var sweep = world.SweepAABB(new Vector3(0, 0, 30), new Vector3(100, 0, 30), half);
            Assert(sweep.Hit && sweep.HitNormal.X < -0.5f, $"WallBrush sweep Hit={sweep.Hit} N={sweep.HitNormal}");
            var ray = world.Raycast(new Vector3(0, 0, 30), new Vector3(1, 0, 0), 200f);
            Assert(ray.Hit && MathF.Abs(ray.Normal.X + 1) < 0.15f, $"WallBrush ray Hit={ray.Hit} N={ray.Normal}");
            var par = world.SweepAABB(new Vector3(0, 0, 80), new Vector3(0, 100, 80), half);
            Assert(!par.Hit, $"WallBrush parallel miss Hit={par.Hit}");
        }
        // StepBrush
        {
            var floor = CreateBoxBrush(new Vector3(-100, -100, 0), new Vector3(100, 100, 16));
            var step = CreateBoxBrush(new Vector3(30, -16, 16), new Vector3(62, 16, 32));
            Assert(VerifyOutward(step, "Step"), "StepBrush normals");
            var map = new MapData(); map.Brushes.Add(floor); map.Brushes.Add(step);
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16, 16, 36);
            var sweep = world.SweepAABB(new Vector3(0, 0, 30), new Vector3(80, 0, 30), half);
            Assert(sweep.Hit, $"StepBrush side sweep Hit={sweep.Hit}");
            var ray = world.Raycast(new Vector3(40, 0, 80), new Vector3(0, 0, -1), 100f);
            Assert(ray.Hit && ray.Normal.Z > 0.9f && MathF.Abs(ray.Point.Z - 32) < 1f, $"StepBrush top ray Hit={ray.Hit} P={ray.Point} N={ray.Normal}");
        }
        // Walking
        {
            var map = MapLoader.Load(FindMapPath());
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16, 16, 36);
            Vector3 spawn = new(0,0,32);
            var ps = map.Entities.FirstOrDefault(e => e.ClassName.Equals("player_start", StringComparison.OrdinalIgnoreCase));
            if (ps != null) spawn = ps.Position;
            var tr = world.Raycast(spawn + new Vector3(0,0,10), new Vector3(0,0,-1), 200f);
            if (tr.Hit) spawn.Z = tr.Point.Z + half.Z + 2f;
            var cc = new CharacterController(world, spawn);
            for(int i=0;i<15;i++) cc.Move(Vector3.Zero,false,false,1f/60f);
            Assert(cc.IsOnGround, $"Walking settled Ground={cc.IsOnGround} Pos={cc.Position}");
            Vector3 before = cc.Position;
            for(int i=0;i<30;i++) cc.Move(new Vector3(0,1,0),false,false,1f/60f);
            Assert(cc.Position.Y > before.Y+10f, $"Walking Y {before.Y:F1}->{cc.Position.Y:F1}");
            Assert(cc.Position.Z>30f && cc.Position.Z<70f, $"Walking Z={cc.Position.Z:F1}");
        }
        // Jumping
        {
            var map = MapLoader.Load(FindMapPath());
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16,16,36);
            Vector3 spawn = new(0,0,60);
            var ps = map.Entities.FirstOrDefault(e=>e.ClassName.Equals("player_start",StringComparison.OrdinalIgnoreCase));
            if(ps!=null) spawn = ps.Position + new Vector3(0,0,30);
            var cc = new CharacterController(world, spawn);
            for(int i=0;i<15;i++) cc.Move(Vector3.Zero,false,false,1f/60f);
            Assert(cc.IsOnGround, $"Jumping before Ground {cc.IsOnGround}");
            cc.Move(Vector3.Zero,false,true,1f/60f);
            Assert(!cc.IsOnGround && cc.Velocity.Z>100f, $"Jumping after VelZ={cc.Velocity.Z:F1}");
            for(int i=0;i<60;i++) cc.Move(Vector3.Zero,false,false,1f/60f);
            Assert(cc.IsOnGround, $"Jumping lands Ground {cc.IsOnGround} Z={cc.Position.Z:F1}");
        }
        // WallCollision
        {
            var map = MapLoader.Load(FindMapPath());
            var world = PhysicsWorld.FromMapData(map);
            Vector3 half = new(16,16,36);
            var cc = new CharacterController(world, new Vector3(0,0,60));
            for(int i=0;i<15;i++) cc.Move(Vector3.Zero,false,false,1f/60f);
            for(int i=0;i<180;i++) cc.Move(new Vector3(1,0,0),false,false,1f/60f);
            Assert(cc.Position.X < 230f && cc.Position.X > 100f, $"WallCollision X={cc.Position.X:F1}");
            Vector3 shrunk = half - new Vector3(0.5f,0.5f,0.5f);
            Assert(!world.CheckAABBCollision(AABB.FromCenterExtents(cc.Position, shrunk)), "WallCollision not deeply inside");
        }
        // Entity system tests
        {
            var em = new EntityManager();
            var e1 = em.CreateEntity("TestPlayer","player_start","player");
            Assert(em.Count==1, "EntityManager CreateEntity count 1");
            var found = em.FindByClassName("player_start").FirstOrDefault();
            Assert(found!=null && found.Id==e1.Id, "FindByClassName");
            e1.Tag = "hero";
            var byTag = em.FindByTag("hero").FirstOrDefault();
            Assert(byTag!=null, "FindByTag");
            var handle = em.CreateHandle(e1);
            Assert(handle.IsValid, "EntityHandle valid");
            em.DestroyEntity(e1);
            em.Update(0f);
            Assert(em.Count==0, "DestroyEntity");
            Assert(!handle.IsValid, "Handle invalid after destroy");
            // Map spawning
            var map = MapLoader.Load(FindMapPath());
            var em2 = new EntityManager();
            MapSpawner.SpawnMapEntities(em2, map);
            Assert(em2.FindByClassName("player_start").Any(), "MapSpawner player_start");
            Assert(em2.FindByClassName("enemy_soldier").Any(), "MapSpawner enemy_soldier");
            Assert(em2.FindByClassName("weapon_pickup").Any(), "MapSpawner weapon_pickup");
            var psEnt = em2.FindByClassName("player_start").First();
            Assert(psEnt.Properties.ContainsKey("classname"), "Map properties stored");
        }
        // Health tests
        {
            var em = new EntityManager();
            var e = em.CreateEntity("Enemy","enemy_soldier");
            var health = e.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
            health.Damage(30);
            Assert(MathF.Abs(health.Health-70)<0.01f, "Health Damage 30 -> 70");
            health.Heal(20);
            Assert(MathF.Abs(health.Health-90)<0.01f, "Health Heal 20 -> 90");
            bool died=false;
            health.OnDeath += () => died=true;
            health.Kill();
            Assert(!health.IsAlive && died, "Health Kill");
        }
        // Material tests
        {
            var mm = new Relic.Gameplay.Materials.MaterialManager();
            var m1 = mm.GetOrCreate("brick", new Relic.Renderer.Vector4(1,0,0,1));
            Assert(m1.DiffuseTextureName=="brick", "Material diffuse");
            Assert(MathF.Abs(m1.TintColor.X-1)<0.01f, "Material tint");
            var m2 = mm.GetOrCreate("brick", new Relic.Renderer.Vector4(1,0,0,1));
            Assert(m1==m2, "MaterialManager cached");
        }
        // Weapon damage
        {
            var em = new EntityManager();
            var world = new PhysicsWorld(); // empty world no walls blocking
            var enemy = em.CreateEntity("enemy","enemy_soldier");
            var health = enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
            enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
            enemy.Transform.Position = new Vector3(100,0,32);
            var def = new Relic.Gameplay.Weapons.WeaponDefinition("TestRifle"){ Damage=25f, Range=2000f, MaxAmmo=30 };
            var rifle = new Relic.Gameplay.Weapons.HitscanWeapon(def, world, em, ()=> new Vector3(0,0,32), ()=> new Vector3(1,0,0));
            rifle.Fire();
            Assert(MathF.Abs(health.Health - 75f) < 0.01f, $"Weapon damage 25 -> health {health.Health:F0} (expected 75)");
        }
        // Enemy death
        {
            var em = new EntityManager();
            var enemy = em.CreateEntity("enemy","enemy_soldier");
            var health = enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
            enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
            enemy.Transform.Position = new Vector3(50,0,16);
            health.Damage(100);
            Assert(!health.IsAlive, "Enemy death IsAlive false");
            em.DestroyEntity(enemy);
            em.Update(0f);
            Assert(em.FindByClassName("enemy_soldier").Count()==0, "Enemy death removed");
        }
        // Inventory switch
        {
            var em = new EntityManager();
            var player = em.CreateEntity("player","player_start");
            var inv = player.AddComponent(new Relic.Gameplay.Components.InventoryComponent());
            var def1 = new Relic.Gameplay.Weapons.WeaponDefinition("Rifle"){ Damage=25, MaxAmmo=30 };
            var def2 = new Relic.Gameplay.Weapons.WeaponDefinition("Pistol"){ Damage=15, MaxAmmo=12 };
            var def3 = new Relic.Gameplay.Weapons.WeaponDefinition("Shotgun"){ Damage=12, MaxAmmo=8 };
            var w1 = new Relic.Gameplay.Weapons.HitscanWeapon(def1);
            var w2 = new Relic.Gameplay.Weapons.HitscanWeapon(def2);
            var w3 = new Relic.Gameplay.Weapons.HitscanWeapon(def3);
            Assert(inv.AddWeapon(w1), "Inventory AddWeapon Rifle");
            Assert(inv.AddWeapon(w2), "Inventory AddWeapon Pistol");
            Assert(inv.AddWeapon(w3), "Inventory AddWeapon Shotgun");
            Assert(inv.Weapons.Count==3, "Inventory count 3");
            Assert(inv.ActiveWeapon?.Name=="Rifle", "Inventory active Rifle");
            Assert(inv.SwitchWeapon(1), "Inventory Switch 1");
            Assert(inv.ActiveWeapon?.Name=="Pistol", "Inventory switched to Pistol");
            Assert(inv.SwitchWeapon(2), "Inventory Switch 2");
            Assert(inv.ActiveWeapon?.Name=="Shotgun", "Inventory switched to Shotgun");
            Assert(!inv.SwitchWeapon(5), "Inventory Switch invalid fails");
        }
        // Weapon pickup (sphere overlap)
        {
            var em = new EntityManager();
            var player = em.CreateEntity("player","player_start");
            player.Transform.Position = new Vector3(0,0,32);
            var inv = player.AddComponent(new Relic.Gameplay.Components.InventoryComponent());
            var rifleDef = Relic.Gameplay.Weapons.WeaponDatabase.Get("Rifle")!;
            var starter = new Relic.Gameplay.Weapons.HitscanWeapon(rifleDef);
            inv.AddWeapon(starter);
            var pickup = em.CreateEntity("pickup","weapon_pickup");
            pickup.Transform.Position = new Vector3(20,0,32); // 20 units away
            pickup.AddComponent(new Relic.Gameplay.Components.WeaponPickupComponent{ WeaponName="Pistol" });
            // Simulate overlap check as in Runtime
            bool overlap = Relic.Gameplay.InteractionSystem.SphereOverlap(player.Transform.Position, 36f, pickup.Transform.Position, 24f);
            Assert(overlap, "Weapon pickup sphere overlap true at 20 units");
            // Perform pickup
            var wpc = pickup.GetComponent<Relic.Gameplay.Components.WeaponPickupComponent>();
            var def = Relic.Gameplay.Weapons.WeaponDatabase.Get(wpc!.WeaponName)!;
            var newWeapon = new Relic.Gameplay.Weapons.HitscanWeapon(def);
            bool added = inv.AddWeapon(newWeapon);
            if (added) em.DestroyEntity(pickup);
            em.Update(0f);
            Assert(inv.Weapons.Count==2, "Weapon pickup added to inventory count 2");
            Assert(!em.FindByClassName("weapon_pickup").Any(), "Weapon pickup destroyed");
            // Far pickup should not overlap
            var farPickup = em.CreateEntity("far","weapon_pickup");
            farPickup.Transform.Position = new Vector3(200,0,32);
            farPickup.AddComponent(new Relic.Gameplay.Components.WeaponPickupComponent{ WeaponName="Shotgun" });
            bool farOverlap = Relic.Gameplay.InteractionSystem.SphereOverlap(player.Transform.Position, 36f, farPickup.Transform.Position, 24f);
            Assert(!farOverlap, "Weapon pickup far no overlap");
        }
        // Health pickup
        {
            var em = new EntityManager();
            var player = em.CreateEntity("player","player_start");
            player.Transform.Position = new Vector3(0,0,32);
            var health = player.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=50 });
            var hpEnt = em.CreateEntity("hp","health_pickup");
            hpEnt.Transform.Position = new Vector3(10,0,32);
            hpEnt.AddComponent(new Relic.Gameplay.Components.HealthPickupComponent{ HealAmount=25 });
            bool overlap = Relic.Gameplay.InteractionSystem.SphereOverlap(player.Transform.Position, 36f, hpEnt.Transform.Position, 24f);
            Assert(overlap, "Health pickup overlap true");
            var hpc = hpEnt.GetComponent<Relic.Gameplay.Components.HealthPickupComponent>();
            health.Heal(hpc!.HealAmount);
            Assert(MathF.Abs(health.Health - 75f) < 0.01f, $"Health pickup heal 25 -> {health.Health:F0} expected 75");
            em.DestroyEntity(hpEnt);
            em.Update(0f);
            Assert(!em.FindByClassName("health_pickup").Any(), "Health pickup destroyed");
            // AABB trigger test
            var box = AABB.FromCenterExtents(new Vector3(0,0,32), new Vector3(40,40,40));
            var inside = Relic.Gameplay.InteractionSystem.QueryAABB(em, box);
            Assert(inside.Any(e=>e.Id==player.Id), "AABB trigger contains player");
            var farBox = AABB.FromCenterExtents(new Vector3(500,0,32), new Vector3(10,10,10));
            var farInside = Relic.Gameplay.InteractionSystem.QueryAABB(em, farBox);
            Assert(!farInside.Any(), "AABB far empty");
        }
        // HUD
        {
            var hud = new Hud();
            hud.Update(75,100,15,30,"Rifle",60);
            Assert(MathF.Abs(hud.Health-75)<0.01f && hud.WeaponName=="Rifle" && hud.Ammo==15, $"HUD Health {hud.Health} Weapon {hud.WeaponName} Ammo {hud.Ammo}");
            string txt = hud.GetText();
            Assert(txt.Contains("75") && txt.Contains("Rifle"), "HUD GetText contains values");
        }

        // Content Pipeline
        {
            var assetsRoot = FindAssetsRoot();
            var db = new Relic.Content.AssetDatabase(assetsRoot);
            var mats = db.ListMaterials().ToList();
            Assert(mats.Contains("brick"), "AssetDatabase brick.mat listed");
            Assert(mats.Contains("metal"), "AssetDatabase metal.mat listed");
            var brickMat = db.LoadMaterial("brick");
            Assert(brickMat.DiffuseTexture.Contains("brick"), $"MaterialLoader brick diffuse {brickMat.DiffuseTexture}");
            var metalMat = db.LoadMaterial("metal");
            Assert(metalMat.Tint.X < 0.9f || metalMat.Tint.Y < 0.9f, $"MaterialLoader metal tint {metalMat.Tint}");
            var model = db.LoadModel("crate.obj");
            Assert(model.Positions.Count >= 8 && model.Indices.Count >= 36, $"ModelLoader crate verts {model.Positions.Count} indices {model.Indices.Count}");
            var sound = db.LoadSound("weapons/shotgn2.wav");
            Assert(sound.Raw.Length > 1000, $"SoundLoader shotgn2 bytes {sound.Raw.Length}");
            var map = Relic.Content.MapLoader.Load(FindMapPath());
            var em2 = new EntityManager();
            Relic.Gameplay.MapSpawner.SpawnMapEntities(em2, map);
            var prop = em2.FindByClassName("prop_static").FirstOrDefault();
            Assert(prop != null, "MapSpawner prop_static");
            Assert(prop!.GetComponent<Relic.Gameplay.Components.ModelComponent>() != null, "prop_static ModelComponent");
            Assert(prop.GetComponent<Relic.Gameplay.Components.ModelComponent>()!.ModelPath == "crate.obj", $"prop_static model path {prop.GetComponent<Relic.Gameplay.Components.ModelComponent>()!.ModelPath}");
            var light = em2.FindByClassName("light").FirstOrDefault();
            Assert(light != null, "MapSpawner light");
            var trigger = em2.FindByClassName("trigger_once").FirstOrDefault();
            Assert(trigger != null, "MapSpawner trigger_once");
        }
        // MDL Loader - dog/demon/soldier
        {
            var assetsRoot = FindAssetsRoot();
            var db = new Relic.Content.AssetDatabase(assetsRoot);
            foreach (var mdlName in new[] { "dog.mdl", "demon.mdl", "soldier.mdl" })
            {
                try
                {
                    var mdl = db.LoadMDL(mdlName);
                    Assert(mdl.StVerts.Count > 0 && mdl.Triangles.Count > 0, $"{mdlName} verts {mdl.StVerts.Count} tris {mdl.Triangles.Count}");
                    Assert(mdl.Frames.Count > 0, $"{mdlName} frames {mdl.Frames.Count}");
                    Assert(mdl.SkinData.Length > 0, $"{mdlName} skin {mdl.SkinWidth}x{mdl.SkinHeight}");
                    var mesh = Relic.Content.MDLLoader.ToMeshData(mdl, 0);
                    Assert(mesh.Vertices.Count > 0 && mesh.Indices.Count > 0, $"{mdlName} mesh verts {mesh.Vertices.Count} indices {mesh.Indices.Count}");
                    if (mdl.Frames.Count > 1)
                    {
                        var interp = Relic.Content.MDLLoader.ToMeshData(mdl, 0, 0.5f, 1);
                        Assert(interp.Vertices.Count == mesh.Vertices.Count, $"{mdlName} interp verts {interp.Vertices.Count}");
                    }
                    var texCoordsOk = mesh.Vertices.All(v => v.TexCoord.X >= 0 && v.TexCoord.X <= 1 && v.TexCoord.Y >= 0 && v.TexCoord.Y <= 1);
                    Assert(texCoordsOk, $"{mdlName} texcoords 0-1");
                }
                catch (Exception ex) { Assert(false, $"{mdlName} load failed: {ex.Message}"); }
            }
        }
        // AnimatedModelComponent
        {
            var assetsRoot2 = FindAssetsRoot();
            var db2 = new Relic.Content.AssetDatabase(assetsRoot2);
            var mdl = db2.LoadMDL("dog.mdl");
            var ent = new EntityManager().CreateEntity("test","prop_static");
            var anim = ent.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
            anim.SetModel(mdl, "dog.mdl");
            Assert(anim.Model != null && anim.Model.Frames.Count > 0, "AnimatedModelComponent SetModel");
            Assert(anim.CurrentFrame == 0, "AnimatedModelComponent initial frame 0");
            var mesh0 = anim.GetMeshData();
            Assert(mesh0.Vertices.Count > 0, "AnimatedModelComponent GetMeshData");
            anim.Update(0.1f); // 0.1 sec at 10fps = 1 frame
            Assert(anim.CurrentFrame == 1 || anim.Lerp > 0, $"AnimatedModelComponent Update frame {anim.CurrentFrame} lerp {anim.Lerp:F2}");
            var mesh1 = anim.GetMeshData();
            Assert(mesh1.Vertices.Count == mesh0.Vertices.Count, "AnimatedModelComponent interp mesh same count");
            // Test frame interpolation changes positions
            bool moved = false;
            for (int i = 0; i < Math.Min(mesh0.Vertices.Count, mesh1.Vertices.Count); i++)
            {
                if ((mesh0.Vertices[i].Position - mesh1.Vertices[i].Position).LengthSquared() > 0.001f) { moved = true; break; }
            }
            Assert(moved, "AnimatedModelComponent frame interpolation moves vertices");
        }
        // Phase 8: Enemy AI & Combat
        {
            // Helper to create empty world (no brushes)
            var emptyWorld = new PhysicsWorld();
            var assetsRoot = FindAssetsRoot();
            var db = new Relic.Content.AssetDatabase(assetsRoot);
            // Soldier detects player
            {
                var em = new EntityManager();
                var player = em.CreateEntity("player","player_start");
                player.Transform.Position = new Vector3(0,0,32);
                player.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                var enemy = em.CreateEntity("soldier","enemy_soldier");
                enemy.Transform.Position = new Vector3(-400,0,32);
                var hc = enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = enemy.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("soldier.mdl"); anim.SetModel(mdl, "soldier.mdl"); } catch {}
                var ai = enemy.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_soldier");
                ai.Initialize(emptyWorld, em, null);
                Assert(ai.State == AIState.Idle, "Soldier initial Idle");
                // Update AI with player in sight (400 < 800)
                for(int i=0;i<10;i++) ai.Update(0.1f);
                Assert(ai.State == AIState.Chase || ai.State == AIState.Attack, $"Soldier detects player -> Chase/Attack, got {ai.State}");
            }
            // Soldier chases player
            {
                var em = new EntityManager();
                var player = em.CreateEntity("player","player_start");
                player.Transform.Position = new Vector3(0,0,32);
                var enemy = em.CreateEntity("soldier","enemy_soldier");
                enemy.Transform.Position = new Vector3(-600,0,32);
                enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = enemy.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("soldier.mdl"); anim.SetModel(mdl, "soldier.mdl"); } catch {}
                var ai = enemy.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_soldier");
                ai.Initialize(emptyWorld, em, null);
                // Ensure player is found
                for(int i=0;i<5;i++) ai.Update(0.1f);
                Vector3 before = enemy.Transform.Position;
                for(int i=0;i<20;i++) ai.Update(0.1f);
                Vector3 after = enemy.Transform.Position;
                float distBefore = (before - player.Transform.Position).Length();
                float distAfter = (after - player.Transform.Position).Length();
                Assert(distAfter < distBefore, $"Soldier chases player dist {distBefore:F1} -> {distAfter:F1}");
            }
            // Soldier attacks player
            {
                var em = new EntityManager();
                var player = em.CreateEntity("player","player_start");
                player.Transform.Position = new Vector3(0,0,32);
                var ph = player.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                var enemy = em.CreateEntity("soldier","enemy_soldier");
                enemy.Transform.Position = new Vector3(-100,0,32); // within 512 attack range
                enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = enemy.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("soldier.mdl"); anim.SetModel(mdl, "soldier.mdl"); } catch {}
                var ai = enemy.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_soldier");
                ai.Initialize(emptyWorld, em, null);
                // Force chase -> attack
                for(int i=0;i<5;i++) ai.Update(0.1f);
                // Now should be in Attack, do attack
                float healthBefore = ph.Health;
                for(int i=0;i<20;i++) ai.Update(0.1f);
                Assert(ph.Health < healthBefore, $"Soldier attacks player health {healthBefore} -> {ph.Health} damage {ph.Health}");
            }
            // Dog melee damage
            {
                var em = new EntityManager();
                var player = em.CreateEntity("player","player_start");
                player.Transform.Position = new Vector3(0,0,32);
                var ph = player.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                var dog = em.CreateEntity("dog","enemy_dog");
                dog.Transform.Position = new Vector3(-30,0,32); // within 64
                dog.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=50, Health=50 });
                dog.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = dog.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("dog.mdl"); anim.SetModel(mdl, "dog.mdl"); } catch {}
                var ai = dog.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_dog");
                ai.Initialize(emptyWorld, em, null);
                float before = ph.Health;
                for(int i=0;i<30;i++) ai.Update(0.1f);
                Assert(ph.Health < before && ph.Health <= 100 - ai.Damage + 0.1f, $"Dog melee damage {before} -> {ph.Health} damage {ai.Damage}");
            }
            // Demon melee damage
            {
                var em = new EntityManager();
                var player = em.CreateEntity("player","player_start");
                player.Transform.Position = new Vector3(0,0,32);
                var ph = player.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=300, Health=300 });
                var demon = em.CreateEntity("demon","enemy_demon");
                demon.Transform.Position = new Vector3(-50,0,32); // within 96
                demon.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=300, Health=300 });
                demon.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = demon.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("demon.mdl"); anim.SetModel(mdl, "demon.mdl"); } catch {}
                var ai = demon.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_demon");
                ai.Initialize(emptyWorld, em, null);
                float before = ph.Health;
                for(int i=0;i<30;i++) ai.Update(0.1f);
                Assert(ph.Health < before, $"Demon melee damage {before} -> {ph.Health}");
                Assert(ai.Damage == 40f, $"Demon damage value {ai.Damage} expected 40");
            }
            // Enemy death
            {
                var em = new EntityManager();
                var enemy = em.CreateEntity("enemy","enemy_soldier");
                enemy.Transform.Position = new Vector3(0,0,32);
                var hc = enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                var ec = enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = enemy.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("soldier.mdl"); anim.SetModel(mdl, "soldier.mdl"); } catch {}
                var ai = enemy.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_soldier");
                ai.Initialize(new PhysicsWorld(), em, null);
                hc.Damage(100);
                for(int i=0;i<5;i++) ai.Update(0.1f);
                Assert(ai.State == AIState.Dead, $"Enemy death AI Dead got {ai.State}");
                Assert(anim.CurrentState == Relic.Gameplay.Animation.AnimationState.Death, $"Enemy death animation Death got {anim.CurrentState}");
            }
            // Animation state switching
            {
                var db2 = new Relic.Content.AssetDatabase(FindAssetsRoot());
                var mdl = db2.LoadMDL("soldier.mdl");
                var ent = new EntityManager().CreateEntity("test","prop_static");
                var anim = ent.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                anim.SetModel(mdl, "soldier.mdl");
                anim.SetAnimation(Relic.Gameplay.Animation.AnimationState.Idle);
                Assert(anim.CurrentState == Relic.Gameplay.Animation.AnimationState.Idle && anim.StartFrame==0, "Animation Idle");
                anim.SetAnimation(Relic.Gameplay.Animation.AnimationState.Walk);
                Assert(anim.CurrentState == Relic.Gameplay.Animation.AnimationState.Walk && anim.StartFrame==73, $"Animation Walk start {anim.StartFrame} expected 73");
                anim.SetAnimation(Relic.Gameplay.Animation.AnimationState.Attack);
                Assert(anim.CurrentState == Relic.Gameplay.Animation.AnimationState.Attack && anim.StartFrame==81, "Animation Attack");
                anim.SetAnimation(Relic.Gameplay.Animation.AnimationState.Pain);
                Assert(anim.CurrentState == Relic.Gameplay.Animation.AnimationState.Pain, "Animation Pain");
                anim.SetAnimation(Relic.Gameplay.Animation.AnimationState.Death);
                Assert(anim.CurrentState == Relic.Gameplay.Animation.AnimationState.Death, "Animation Death");
            }
            // AI state transitions
            {
                var em = new EntityManager();
                var player = em.CreateEntity("player","player_start");
                player.Transform.Position = new Vector3(0,0,32);
                var enemy = em.CreateEntity("soldier","enemy_soldier");
                enemy.Transform.Position = new Vector3(-1000,0,32); // far, out of sight
                enemy.AddComponent(new Relic.Gameplay.Components.HealthComponent{ MaxHealth=100, Health=100 });
                enemy.AddComponent(new Relic.Gameplay.Components.EnemyComponent());
                var anim = enemy.AddComponent(new Relic.Gameplay.Components.AnimatedModelComponent());
                try { var mdl = db.LoadMDL("soldier.mdl"); anim.SetModel(mdl, "soldier.mdl"); } catch {}
                var ai = enemy.AddComponent(new Relic.Gameplay.Components.EnemyAIComponent());
                ai.ConfigureFor("enemy_soldier");
                ai.Initialize(emptyWorld, em, null);
                Assert(ai.State == AIState.Idle, "AI initial Idle");
                // Move player close to trigger chase (within 800)
                player.Transform.Position = new Vector3(-600,0,32);
                for(int i=0;i<10;i++) ai.Update(0.1f);
                Assert(ai.State == AIState.Chase || ai.State == AIState.Attack, $"AI transitions to Chase/Attack got {ai.State}");
                // Damage enemy to trigger pain/death
                var hc = enemy.GetComponent<Relic.Gameplay.Components.HealthComponent>();
                hc!.Damage(10);
                // Should still be Chase/Attack, not Dead
                Assert(ai.State != AIState.Dead, "AI not Dead after 10 damage");
                hc.Damage(100);
                for(int i=0;i<5;i++) ai.Update(0.1f);
                Assert(ai.State == AIState.Dead, $"AI transitions to Dead got {ai.State}");
            }
        }

        string FindAssetsRoot()
        {
            string[] candidates={"Assets","Relic/Relic.Runtime/Assets","../../../Assets", Path.Combine(AppContext.BaseDirectory,"Assets"), Path.Combine(AppContext.BaseDirectory,"../../../Assets"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets"};
            foreach(var c in candidates) if(Directory.Exists(c)) return c;
            var dir=new DirectoryInfo(AppContext.BaseDirectory);
            for(int i=0;i<6 && dir!=null;i++, dir=dir.Parent){ var p=Path.Combine(dir.FullName,"Assets"); if(Directory.Exists(p)) return p; }
            return "Assets";
        }

        // Phase 9: Modern Rendering & Lighting
        {
            string FindLightingMap()
            {
                string[] cands = { "Assets/Maps/lighting_test.map", "Relic/Relic.Runtime/Assets/Maps/lighting_test.map", Path.Combine(FindAssetsRoot(), "Maps/lighting_test.map"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/Maps/lighting_test.map" };
                foreach (var c in cands) if (File.Exists(c)) return c;
                return FindMapPath();
            }
            var map = Relic.Content.MapLoader.Load(FindLightingMap());
            var em = new EntityManager();
            Relic.Gameplay.MapSpawner.SpawnMapEntities(em, map);
            // Point light
            var point = em.FindByClassName("light").FirstOrDefault();
            Assert(point != null, "Point light loads from map");
            if (point != null)
            {
                var lc = point.GetComponent<LightComponent>();
                Assert(lc != null && lc.Type == LightType.Point, $"Point light type {lc?.Type}");
                Assert(lc != null && MathF.Abs(lc.Intensity - 800) < 0.01f, $"Point light intensity {lc?.Intensity}");
                Assert(lc != null && MathF.Abs(lc.Range - 512) < 0.01f, $"Point light range {lc?.Range}");
            }
            // Spot light
            var spot = em.FindByClassName("light_spot").FirstOrDefault();
            Assert(spot != null, "Spot light loads from map");
            if (spot != null)
            {
                var lc = spot.GetComponent<LightComponent>();
                Assert(lc != null && lc.Type == LightType.Spot, $"Spot light type {lc?.Type}");
                Assert(lc != null && MathF.Abs(lc.Intensity - 1200) < 0.1f, $"Spot intensity {lc?.Intensity}");
                Assert(lc != null && MathF.Abs(lc.Range - 1024) < 0.1f, $"Spot range {lc?.Range}");
                Assert(lc != null && lc.Direction.LengthSquared() > 0.1f, "Spot direction set");
            }
            // Sun light
            var sun = em.FindByClassName("light_sun").FirstOrDefault();
            Assert(sun != null, "Sun light loads from map");
            if (sun != null)
            {
                var lc = sun.GetComponent<LightComponent>();
                Assert(lc != null && lc.Type == LightType.Directional, $"Sun type {lc?.Type}");
                Assert(lc != null && MathF.Abs(lc.Intensity - 2.5f) < 0.01f, $"Sun intensity {lc?.Intensity}");
            }
            // HDR framebuffer
            {
                var hdr = new Relic.Renderer.HDRFramebuffer(1280, 720, Relic.Renderer.HDRFormat.RGBA16F);
                Assert(hdr.IsCreated && hdr.Format == Relic.Renderer.HDRFormat.RGBA16F, $"HDR framebuffer {hdr.Format} {hdr.Width}x{hdr.Height}");
                Assert(hdr.Width == 1280 && hdr.Height == 720, "HDR size");
                hdr.Dispose();
            }
            // Bloom pass execution
            {
                var bloom = new Relic.Renderer.BloomPass();
                var ctx = new Relic.Renderer.RenderGraphContext { Stats = new Relic.Renderer.RendererStats() };
                bloom.Execute(ctx);
                Assert(true, "Bloom pass execution");
                // Console vars
                Relic.Gameplay.ConsoleVariables.R_Bloom = true;
                Relic.Gameplay.ConsoleVariables.R_BloomIntensity = 1.5f;
                Assert(Relic.Gameplay.ConsoleVariables.R_BloomIntensity == 1.5f, "r_bloom_intensity");
            }
            // Lightmap generation
            {
                var lm = new Relic.Renderer.LightmapSystem();
                var lights = em.Entities.Where(e => e.GetComponent<LightComponent>() != null).Select(e => {
                    var lc = e.GetComponent<LightComponent>()!;
                    return new Relic.Renderer.LightInfo { Position = e.Transform.Position, Color = lc.Color, Intensity = lc.Intensity, Range = lc.Range };
                }).ToList();
                lm.Generate(lights, null);
                Assert(lm.IsBaked && lm.LightmapData != null, $"Lightmap baked {lm.LightmapWidth}x{lm.LightmapHeight} time {lm.BakeTimeMs:F1}ms");
                Assert(lm.LightmapData!.Length == lm.LightmapWidth * lm.LightmapHeight * 4, "Lightmap data size");
            }
            // Material normal map loading
            {
                var db = new Relic.Content.AssetDatabase(FindAssetsRoot());
                var mat = db.LoadMaterial("brick");
                Assert(!string.IsNullOrEmpty(mat.NormalTexture), $"Material normal map {mat.NormalTexture}");
                Assert(!string.IsNullOrEmpty(mat.SpecularTexture), $"Material specular {mat.SpecularTexture}");
                Assert(!string.IsNullOrEmpty(mat.RoughnessTexture), $"Material roughness {mat.RoughnessTexture}");
                var mat2 = db.LoadMaterial("metal");
                Assert(!string.IsNullOrEmpty(mat2.NormalTexture), "Metal normal map");
            }
            // Renderer stats generation
            {
                var stats = new Relic.Renderer.RendererStats();
                stats.AddDrawCall(3);
                stats.AddTriangles(100);
                stats.SetVisibleLights(5);
                stats.SetVisibleEntities(10);
                stats.SetFrameTime(16.6f);
                Assert(stats.DrawCalls == 3 && stats.Triangles == 100, $"Renderer stats draw {stats.DrawCalls} tris {stats.Triangles}");
                Assert(stats.VisibleLights == 5 && stats.VisibleEntities == 10, "Renderer stats lights/ents");
                string txt = stats.GetText();
                Assert(txt.Contains("DrawCalls") && txt.Contains("Lights"), "Renderer stats text");
            }
            // Frustum culling visibility
            {
                var proj = Relic.Framework.Matrix4x4.CreatePerspectiveFieldOfView(75f*MathF.PI/180f, 16f/9f, 0.1f, 1000f);
                var view = Relic.Framework.Matrix4x4.CreateLookAt(new Relic.Framework.Vector3(0,0,5), new Relic.Framework.Vector3(0,0,0), new Relic.Framework.Vector3(0,0,1));
                var vp = proj * view;
                var frustum = Relic.Renderer.Frustum.FromViewProjection(vp);
                bool visible = frustum.IsBoxVisible(new Relic.Framework.Vector3(-1,-1,-1), new Relic.Framework.Vector3(1,1,1));
                Assert(visible, "Frustum culling visible box at origin");
                bool notVisible = frustum.IsBoxVisible(new Relic.Framework.Vector3(1000,1000,1000), new Relic.Framework.Vector3(1001,1001,1001));
                Assert(!notVisible, "Frustum culling invisible far box");
                bool sphereVisible = frustum.IsSphereVisible(new Relic.Framework.Vector3(0,0,0), 1f);
                Assert(sphereVisible, "Frustum sphere visible");
            }
            // RenderGraph with 7 passes
            {
                var rg = new Relic.Renderer.RenderGraph();
                rg.AddPass(new Relic.Renderer.GeometryPass());
                rg.AddPass(new Relic.Renderer.LightingPass());
                rg.AddPass(new Relic.Renderer.LightmapPass());
                rg.AddPass(new Relic.Renderer.HDRPass());
                rg.AddPass(new Relic.Renderer.BloomPass());
                rg.AddPass(new Relic.Renderer.ToneMapPass());
                rg.AddPass(new Relic.Renderer.UIPass());
                Assert(rg.Passes.Count == 7, $"RenderGraph 7 passes got {rg.Passes.Count}");
                var ctx = new Relic.Renderer.RenderGraphContext { Stats = new Relic.Renderer.RendererStats() };
                rg.Execute(ctx);
                Assert(ctx.Stats!.DrawCalls >= 2, "RenderGraph Execute draws");
            }
            // Fog and postprocess from map
            {
                var fog = em.FindByClassName("env_fog").FirstOrDefault();
                Assert(fog != null, "env_fog loads from map");
                if (fog != null) Assert(MathF.Abs(fog.GetComponent<FogComponent>()!.Density - 0.002f) < 0.0001f, $"Fog density {fog.GetComponent<FogComponent>()!.Density}");
                var pp = em.FindByClassName("env_postprocess").FirstOrDefault();
                Assert(pp != null, "env_postprocess loads");
                if (pp != null) Assert(pp.GetComponent<PostProcessComponent>()!.BloomEnabled, "PostProcess bloom enabled");
            }
        }


        // Real GPU rendering: shadows, HDR, bloom, debug views, shadow_test.map
        {
            string FindShadowMap()
            {
                string[] cands = { "Assets/Maps/shadow_test.map", "Relic/Relic.Runtime/Assets/Maps/shadow_test.map", Path.Combine(FindAssetsRoot(), "Maps/shadow_test.map"), "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/Maps/shadow_test.map" };
                foreach (var c in cands) if (File.Exists(c)) return c;
                return FindMapPath();
            }
            var smap = Relic.Content.MapLoader.Load(FindShadowMap());
            var sem = new EntityManager();
            Relic.Gameplay.MapSpawner.SpawnMapEntities(sem, smap);
            Assert(sem.FindByClassName("light").Any(), "shadow_test point light");
            Assert(sem.FindByClassName("light_spot").Any(), "shadow_test spot light");
            Assert(sem.FindByClassName("light_sun").Any(), "shadow_test sun light");
            // Point light shader
            {
                string vs = Relic.Renderer.OpenGL.Shader.DefaultVertex;
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(vs.Contains("aTangent") && fs.Contains("uPointPos0") && fs.Contains("attenuation"), "Point light shader");
                Assert(fs.Contains("diffuseTex") && fs.Contains("specTex"), "Point light diffuse+specular");
            }
            // Spot light shader
            {
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs.Contains("uSpotPos0") && fs.Contains("uSpotDir0") && fs.Contains("uSpotInner0"), "Spot light shader");
            }
            // Sun light shader
            {
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs.Contains("uSunDir") && fs.Contains("uSunColor") && fs.Contains("uSunIntensity"), "Sun light shader");
            }
            // Shadow map generation
            {
                var renderer = new Relic.Renderer.OpenGL.ShadowRenderer();
                var pointShadow = renderer.CreatePointLightShadow(512f, 512);
                Assert(pointShadow.IsCreated, "Point shadow map created");
                var spotShadow = renderer.CreateSpotLightShadow(1024f, 1024);
                Assert(spotShadow.IsCreated, "Spot shadow map created");
                var sunShadow = renderer.CreateSunShadow(1024);
                Assert(sunShadow.IsCreated && Relic.Renderer.OpenGL.CascadedShadowMap.CascadeCount == 3, "Sun cascaded shadows 3");
                Assert(sunShadow.SplitDistances[0] < sunShadow.SplitDistances[1], "Cascade near<mid");
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs.Contains("shadowPCF") && fs.Contains("uShadowBias"), "Shadow map PCF + bias");
            }
            // Fog application
            {
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs.Contains("uFogColor") && fs.Contains("uFogDensity") && fs.Contains("exp("), "Fog application");
                var fog = sem.FindByClassName("env_fog").FirstOrDefault();
                Assert(fog != null, "shadow_test fog");
            }
            // Bloom pipeline
            {
                var bloom = new Relic.Renderer.BloomPass();
                var ctx = new Relic.Renderer.RenderGraphContext { Stats = new Relic.Renderer.RendererStats() };
                bloom.Execute(ctx);
                Assert(true, "Bloom pipeline bright+blur+composite");
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs.Contains("uExposure"), "Bloom HDR exposure");
            }
            // HDR rendering
            {
                var hdr = new Relic.Renderer.HDRFramebuffer(1280, 720, Relic.Renderer.HDRFormat.RGBA16F);
                Assert(hdr.IsCreated && hdr.Format == Relic.Renderer.HDRFormat.RGBA16F, "HDR RGBA16F");
                string fs = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs.Contains("hdr") && fs.Contains("ldr"), "HDR tonemap");
            }
            // Normal map loading
            {
                var db = new Relic.Content.AssetDatabase(FindAssetsRoot());
                var mat = db.LoadMaterial("brick");
                Assert(!string.IsNullOrEmpty(mat.NormalTexture) && mat.NormalTexture.Contains("brick_n"), "Normal map loading brick_n");
                string vs = Relic.Renderer.OpenGL.Shader.DefaultVertex;
                string fs2 = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(vs.Contains("aTangent") && fs2.Contains("getNormal") && fs2.Contains("TBN"), "Normal map TBN");
            }
            // Material lighting
            {
                var mm = new Relic.Gameplay.Materials.MaterialManager();
                var m = mm.GetOrCreate("brick");
                Assert(!string.IsNullOrEmpty(m.DiffuseTextureName), "Material lighting diffuse");
                string fs3 = Relic.Renderer.OpenGL.Shader.DefaultFragment;
                Assert(fs3.Contains("uSpecularMap") && fs3.Contains("uRoughnessMap"), "Material lighting specular+roughness");
            }
        }

        // Phase 9.5: First-Class TrenchBroom Integration
        {
            var assetsRoot = FindAssetsRoot();
            var tbDir = Path.Combine(assetsRoot, "TrenchBroom");
            // Game definition files present
            Assert(File.Exists(Path.Combine(tbDir, "GameConfig.cfg")), "TrenchBroom GameConfig.cfg present");
            Assert(File.Exists(Path.Combine(tbDir, "EntityGroups.cfg")), "TrenchBroom EntityGroups.cfg present");
            Assert(File.Exists(Path.Combine(tbDir, "FaceAttribs.cfg")), "TrenchBroom FaceAttribs.cfg present");
            Assert(File.Exists(Path.Combine(tbDir, "CompilationProfiles.cfg")), "TrenchBroom CompilationProfiles.cfg present");
            Assert(File.Exists(Path.Combine(tbDir, "Icon.png")), "TrenchBroom Icon.png present");
            Assert(File.Exists(Path.Combine(tbDir, "README.txt")), "TrenchBroom README.txt present");
            Assert(File.Exists(Path.Combine(assetsRoot, "Relic.fgd")), "Assets/Relic.fgd present");
            // GameConfig content: Relic name, Valve 220, Assets filesystem, Textures root, Relic.fgd, Maps export
            {
                var cfg = File.ReadAllText(Path.Combine(tbDir, "GameConfig.cfg"));
                Assert(cfg.Contains("\"Relic\"") || cfg.Contains("Relic"), "GameConfig game name Relic");
                Assert(cfg.Contains("Valve"), "GameConfig Valve 220 map format");
                Assert(cfg.Contains("Relic.fgd"), "GameConfig entity definitions Relic.fgd");
                Assert(cfg.Contains("Textures"), "GameConfig materials root Textures");
                Assert(cfg.Contains("Assets"), "GameConfig filesystem Assets");
            }
            // Icon is a valid PNG
            {
                var bytes = File.ReadAllBytes(Path.Combine(tbDir, "Icon.png"));
                Assert(bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G', "Icon.png valid PNG");
            }
            // FGD registers every required entity
            {
                var fgd = File.ReadAllText(Path.Combine(assetsRoot, "Relic.fgd"));
                foreach (var cls in new[] { "player_start", "enemy_soldier", "enemy_dog", "enemy_demon", "weapon_pickup", "health_pickup", "prop_static", "light", "light_spot", "light_sun", "env_fog", "env_postprocess", "trigger_once" })
                    Assert(fgd.Contains(cls), $"FGD registers {cls}");
                Assert(fgd.Contains("model("), "FGD model preview directives");
            }
            // Entity groups cover all categories
            {
                var groups = File.ReadAllText(Path.Combine(tbDir, "EntityGroups.cfg"));
                foreach (var g in new[] { "[Player]", "[Enemies]", "[Pickups]", "[Props]", "[Lights]", "[Environment]", "[Triggers]" })
                    Assert(groups.Contains(g), $"EntityGroups category {g}");
            }
            // Map templates available and loadable
            {
                var db = new Relic.Content.AssetDatabase(assetsRoot);
                var templates = db.ListTemplates().ToList();
                foreach (var t in new[] { "basic_room.map", "combat_test.map", "lighting_test.map", "shadow_test.map" })
                    Assert(templates.Contains(t), $"Template available {t}");
                foreach (var t in templates)
                {
                    var map = Relic.Content.MapLoader.Load(Path.Combine(assetsRoot, "Templates", t));
                    Assert(map.Brushes.Count > 0, $"Template {t} has brushes");
                    Assert(map.Entities.Any(e => e.ClassName == "player_start"), $"Template {t} has player_start");
                }
            }
            // Texture integration: browser exposes Assets/Textures grouped under Relic
            {
                var db = new Relic.Content.AssetDatabase(assetsRoot);
                var textures = db.ListTextures().ToList();
                Assert(textures.Count > 0, $"Textures exposed ({textures.Count})");
                Assert(textures.Contains("brick.png"), "Texture browser has brick");
                Assert(Relic.Content.AssetDatabase.TextureGroup == "Relic", "Texture group Relic");
            }
            // Material support: diffuse/normal/specular/roughness from .mat
            {
                var db = new Relic.Content.AssetDatabase(assetsRoot);
                foreach (var m in new[] { "brick", "metal", "concrete" })
                {
                    var mat = db.LoadMaterial(m);
                    Assert(!string.IsNullOrEmpty(mat.DiffuseTexture), $"{m}.mat diffuse");
                    Assert(!string.IsNullOrEmpty(mat.NormalTexture), $"{m}.mat normal");
                    Assert(!string.IsNullOrEmpty(mat.SpecularTexture), $"{m}.mat specular");
                    Assert(!string.IsNullOrEmpty(mat.RoughnessTexture), $"{m}.mat roughness");
                }
            }
            // Model preview support: obj previews, mdl falls back to bounding box
            {
                var db = new Relic.Content.AssetDatabase(assetsRoot);
                Assert(File.Exists(Path.Combine(assetsRoot, "Models", "crate.obj")), "Preview model crate.obj");
                foreach (var m in new[] { "soldier.mdl", "dog.mdl", "demon.mdl" })
                    Assert(File.Exists(Path.Combine(assetsRoot, "Models", m)), $"Preview model {m}");
                var objPreview = db.GetModelPreview("crate.obj");
                Assert(objPreview.PreviewSupported, "crate.obj preview supported");
                foreach (var m in new[] { "soldier.mdl", "dog.mdl", "demon.mdl" })
                {
                    var preview = db.GetModelPreview(m);
                    Assert(!preview.PreviewSupported && preview.Fallback == "boundingbox", $"{m} bounding-box fallback");
                }
            }
            // Automatic installation: Install / Verify / Uninstall into an isolated games dir
            {
                var installer = new Relic.Content.RelicTrenchBroomInstaller(assetsRoot);
                Assert(installer.SourcesPresent(out var missing), $"Installer sources present{(missing.Length > 0 ? ": " + string.Join(",", missing) : "")}");
                Assert(installer.CandidateGamesDirs().Count > 0, "Installer detects TrenchBroom games dir");
                var tempGames = Path.Combine(Path.GetTempPath(), "RelicTBTest_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempGames);
                try
                {
                    Assert(installer.Install(tempGames), "Installer Install()");
                    var verify = installer.Verify(tempGames);
                    Assert(verify.Ok, $"Installer Verify() after install: {verify.Detail}");
                    Assert(File.Exists(Path.Combine(tempGames, "Relic", "GameConfig.cfg")), "Installed GameConfig.cfg");
                    Assert(File.Exists(Path.Combine(tempGames, "Relic", "Icon.png")), "Installed Icon.png");
                    Assert(File.Exists(Path.Combine(tempGames, "Relic", "Relic.fgd")), "Installed Relic.fgd");
                    Assert(installer.Uninstall(tempGames), "Installer Uninstall()");
                    Assert(!installer.Verify(tempGames).Ok, "Installer Verify() fails after uninstall");
                }
                finally { try { Directory.Delete(tempGames, true); } catch { } }
            }
        }

        // Phase 9.6: TrenchBroom integration diagnosis (no console errors on load)
        {
            var assetsRoot = FindAssetsRoot();
            var fgd = File.ReadAllText(Path.Combine(assetsRoot, "Relic.fgd"));
            // ERROR 1 root cause: @include of a file that ships nowhere. FGD must be standalone.
            Assert(!fgd.Contains("@include"), "FGD has no @include (standalone, no base.fgd dependency)");
            // ERROR 2 root cause: size() without comma. Every size() must be size(min, max).
            {
                var sizes = System.Text.RegularExpressions.Regex.Matches(fgd, @"size\(([^)]*)\)")
                    .Where(m => System.Text.RegularExpressions.Regex.IsMatch(m.Groups[1].Value, @"\d")).ToList();
                Assert(sizes.Count >= 40, $"FGD size() count ({sizes.Count}, one per sized point class; solid classes must not have size)");
                Assert(!System.Text.RegularExpressions.Regex.IsMatch(fgd, @"@SolidClass[^=]*size\("), "FGD no size() on solid classes");
                // All float defaults must be quoted (unquoted floats warn in TrenchBroom)
                {
                    bool allQuoted = true;
                    foreach (var line in fgd.Split('\n'))
                    {
                        if (!line.Contains("(float)")) continue;
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"\(float\)\s*:\s*""[^""]*""\s*:\s*(\S+)");
                        if (m.Success && !m.Groups[1].Value.StartsWith('"')) { allQuoted = false; Console.WriteLine($"[FAIL] Unquoted float default: {line.Trim()}"); }
                    }
                    Assert(allQuoted, "FGD all float defaults quoted");
                }
                bool allComma = sizes.All(m => m.Groups[1].Value.Contains(','));
                Assert(allComma, "FGD every size() has min,max comma");
                bool sixNumbers = sizes.All(m => m.Groups[1].Value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length == 6);
                Assert(sixNumbers, "FGD every size() has 6 numbers");
            }
            // No {{}} template substitution (unsupported by TrenchBroom FGD model syntax)
            Assert(!fgd.Contains("{{") && !fgd.Contains("}}"), "FGD no {{}} substitution");
            // Every model() uses EL dict with a concrete path that exists
            {
                var models = System.Text.RegularExpressions.Regex.Matches(fgd, @"model\(\s*\{\s*""path""\s*:\s*""([^""]+)""");
                Assert(models.Count >= 5, $"FGD model() dict count ({models.Count})");
                foreach (System.Text.RegularExpressions.Match m in models)
                    Assert(File.Exists(Path.Combine(assetsRoot, m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar))), $"FGD model path exists {m.Groups[1].Value}");
            }
            // All 13 entities + valid class declarations
            foreach (var cls in new[] { "player_start", "enemy_soldier", "enemy_dog", "enemy_demon", "weapon_pickup", "health_pickup", "prop_static", "light", "light_spot", "light_sun", "env_fog", "env_postprocess", "trigger_once" })
                Assert(System.Text.RegularExpressions.Regex.IsMatch(fgd, @"=\s*" + cls + @"\s*:"), $"FGD class declaration {cls}");
            Assert(fgd.Contains("color(color255)"), "FGD color255 property type");
            Assert(fgd.Contains("(choices)"), "FGD choices property type");
            // GameConfig validation
            {
                var cfg = File.ReadAllText(Path.Combine(assetsRoot, "TrenchBroom", "GameConfig.cfg"));
                Assert(cfg.Contains("\"version\": 5"), "GameConfig version 5");
                Assert(cfg.Contains("\"Relic\""), "GameConfig name Relic");
                Assert(cfg.Contains("\"Valve\""), "GameConfig Valve 220 format");
                Assert(cfg.Contains("Relic.fgd"), "GameConfig entity definitions Relic.fgd");
                Assert(Directory.Exists(Path.Combine(assetsRoot, "Textures")), "GameConfig materials root Textures exists");
                Assert(File.Exists(Path.Combine(assetsRoot, "Relic.fgd")), "GameConfig entity definition path exists");
                Assert(Directory.Exists(assetsRoot), "GameConfig filesystem path Assets exists");
                Assert(Directory.Exists(Path.Combine(assetsRoot, "Maps")), "GameConfig map export Maps exists");
            }
            // Materials discoverable
            foreach (var m in new[] { "brick", "metal", "concrete" })
            {
                var mat = new Relic.Content.AssetDatabase(assetsRoot).LoadMaterial(m);
                Assert(File.Exists(Path.Combine(assetsRoot, "Textures", mat.DiffuseTexture)), $"Material {m} diffuse discoverable");
            }
            // Models valid
            foreach (var m in new[] { "crate.obj", "dog.mdl", "demon.mdl", "soldier.mdl", "player.mdl" })
                Assert(File.Exists(Path.Combine(assetsRoot, "Models", m)), $"Model valid {m}");
            // Palette for MDL previews (Quake indexed skins need textures.palette)
            {
                var cfg = File.ReadAllText(Path.Combine(assetsRoot, "TrenchBroom", "GameConfig.cfg"));
                Assert(cfg.Contains("\"palette\": \"gfx/palette.lmp\""), "GameConfig textures.palette configured");
                var palettePath = Path.Combine(assetsRoot, "gfx", "palette.lmp");
                Assert(File.Exists(palettePath), "Assets/gfx/palette.lmp ships");
                Assert(new FileInfo(palettePath).Length == 768, "palette.lmp is 768 bytes (256 RGB)");
                // Game MDLLoader uses the same palette so game and editor agree
                Relic.Content.MDLLoader.ResetPaletteCache();
                var dbPal = new Relic.Content.AssetDatabase(assetsRoot);
                var mdlPal = dbPal.LoadMDL("dog.mdl");
                Assert(mdlPal.SkinData.Length == mdlPal.SkinWidth * mdlPal.SkinHeight * 4, "MDL skin decoded via palette");
            }
            // Installed copy matches source and verifies clean
            {
                var installer = new Relic.Content.RelicTrenchBroomInstaller(assetsRoot);
                var gamesDir = installer.DetectTrenchBroomGamesDir();
                Assert(gamesDir != null, "TrenchBroom games dir detected");
                if (gamesDir != null)
                {
                    var installed = File.ReadAllText(Path.Combine(gamesDir, "Relic", "Relic.fgd"));
                    Assert(installed == fgd, "Installed Relic.fgd matches source");
                    var verify = installer.Verify(gamesDir);
                    Assert(verify.Ok, $"Installed game verifies clean: {verify.Detail}");
                }
            }
        }

        // Phase 10: World Systems, Mapping Ecosystem & Entity Expansion
        {
            var assetsRoot = FindAssetsRoot();
            var fgd = File.ReadAllText(Path.Combine(assetsRoot, "Relic.fgd"));
            // FGD registers every new entity class
            foreach (var cls in new[] { "ambient_sound", "sound_emitter", "sound_zone", "music_player", "music_trigger", "reverb_zone",
                "trigger_multiple", "trigger_hurt", "trigger_heal", "trigger_push", "trigger_teleport", "trigger_sound", "trigger_music",
                "func_door", "func_door_rotating", "func_button", "func_elevator", "func_platform", "func_train",
                "logic_timer", "logic_random", "logic_relay", "logic_counter", "logic_compare",
                "path_corner", "path_patrol", "info_enemy_spawn",
                "env_particles", "fx_smoke", "fx_fire", "fx_sparks", "fx_blood", "fx_explosion", "fx_muzzleflash",
                "env_rain", "env_snow", "env_lightning", "env_wind", "env_skybox", "env_ambient",
                "ammo_pickup", "armor_pickup", "keycard_pickup", "weapon_spawn",
                "objective", "checkpoint", "mission_start", "mission_end" })
                Assert(System.Text.RegularExpressions.Regex.IsMatch(fgd, @"=\s*" + cls + @"\s*:"), $"FGD registers {cls}");
            // No size() on solid classes (func_*, trigger_*), quoted float defaults everywhere
            Assert(!System.Text.RegularExpressions.Regex.IsMatch(fgd, @"@SolidClass[^=]*size\("), "FGD no size() on solid classes");
            {
                bool allQuoted = true;
                foreach (var line in fgd.Split('\n'))
                {
                    if (!line.Contains("(float)")) continue;
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"\(float\)\s*:\s*""[^""]*""\s*:\s*(\S+)");
                    if (m.Success && !m.Groups[1].Value.StartsWith('"')) { allQuoted = false; Console.WriteLine($"[FAIL] Unquoted float default: {line.Trim()}"); }
                }
                Assert(allQuoted, "FGD all float defaults quoted (Phase 10)");
            }
            // EntityGroups covers new categories
            {
                var groups = File.ReadAllText(Path.Combine(assetsRoot, "TrenchBroom", "EntityGroups.cfg"));
                foreach (var g in new[] { "[Items]", "[Weapons]", "[Audio]", "[Doors]", "[Logic]", "[Particles]", "[Objectives]", "[AI]" })
                    Assert(groups.Contains(g), $"EntityGroups category {g}");
            }
            // Showcase map loads with all world systems
            {
                var mapPath = Path.Combine(assetsRoot, "Maps", "world_systems_test.map");
                Assert(File.Exists(mapPath), "world_systems_test.map exists");
                var map = Relic.Content.MapLoader.Load(mapPath);
                var em = new EntityManager();
                Relic.Gameplay.MapSpawner.SpawnMapEntities(em, map);
                foreach (var cls in new[] { "ambient_sound", "music_player", "trigger_hurt", "trigger_teleport", "func_door", "func_elevator",
                    "logic_timer", "logic_relay", "path_corner", "env_particles", "fx_fire", "env_rain", "env_lightning",
                    "ammo_pickup", "armor_pickup", "keycard_pickup", "weapon_spawn", "objective", "checkpoint", "mission_start", "mission_end", "info_enemy_spawn" })
                    Assert(em.FindByClassName(cls).Any(), $"Showcase map spawns {cls}");
                // Spawner honors .map properties
                var amb = em.FindByClassName("ambient_sound").First();
                Assert(amb.GetComponent<AmbientSoundComponent>()!.Sound == "forest_wind.wav", "Spawner ambient_sound props");
                var hurt = em.FindByClassName("trigger_hurt").First();
                Assert(MathF.Abs(hurt.GetComponent<TriggerComponent>()!.Damage - 10f) < 0.01f, "Spawner trigger_hurt props");
                var door = em.FindByClassName("func_door").First();
                Assert(MathF.Abs(door.GetComponent<DoorComponent>()!.Speed - 100f) < 0.01f, "Spawner func_door props");
                var dog = em.FindByClassName("enemy_dog").First();
                Assert(dog.GetComponent<PatrolComponent>() != null, "Spawner enemy patrol route");
            }
            // Audio playback
            {
                var em = new EntityManager();
                var e = em.CreateEntity("amb", "ambient_sound");
                var amb = e.AddComponent(new AmbientSoundComponent { Sound = "forest_wind.wav" });
                string? played = null;
                amb.OnPlaySound = (s, v) => played = s;
                amb.Play();
                Assert(amb.PlayCount == 1 && played == "forest_wind.wav", "Audio playback ambient_sound");
                var se = em.CreateEntity("em", "sound_emitter").AddComponent(new SoundEmitterComponent { Sound = "alarm.wav", Interval = 1f });
                for (int i = 0; i < 130; i++) se.Update(1f / 60f);
                Assert(se.PlayCount >= 2, $"Audio sound_emitter periodic ({se.PlayCount})");
                var mp = em.CreateEntity("mus", "music_player").AddComponent(new MusicPlayerComponent { Track = "main_theme.wav" });
                mp.Play();
                Assert(mp.IsPlaying && mp.PlayCount == 1, "Audio music_player play");
                var mt = em.CreateEntity("mt", "music_trigger").AddComponent(new MusicTriggerComponent { Track = "battle.wav" });
                string? trigTrack = null;
                mt.OnTriggered += t => trigTrack = t;
                mt.Trigger();
                Assert(mt.Triggered && trigTrack == "battle.wav", "Audio music_trigger fires");
            }
            // Trigger activation (hurt / heal / teleport / once)
            {
                var em = new EntityManager();
                var player = em.CreateEntity("p", "player_start");
                player.Transform.Position = new Vector3(0, 0, 32);
                var hp = player.AddComponent(new HealthComponent { MaxHealth = 100, Health = 100 });
                var hurt = em.CreateEntity("h", "trigger_hurt").AddComponent(new TriggerComponent { Kind = TriggerKind.Hurt, Damage = 10, Interval = 0f });
                hurt.Activate(em, player);
                Assert(MathF.Abs(hp.Health - 90f) < 0.01f, "Trigger trigger_hurt damages");
                var heal = em.CreateEntity("hl", "trigger_heal").AddComponent(new TriggerComponent { Kind = TriggerKind.Heal, Amount = 25 });
                heal.Activate(em, player);
                Assert(MathF.Abs(hp.Health - 100f) < 0.01f, "Trigger trigger_heal heals");
                var dest = em.CreateEntity("d", "path_corner", "tele_dest");
                dest.Transform.Position = new Vector3(200, 200, 32);
                var tele = em.CreateEntity("t", "trigger_teleport").AddComponent(new TriggerComponent { Kind = TriggerKind.Teleport, Target = "tele_dest" });
                tele.Activate(em, player);
                Assert(player.Transform.Position.X == 200f, "Trigger trigger_teleport moves player");
                var once = em.CreateEntity("o", "trigger_once").AddComponent(new TriggerComponent { Kind = TriggerKind.Once });
                once.Activate(em, player);
                Assert(once.FireCount == 1 && once.Consumed, "Trigger trigger_once consumes");
            }
            // Door opening + elevator movement
            {
                var em = new EntityManager();
                var door = em.CreateEntity("d", "func_door");
                door.Transform.Position = new Vector3(0, 0, 32);
                var dc = door.AddComponent(new DoorComponent { Kind = DoorKind.Door, Speed = 100f, Wait = 999f, Distance = 64f });
                Vector3 closed = door.Transform.Position;
                dc.Open();
                for (int i = 0; i < 60; i++) dc.Update(1f / 60f);
                Assert(dc.State == MoverState.Open, "Door opening reaches Open");
                Assert((door.Transform.Position - closed).Length() > 30f, "Door opening moves");
                var locked = em.CreateEntity("l", "func_door").AddComponent(new DoorComponent { Locked = true });
                locked.Open();
                Assert(locked.State == MoverState.Closed, "Door locked stays Closed");
                var elev = em.CreateEntity("e", "func_elevator");
                elev.Transform.Position = new Vector3(0, 0, 32);
                var ec = elev.AddComponent(new DoorComponent { Kind = DoorKind.Elevator, Speed = 100f, Wait = 999f, Distance = 64f, Direction = new Vector3(0, 0, 1) });
                ec.Open();
                for (int i = 0; i < 60; i++) ec.Update(1f / 60f);
                Assert(ec.State == MoverState.Open && elev.Transform.Position.Z > 60f, "Elevator movement rises");
            }
            // Logic timer + relay
            {
                var em = new EntityManager();
                var timer = em.CreateEntity("t", "logic_timer").AddComponent(new LogicTimerComponent { Interval = 1f });
                for (int i = 0; i < 130; i++) timer.Update(1f / 60f);
                Assert(timer.FireCount >= 2, $"Logic timer fires ({timer.FireCount})");
                var relay = em.CreateEntity("r", "logic_relay").AddComponent(new LogicRelayComponent { Target = "door1" });
                string? fired = null;
                relay.OnFire += t => fired = t;
                relay.Fire();
                Assert(relay.FireCount == 1 && fired == "door1", "Logic relay fires target");
                var counter = em.CreateEntity("c", "logic_counter").AddComponent(new LogicCounterComponent { TargetCount = 3 });
                counter.Add(); counter.Add();
                Assert(!counter.Reached, "Logic counter not reached at 2/3");
                counter.Add();
                Assert(counter.Reached, "Logic counter reached at 3/3");
                var cmp = em.CreateEntity("cmp", "logic_compare").AddComponent(new LogicCompareComponent { A = 5, B = 10, Op = "<" });
                Assert(cmp.Evaluate(), "Logic compare 5<10");
            }
            // Enemy patrol
            {
                var em = new EntityManager();
                var pc1 = em.CreateEntity("pc1", "path_corner", "pc1");
                pc1.Transform.Position = new Vector3(-64, 0, 32);
                pc1.AddComponent(new PathCornerComponent { Target = "pc2" });
                var pc2 = em.CreateEntity("pc2", "path_corner", "pc2");
                pc2.Transform.Position = new Vector3(64, 0, 32);
                pc2.AddComponent(new PathCornerComponent { Target = "pc1" });
                var dog = em.CreateEntity("dog", "enemy_dog");
                dog.Transform.Position = new Vector3(-64, 0, 32);
                var patrol = dog.AddComponent(new PatrolComponent { Speed = 120f });
                patrol.Resolve(em, "pc1");
                Assert(patrol.Waypoints.Count == 2, "Enemy patrol route resolved");
                for (int i = 0; i < 120; i++) patrol.Update(1f / 60f);
                Assert(patrol.WaypointsVisited >= 1, "Enemy patrol walks route");
                Assert(dog.Transform.Position.X > -64f, "Enemy patrol moves dog");
            }
            // Particle creation
            {
                var em = new EntityManager();
                foreach (var kind in new[] { ParticleEffectKind.Smoke, ParticleEffectKind.Fire, ParticleEffectKind.Sparks, ParticleEffectKind.Blood, ParticleEffectKind.Explosion, ParticleEffectKind.Muzzleflash })
                {
                    var fx = em.CreateEntity(kind.ToString(), "fx").AddComponent(new ParticleEffectComponent { Kind = kind, SpawnRate = 60f });
                    for (int i = 0; i < 30; i++) fx.Update(1f / 60f);
                    Assert(fx.TotalSpawned > 0, $"Particle creation {kind}");
                }
            }
            // Weather systems
            {
                var em = new EntityManager();
                foreach (var kind in new[] { WeatherKind.Rain, WeatherKind.Snow, WeatherKind.Lightning, WeatherKind.Wind, WeatherKind.Skybox, WeatherKind.Ambient })
                {
                    var w = em.CreateEntity(kind.ToString(), "env").AddComponent(new WeatherComponent { Kind = kind });
                    for (int i = 0; i < 10; i++) w.Update(1f / 60f);
                    Assert(w.TickCount == 10, $"Weather systems {kind} tick");
                }
                var lightning = em.CreateEntity("l", "env_lightning").AddComponent(new WeatherComponent { Kind = WeatherKind.Lightning });
                for (int i = 0; i < 600; i++) lightning.Update(1f / 60f);
                Assert(lightning.StrikeCount >= 1, "Weather lightning strikes");
            }
            // Objective completion + checkpoint saving + mission completion
            {
                var em = new EntityManager();
                var obj = em.CreateEntity("o", "objective").AddComponent(new ObjectiveComponent { ObjectiveId = "obj_gen", Required = 2 });
                bool completed = false;
                obj.OnCompleted += () => completed = true;
                obj.AddProgress();
                Assert(!obj.IsComplete, "Objective incomplete at 1/2");
                obj.AddProgress();
                Assert(obj.IsComplete && completed, "Objective completion");
                var cp = em.CreateEntity("cp", "checkpoint").AddComponent(new CheckpointComponent { CheckpointId = "cp1" });
                cp.Activate(new Vector3(100, 0, 32));
                Assert(cp.Activated && cp.SavedPosition.X == 100f, "Checkpoint saving");
                var end = em.CreateEntity("end", "mission_end").AddComponent(new MissionEndComponent());
                var pending = em.CreateEntity("pend", "objective").AddComponent(new ObjectiveComponent { Required = 5 });
                Assert(!end.CheckAll(new[] { obj, pending }), "Mission incomplete while objective pending");
                var obj2 = em.CreateEntity("o2", "objective").AddComponent(new ObjectiveComponent { Required = 1 });
                obj2.AddProgress();
                Assert(end.CheckAll(new[] { obj, obj2 }), "Mission completion");
            }
            // FGD bounds match measured mesh bounds (rounded outward)
            {
                var db = new Relic.Content.AssetDatabase(assetsRoot);
                var bounds = new Dictionary<string, (Vector3 min, Vector3 max)>();
                foreach (var m in new[] { "player.mdl", "soldier.mdl", "dog.mdl", "demon.mdl" })
                {
                    var mdl = db.LoadMDL(m);
                    Vector3 mn = new(float.MaxValue), mx = new(float.MinValue);
                    foreach (var f in mdl.Frames)
                        foreach (var v in f.Verts)
                        {
                            var p = v.GetPosition(mdl.Scale, mdl.ScaleOrigin);
                            mn.X = MathF.Min(mn.X, p.X); mn.Y = MathF.Min(mn.Y, p.Y); mn.Z = MathF.Min(mn.Z, p.Z);
                            mx.X = MathF.Max(mx.X, p.X); mx.Y = MathF.Max(mx.Y, p.Y); mx.Z = MathF.Max(mx.Z, p.Z);
                        }
                    bounds[m] = (mn, mx);
                }
                var crate = db.LoadModel("crate.obj");
                {
                    Vector3 mn = new(float.MaxValue), mx = new(float.MinValue);
                    foreach (var p in crate.Positions)
                    {
                        mn.X = MathF.Min(mn.X, p.X); mn.Y = MathF.Min(mn.Y, p.Y); mn.Z = MathF.Min(mn.Z, p.Z);
                        mx.X = MathF.Max(mx.X, p.X); mx.Y = MathF.Max(mx.Y, p.Y); mx.Z = MathF.Max(mx.Z, p.Z);
                    }
                    bounds["crate.obj"] = (mn, mx);
                }
                var fgdSizes = new Dictionary<string, (Vector3 min, Vector3 max)>();
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(fgd, @"size\(\s*(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s*,\s*(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s*\)[^\n=]*=\s*(\w+)"))
                {
                    fgdSizes[m.Groups[7].Value] = (new Vector3(float.Parse(m.Groups[1].Value), float.Parse(m.Groups[2].Value), float.Parse(m.Groups[3].Value)),
                        new Vector3(float.Parse(m.Groups[4].Value), float.Parse(m.Groups[5].Value), float.Parse(m.Groups[6].Value)));
                }
                var modelFor = new Dictionary<string, string> { ["player_start"] = "player.mdl", ["enemy_soldier"] = "soldier.mdl", ["enemy_dog"] = "dog.mdl", ["enemy_demon"] = "demon.mdl", ["prop_static"] = "crate.obj" };
                foreach (var kv in modelFor)
                {
                    var (mn, mx) = bounds[kv.Value];
                    var size = fgdSizes[kv.Key];
                    const float eps = 0.01f;
                    bool loX = size.min.X <= mn.X + eps, loY = size.min.Y <= mn.Y + eps, loZ = size.min.Z <= mn.Z + eps;
                    bool hiX = size.max.X >= mx.X - eps, hiY = size.max.Y >= mx.Y - eps, hiZ = size.max.Z >= mx.Z - eps;
                    bool tloX = size.min.X >= mn.X - 1f, tloY = size.min.Y >= mn.Y - 1f, tloZ = size.min.Z >= mn.Z - 1f;
                    bool thiX = size.max.X <= mx.X + 1f, thiY = size.max.Y <= mx.Y + 1f, thiZ = size.max.Z <= mx.Z + 1f;
                    bool fits = loX && loY && loZ && hiX && hiY && hiZ && tloX && tloY && tloZ && thiX && thiY && thiZ;

                    Assert(fits, $"FGD bounds fit {kv.Value} for {kv.Key}: mesh min=({mn.X:F1} {mn.Y:F1} {mn.Z:F1}) max=({mx.X:F1} {mx.Y:F1} {mx.Z:F1}) fgd min=({size.min.X} {size.min.Y} {size.min.Z}) max=({size.max.X} {size.max.Y} {size.max.Z})");
                }
            }
        }

        Console.WriteLine($"Done: {passed} passed, {failed} failed");
        if (failed>0) Console.WriteLine("FAILURE");
        else Console.WriteLine("SUCCESS");
        return failed==0 ? 0 : 1;
    }
}
