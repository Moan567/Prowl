// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// One-button TrenchBroom workflow: open current map, reimport on return.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Prowl.Runtime.Relic;

public static class RelicTrenchBroom
{
    /// <summary>Last imported .map path — the "current map" for Open/Reimport.</summary>
    public static string? CurrentMapPath { get; set; }

    public static string FgdFileName => "Relic.fgd";

    /// <summary>
    /// Locate TrenchBroom.exe: env var, project tools folder, a TrenchBroom*
    /// folder next to the project/repo (e.g. TrenchBroom-Win64-*-Release),
    /// default install paths, then PATH. No manual configuration needed.
    /// </summary>
    public static string? FindExecutable(string? projectRoot = null)
    {
        var env = Environment.GetEnvironmentVariable("TRENCHBROOM_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

        if (!string.IsNullOrEmpty(projectRoot))
        {
            foreach (var candidate in new[]
            {
                Path.Combine(projectRoot, "Tools", "TrenchBroom", "TrenchBroom.exe"),
                Path.Combine(projectRoot, "TrenchBroom", "TrenchBroom.exe"),
            })
                if (File.Exists(candidate)) return candidate;
        }

        // Folders like TrenchBroom-Win64-v2023.1-Release/ sitting next to the
        // project or repo root (one level deep, no recursive search).
        foreach (var root in AncestorRoots(projectRoot))
        {
            var found = ScanAppFolder(root);
            if (found != null) return found;
        }

        foreach (var dir in new[]
        {
            @"C:\Program Files\TrenchBroom\TrenchBroom.exe",
            @"C:\Program Files (x86)\TrenchBroom\TrenchBroom.exe",
        })
            if (File.Exists(dir)) return dir;

        // PATH lookup.
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var d in path.Split(Path.PathSeparator))
        {
            try
            {
                var c = Path.Combine(d.Trim(), "TrenchBroom.exe");
                if (File.Exists(c)) return c;
                var c2 = Path.Combine(d.Trim(), "trenchbroom");
                if (File.Exists(c2)) return c2;
            }
            catch { }
        }
        return null;
    }

    /// <summary>Check dir itself and one level of TrenchBroom* subfolders for the app binary.</summary>
    private static string? ScanAppFolder(string dir)
    {
        try
        {
            var direct = Path.Combine(dir, "TrenchBroom.exe");
            if (File.Exists(direct)) return direct;
            var directNix = Path.Combine(dir, "trenchbroom");
            if (File.Exists(directNix)) return directNix;
            foreach (var sub in Directory.GetDirectories(dir, "TrenchBroom*"))
            {
                var c = Path.Combine(sub, "TrenchBroom.exe");
                if (File.Exists(c)) return c;
                var c2 = Path.Combine(sub, "trenchbroom");
                if (File.Exists(c2)) return c2;
            }
        }
        catch { }
        return null;
    }

    /// <summary>Project root, engine assembly neighbourhood and CWD, walked upward.</summary>
    private static IEnumerable<string> AncestorRoots(string? projectRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var starts = new List<string?>();
        if (!string.IsNullOrEmpty(projectRoot)) starts.Add(projectRoot);
        try { starts.Add(AppContext.BaseDirectory); } catch { }
        try { starts.Add(Directory.GetCurrentDirectory()); } catch { }
        foreach (var start in starts)
        {
            string? dir = start;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                if (seen.Add(dir)) yield return dir;
                try { dir = Directory.GetParent(dir)?.FullName; }
                catch { break; }
            }
        }
    }

    /// <summary>Open the current map in TrenchBroom (no manual workflow).</summary>
    public static bool OpenCurrentMap(string? trenchBroomExe = null, string? projectRoot = null)
    {
        var exe = trenchBroomExe ?? FindExecutable(projectRoot);
        if (string.IsNullOrEmpty(exe) || CurrentMapPath == null) return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe, $"\"{CurrentMapPath}\"") { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    /// <summary>Resolve the Relic.fgd shipped next to the engine for TrenchBroom game config.</summary>
    public static string? FindFgd(string? projectRoot = null)
    {
        if (!string.IsNullOrEmpty(projectRoot))
        {
            var c = Path.Combine(projectRoot, FgdFileName);
            if (File.Exists(c)) return c;
            var c2 = Path.Combine(projectRoot, "Assets", FgdFileName);
            if (File.Exists(c2)) return c2;
        }
        string? assets = FindEngineAssetsDir(projectRoot);
        if (assets != null)
        {
            var c = Path.Combine(assets, FgdFileName);
            if (File.Exists(c)) return c;
        }
        return null;
    }

    /// <summary>
    /// Find the shipped engine assets dir (contains Relic.fgd + Textures/ + Maps/
    /// + TrenchBroom/GameConfig.cfg): RELIC_engine/Assets style layout, searched
    /// around the project root, the engine binaries and the working directory.
    /// </summary>
    public static string? FindEngineAssetsDir(string? projectRoot = null)
    {
        foreach (var root in AncestorRoots(projectRoot))
        {
            foreach (var cand in new[]
            {
                Path.Combine(root, "Assets"),
                Path.Combine(root, "RELIC_engine", "Assets"),
                root,
            })
            {
                try
                {
                    if (File.Exists(Path.Combine(cand, FgdFileName))) return cand;
                    if (File.Exists(Path.Combine(cand, "TrenchBroom", "GameConfig.cfg"))) return cand;
                }
                catch { }
            }
        }
        return null;
    }

    /// <summary>
    /// The TrenchBroom game path: the folder whose Assets/ holds Textures, Maps
    /// and the FGD. Point TrenchBroom's game configuration here.
    /// </summary>
    public static string? FindGamePath(string? projectRoot = null)
    {
        string? assets = FindEngineAssetsDir(projectRoot);
        if (assets == null) return null;
        // Game path is the folder containing Assets/ (per GameConfig searchpath).
        if (assets.EndsWith($"{Path.DirectorySeparatorChar}Assets", StringComparison.OrdinalIgnoreCase) ||
            assets.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(assets), "Assets", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string? parent = Directory.GetParent(assets)?.FullName;
                if (parent != null && Directory.Exists(Path.Combine(parent, "Assets"))) return parent;
            }
            catch { }
        }
        return assets;
    }

    /// <summary>Face-texture folder for the current setup (may not exist yet).</summary>
    public static string? FindTextureDir(string? projectRoot = null)
    {
        // Prefer the project's own Textures/ so user textures win; fall back to shipped pack.
        if (!string.IsNullOrEmpty(projectRoot))
        {
            var p = Path.Combine(projectRoot, "Assets", "Textures");
            if (Directory.Exists(p)) return p;
        }
        string? assets = FindEngineAssetsDir(projectRoot);
        if (assets == null) return null;
        return Path.Combine(assets, "Textures");
    }

    /// <summary>Map folder for the current setup (may not exist yet).</summary>
    public static string? FindMapsDir(string? projectRoot = null)
    {
        if (!string.IsNullOrEmpty(projectRoot))
        {
            var p = Path.Combine(projectRoot, "Assets", "Maps");
            if (Directory.Exists(p)) return p;
        }
        string? assets = FindEngineAssetsDir(projectRoot);
        if (assets == null) return null;
        return Path.Combine(assets, "Maps");
    }
}
