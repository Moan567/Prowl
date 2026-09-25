// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// Automatic .map hot-reload: watch the current TrenchBroom .map file and
// reimport it into the open scene a moment after every save, so edits made
// in TrenchBroom appear in the editor in real time with no button press.
//
// Threading: FileSystemWatcher fires on a background thread, so it only sets
// a pending flag. Update() runs on the editor main thread (called once per
// frame from EditorApplication) and performs the actual reimport.

using System;
using System.IO;

using Prowl.Runtime;
using Prowl.Runtime.Relic;
using Prowl.Runtime.Resources;

namespace Prowl.Editor.Relic;

public static class RelicMapAutoReload
{
    /// <summary>Master toggle, surfaced as "Auto-reload on save" in the Relic panels.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>How long to wait after the last file event before reimporting (ms). Coalesces TrenchBroom's save burst.</summary>
    public static double DebounceMs { get; set; } = 750;

    /// <summary>Human-readable status for the Relic panels ("Watching ...", "Reloaded ...", error text).</summary>
    public static string Status { get; private set; } = string.Empty;

    /// <summary>When the last successful auto-reload happened (local time).</summary>
    public static DateTime LastReloadTime { get; private set; } = DateTime.MinValue;

    /// <summary>Whether a watcher is currently armed on an existing .map file.</summary>
    public static bool IsWatching => _watcher != null && !string.IsNullOrEmpty(_watchedPath) && File.Exists(_watchedPath);

    /// <summary>Last import options used (set by every manual Import/Reimport so auto-reloads match).</summary>
    public static RelicBuildOptions? LastOptions { get; private set; }

    private static FileSystemWatcher? _watcher;
    private static string? _watchedPath;
    private static readonly object _lock = new();
    private static DateTime _lastEventUtc = DateTime.MinValue;
    private static bool _hasPending;
    private static string? _pendingPath;

    /// <summary>Call after every manual Import/Reimport so auto-reloads reuse the same options.</summary>
    public static void NotifyImported(string mapPath, RelicBuildOptions? options)
    {
        if (options != null)
        {
            LastOptions = new RelicBuildOptions
            {
                UnitScale = options.UnitScale,
                BuildCollision = options.BuildCollision,
                BuildMaterials = options.BuildMaterials,
                SpawnEntities = options.SpawnEntities,
                SpawnPlayerRig = options.SpawnPlayerRig,
                LoadTextures = options.LoadTextures,
                ProjectRoot = options.ProjectRoot,
                FallbackTextureSize = options.FallbackTextureSize
            };
            LastOptions.TextureSearchRoots.AddRange(options.TextureSearchRoots);
            foreach (var ns in options.NonSolidBrushOwners)
                LastOptions.NonSolidBrushOwners.Add(ns);
        }
        EnsureWatching(mapPath);
        // The file we just read is the current content — ignore any event it raised.
        lock (_lock) { _hasPending = false; _pendingPath = null; }
        if (string.IsNullOrEmpty(Status) || Status.StartsWith("Reloaded", StringComparison.OrdinalIgnoreCase))
            Status = $"Watching '{Path.GetFileName(mapPath)}' for changes.";
    }

    /// <summary>Main-thread tick. Called once per editor frame.</summary>
    public static void Update()
    {
        string? current = null;
        try { current = RelicTrenchBroom.CurrentMapPath; } catch { }

        if (!Enabled || string.IsNullOrEmpty(current) || !File.Exists(current))
        {
            if (!string.IsNullOrEmpty(current) && !File.Exists(current))
                Status = $"Map file not found: '{current}'.";
            if (!Enabled) EnsureWatching(null);
            else EnsureWatching(current);
            // Drop stale pendings when disabled or file vanished.
            if (!Enabled || string.IsNullOrEmpty(current) || !File.Exists(current))
            {
                lock (_lock) { _hasPending = false; _pendingPath = null; }
            }
            return;
        }

        EnsureWatching(current);

        string? toReload = null;
        lock (_lock)
        {
            if (!_hasPending) return;
            if ((DateTime.UtcNow - _lastEventUtc).TotalMilliseconds < DebounceMs) return;
            _hasPending = false;
            toReload = _pendingPath ?? current;
            _pendingPath = null;
        }

        TryReload(toReload ?? current);
    }

    /// <summary>Force an immediate reimport now, regardless of debounce (used by panels).</summary>
    public static bool ReloadNow()
    {
        string? current;
        try { current = RelicTrenchBroom.CurrentMapPath; } catch { current = null; }
        if (string.IsNullOrEmpty(current) || !File.Exists(current)) return false;
        lock (_lock) { _hasPending = false; _pendingPath = null; }
        return TryReload(current);
    }

    private static bool TryReload(string mapPath)
    {
        Scene? scene = null;
        try { scene = Scene.Current; } catch { }
        if (scene == null || !scene.IsValid())
        {
            Status = "Auto-reload skipped: no scene open.";
            return false;
        }

        // TrenchBroom may still hold the file for a moment after the event fires.
        // Retry a few times; if still locked, re-arm pending so the next frame tries again.
        Exception? lastError = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var options = CloneOptions(LastOptions);
                var report = RelicSceneBuilder.Reimport(mapPath, scene, options);
                LastReloadTime = DateTime.Now;
                Status = $"Reloaded '{Path.GetFileName(mapPath)}' at {LastReloadTime:HH:mm:ss} — {report.WorldTriangles} tris, {report.EntitiesSpawned} entities.";
                Runtime.Debug.Log($"[Relic][AutoReload] {Status}");
                foreach (var g in report.GeometryIssues) Runtime.Debug.LogError($"[Relic][Geometry] {g}");
                foreach (var m in report.MissingTextures) Runtime.Debug.LogWarning($"[Relic] Missing texture '{m}' — fallback color used.");
                try { GUI.SceneView.EditorSceneManager.MarkDirty(); } catch { }
                return true;
            }
            catch (IOException ex)
            {
                lastError = ex;
                System.Threading.Thread.Sleep(50 * (attempt + 1));
            }
            catch (Exception ex)
            {
                Status = $"Auto-reload failed: {ex.Message}";
                Runtime.Debug.LogError($"[Relic][AutoReload] {Status}\n{ex}");
                return false;
            }
        }

        // Still locked — try again next frame rather than surfacing an error.
        lock (_lock) { _hasPending = true; _pendingPath = mapPath; _lastEventUtc = DateTime.UtcNow; }
        Status = $"Auto-reload deferred (file busy): '{Path.GetFileName(mapPath)}'.";
        if (lastError != null) Runtime.Debug.LogWarning($"[Relic][AutoReload] {Status} {lastError.Message}");
        return false;
    }

    private static RelicBuildOptions CloneOptions(RelicBuildOptions? src)
    {
        var dst = new RelicBuildOptions();
        string? projectRoot = null;
        try { projectRoot = Projects.Project.Current?.RootPath; } catch { }
        if (src != null)
        {
            dst.UnitScale = src.UnitScale;
            dst.BuildCollision = src.BuildCollision;
            dst.BuildMaterials = src.BuildMaterials;
            dst.SpawnEntities = src.SpawnEntities;
            dst.SpawnPlayerRig = src.SpawnPlayerRig;
            dst.LoadTextures = src.LoadTextures;
            dst.ProjectRoot = src.ProjectRoot ?? projectRoot;
            dst.FallbackTextureSize = src.FallbackTextureSize;
            dst.TextureSearchRoots.AddRange(src.TextureSearchRoots);
            dst.NonSolidBrushOwners.Clear();
            foreach (var ns in src.NonSolidBrushOwners) dst.NonSolidBrushOwners.Add(ns);
        }
        else
        {
            dst.ProjectRoot = projectRoot;
        }
        return dst;
    }

    private static void EnsureWatching(string? mapPath)
    {
        if (string.Equals(_watchedPath, mapPath, StringComparison.OrdinalIgnoreCase))
        {
            // Watched file deleted out from under us — drop the watcher so IsWatching is honest.
            if (!string.IsNullOrEmpty(mapPath) && !File.Exists(mapPath)) SetWatcher(null, null);
            return;
        }
        SetWatcher(null, null);
        if (string.IsNullOrEmpty(mapPath) || !File.Exists(mapPath)) return;

        string? dir;
        string? file;
        try
        {
            dir = Path.GetDirectoryName(mapPath);
            file = Path.GetFileName(mapPath);
        }
        catch { return; }
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file) || !Directory.Exists(dir)) return;

        try
        {
            var watcher = new FileSystemWatcher(dir, file)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
            };
            FileSystemEventHandler onEvent = (_, e) => OnMapFileEvent(e.FullPath);
            RenamedEventHandler onRenamed = (_, e) => OnMapFileEvent(e.FullPath);
            watcher.Changed += onEvent;
            watcher.Created += onEvent;
            watcher.Renamed += onRenamed;
            watcher.Error += (_, e) =>
            {
                try { Runtime.Debug.LogWarning($"[Relic][AutoReload] Watcher error: {e.GetException().Message}"); } catch { }
            };
            watcher.EnableRaisingEvents = true;
            SetWatcher(watcher, mapPath);
            Status = $"Watching '{file}' for changes.";
        }
        catch (Exception ex)
        {
            SetWatcher(null, null);
            Status = $"Could not watch '{file}': {ex.Message}";
        }
    }

    private static void SetWatcher(FileSystemWatcher? watcher, string? path)
    {
        var old = _watcher;
        _watcher = watcher;
        _watchedPath = path;
        if (old != null)
        {
            try { old.EnableRaisingEvents = false; } catch { }
            try { old.Dispose(); } catch { }
        }
    }

    private static void OnMapFileEvent(string fullPath)
    {
        lock (_lock)
        {
            _hasPending = true;
            _pendingPath = fullPath;
            _lastEventUtc = DateTime.UtcNow;
        }
    }
}
