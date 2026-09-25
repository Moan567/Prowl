namespace Relic.Content;

// Installs the Relic game definition into TrenchBroom so Relic appears
// in TrenchBroom -> Select Game beside Quake, Quake 2, Half-Life, Hexen 2.
// No Generic mode, no manual configuration, no post-install file copying:
//
//   var installer = new RelicTrenchBroomInstaller();
//   installer.Install();  // detects TrenchBroom, copies 3 files
//   installer.Verify();   // checks the installed game definition
//
// Installed layout (<TrenchBroom>/games/Relic/):
//   GameConfig.cfg   (from Assets/TrenchBroom/GameConfig.cfg)
//   Icon.png         (from Assets/TrenchBroom/Icon.png)
//   Relic.fgd        (from Assets/Relic.fgd)
public sealed class RelicTrenchBroomInstaller
{
    public const string GameName = "Relic";

    private static readonly string[] RequiredFiles = { "GameConfig.cfg", "Icon.png", "Relic.fgd" };

    private readonly string _assetsRoot;

    public RelicTrenchBroomInstaller(string? assetsRoot = null)
    {
        _assetsRoot = assetsRoot ?? FindAssetsRoot();
    }

    public string AssetsRoot => _assetsRoot;

    // All required source files (absolute paths) for the game definition.
    public IReadOnlyDictionary<string, string> SourceFiles()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GameConfig.cfg"] = Path.Combine(_assetsRoot, "TrenchBroom", "GameConfig.cfg"),
            ["Icon.png"] = Path.Combine(_assetsRoot, "TrenchBroom", "Icon.png"),
            ["Relic.fgd"] = Path.Combine(_assetsRoot, "Relic.fgd"),
        };
        return map;
    }

    public bool SourcesPresent(out string[] missing)
    {
        var list = new List<string>();
        foreach (var kv in SourceFiles())
            if (!File.Exists(kv.Value)) list.Add(kv.Value);
        missing = list.ToArray();
        return missing.Length == 0;
    }

    // Candidate <TrenchBroom>/games directories, highest priority first.
    public IReadOnlyList<string> CandidateGamesDirs()
    {
        var candidates = new List<string>();

        // 1. Explicit override: RELIC_TRENCHBROOM_DIR (TrenchBroom root or games dir)
        var env = Environment.GetEnvironmentVariable("RELIC_TRENCHBROOM_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (env.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   .EndsWith("games", StringComparison.OrdinalIgnoreCase))
                candidates.Add(env);
            else
                candidates.Add(Path.Combine(env, "games"));
        }

        // 2. Bundled TrenchBroom next to the repo (RELIC_engine/TrenchBroom-*/games)
        try
        {
            var repoRoot = Directory.GetParent(Path.GetFullPath(_assetsRoot))?.FullName;
            if (repoRoot != null && Directory.Exists(repoRoot))
            {
                foreach (var dir in Directory.GetDirectories(repoRoot, "TrenchBroom-*"))
                {
                    var games = Path.Combine(dir, "games");
                    if (Directory.Exists(games)) candidates.Add(games);
                }
            }
        }
        catch { }

        // 3. Standard installs
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var root in new[] { programFiles, programFilesX86, localAppData, appData })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            candidates.Add(Path.Combine(root, "TrenchBroom", "games"));
        }

        // 4. Steam install
        if (!string.IsNullOrWhiteSpace(programFilesX86))
            candidates.Add(Path.Combine(programFilesX86, "Steam", "steamapps", "common", "TrenchBroom", "games"));

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // First candidate that exists on disk, or null if TrenchBroom is not found.
    public string? DetectTrenchBroomGamesDir()
    {
        foreach (var dir in CandidateGamesDirs())
        {
            try { if (Directory.Exists(dir)) return dir; }
            catch { }
        }
        return null;
    }

    public string TargetDir(string gamesDir) => Path.Combine(gamesDir, GameName);

    public bool Install(string? gamesDir = null)
    {
        gamesDir ??= DetectTrenchBroomGamesDir();
        if (gamesDir == null || !SourcesPresent(out _)) return false;
        try
        {
            var target = TargetDir(gamesDir);
            Directory.CreateDirectory(target);
            foreach (var kv in SourceFiles())
                File.Copy(kv.Value, Path.Combine(target, kv.Key), overwrite: true);
            return Verify(gamesDir).Ok;
        }
        catch { return false; }
    }

    public bool Uninstall(string? gamesDir = null)
    {
        gamesDir ??= DetectTrenchBroomGamesDir();
        if (gamesDir == null) return false;
        try
        {
            var target = TargetDir(gamesDir);
            if (!Directory.Exists(target)) return true;
            foreach (var name in RequiredFiles)
            {
                var p = Path.Combine(target, name);
                if (File.Exists(p)) File.Delete(p);
            }
            if (!Directory.EnumerateFileSystemEntries(target).Any())
                Directory.Delete(target);
            return !Verify(gamesDir).Ok;
        }
        catch { return false; }
    }

    public (bool Ok, string[] Missing, string Detail) Verify(string? gamesDir = null)
    {
        gamesDir ??= DetectTrenchBroomGamesDir();
        if (gamesDir == null) return (false, RequiredFiles.ToArray(), "TrenchBroom installation not found");
        var target = TargetDir(gamesDir);
        var missing = new List<string>();
        var notes = new List<string>();

        foreach (var name in RequiredFiles)
        {
            var p = Path.Combine(target, name);
            if (!File.Exists(p)) { missing.Add(name); continue; }
            try
            {
                if (name == "GameConfig.cfg")
                {
                    var text = File.ReadAllText(p);
                    if (!text.Contains("\"Relic\"") && !text.Contains("'Relic'") && !text.Contains("Relic"))
                        missing.Add(name + " (missing game name Relic)");
                    if (!text.Contains("Valve"))
                        missing.Add(name + " (missing Valve map format)");
                    if (!text.Contains("Relic.fgd"))
                        missing.Add(name + " (missing Relic.fgd entity definitions)");
                }
                else if (name == "Icon.png")
                {
                    var bytes = File.ReadAllBytes(p);
                    if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != (byte)'P' || bytes[2] != (byte)'N' || bytes[3] != (byte)'G')
                        missing.Add(name + " (not a valid PNG)");
                }
                else if (name == "Relic.fgd")
                {
                    var text = File.ReadAllText(p);
                    foreach (var cls in new[] { "player_start", "enemy_soldier", "enemy_dog", "enemy_demon", "weapon_pickup", "health_pickup", "prop_static", "light", "light_spot", "light_sun", "env_fog", "env_postprocess", "trigger_once",
                        "ambient_sound", "sound_emitter", "sound_zone", "music_player", "music_trigger", "reverb_zone",
                        "trigger_multiple", "trigger_hurt", "trigger_heal", "trigger_push", "trigger_teleport", "trigger_sound", "trigger_music",
                        "func_door", "func_door_rotating", "func_button", "func_elevator", "func_platform", "func_train",
                        "logic_timer", "logic_random", "logic_relay", "logic_counter", "logic_compare",
                        "path_corner", "path_patrol", "info_enemy_spawn",
                        "env_particles", "fx_smoke", "fx_fire", "fx_sparks", "fx_blood", "fx_explosion", "fx_muzzleflash",
                        "env_rain", "env_snow", "env_lightning", "env_wind", "env_skybox", "env_ambient",
                        "ammo_pickup", "armor_pickup", "keycard_pickup", "weapon_spawn",
                        "objective", "checkpoint", "mission_start", "mission_end" })
                        if (!text.Contains(cls)) missing.Add(name + $" (missing entity {cls})");
                }
            }
            catch (Exception ex) { notes.Add($"{name}: {ex.Message}"); }
        }

        // Palette: resolved by TrenchBroom through the game filesystem
        // (<game path>/Assets/gfx/palette.lmp), not the games/Relic/ dir.
        var palettePath = Path.Combine(_assetsRoot, "gfx", "palette.lmp");
        if (!File.Exists(palettePath))
            missing.Add("Assets/gfx/palette.lmp (Quake palette for MDL previews)");
        else if (new FileInfo(palettePath).Length != 768)
            missing.Add("Assets/gfx/palette.lmp (must be 768 bytes: 256 RGB entries)");

        var detail = missing.Count == 0
            ? $"Relic game definition OK at {target}" + (notes.Count > 0 ? $" (notes: {string.Join("; ", notes)})" : "")
            : $"Missing/invalid at {target}: {string.Join(", ", missing)}";
        return (missing.Count == 0, missing.ToArray(), detail);
    }

    private static string FindAssetsRoot()
    {
        string[] candidates =
        {
            "Assets",
            Path.Combine(AppContext.BaseDirectory, "Assets"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets"),
            "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets",
        };
        foreach (var c in candidates)
        {
            try { if (Directory.Exists(c)) return Path.GetFullPath(c); }
            catch { }
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            try
            {
                var p = Path.Combine(dir.FullName, "Assets");
                if (Directory.Exists(p)) return p;
                var p2 = Path.Combine(dir.FullName, "RELIC_engine", "Assets");
                if (Directory.Exists(p2)) return p2;
            }
            catch { }
        }
        return "Assets";
    }
}
