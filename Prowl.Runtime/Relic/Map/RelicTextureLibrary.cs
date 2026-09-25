// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// Texture resolution for .map face textures: bare TrenchBroom texture names
// (e.g. "brick") -> image files on disk -> Prowl Texture2D + sizes for UVs.
// TrenchBroom reads from <game>/Assets/Textures/ (see GameConfig.cfg); the
// importer mirrors that layout so what you paint in TB is what you get in Relic.

using System;
using System.Collections.Generic;
using System.IO;

using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime.Relic.Map;

/// <summary>
/// Per-import texture set: resolved + loaded textures, real pixel sizes for
/// TB-accurate UVs, and the missing-texture list surfaced to the editor.
/// </summary>
public sealed class RelicTextureSet
{
    public List<string> SearchRoots { get; } = new();

    /// <summary>Bare texture names with no file found in any search root.</summary>
    public HashSet<string> Missing { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Texture2D> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Float2> _sizes = new(StringComparer.OrdinalIgnoreCase);

    public Float2 FallbackSize = new(64, 64);

    public int LoadedCount => _loaded.Count;

    /// <summary>Find the file for a texture without touching the GPU (safe in tests).</summary>
    public string? ResolvePath(string textureName) =>
        RelicTextureLibrary.ResolveTexturePath(textureName, SearchRoots);

    /// <summary>Get (and lazily load) the texture. Returns false when missing.</summary>
    public bool TryGetTexture(string textureName, out Texture2D texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(textureName)) return false;
        if (_loaded.TryGetValue(textureName, out var cached) && cached.IsValid())
        {
            texture = cached;
            return true;
        }
        string? path = ResolvePath(textureName);
        if (path == null)
        {
            Missing.Add(textureName);
            return false;
        }
        try
        {
            var tex = Texture2D.FromFile(path, generateMipmaps: true);
            _loaded[textureName] = tex;
            _paths[textureName] = path;
            _sizes[textureName] = new Float2(tex.Width, tex.Height);
            Missing.Remove(textureName);
            texture = tex;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Relic] Texture '{textureName}' failed to load ({path}): {ex.Message}");
            Missing.Add(textureName);
            return false;
        }
    }

    /// <summary>Real pixel size for UV math, or the fallback when missing/unloaded.</summary>
    public Float2 GetSize(string textureName)
    {
        if (_sizes.TryGetValue(textureName, out var s) && s.X > 0 && s.Y > 0) return s;
        return FallbackSize;
    }

    internal void Preload(IEnumerable<string> textureNames)
    {
        foreach (var name in textureNames)
        {
            if (RelicBrushBuilder.IsNodraw(name)) continue;
            TryGetTexture(name, out _);
        }
    }
}

public static class RelicTextureLibrary
{
    public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".tga", ".bmp" };

    /// <summary>
    /// Default search roots for a game project: project textures first (user
    /// overrides win), then the shipped RELIC_engine texture pack as fallback.
    /// </summary>
    public static List<string> GetDefaultSearchRoots(string? projectRoot)
    {
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(projectRoot))
        {
            roots.Add(Path.Combine(projectRoot, "Assets", "Textures"));
            roots.Add(Path.Combine(projectRoot, "Assets"));
        }
        string? engineAssets = RelicTrenchBroom.FindEngineAssetsDir(projectRoot);
        if (engineAssets != null)
        {
            roots.Add(Path.Combine(engineAssets, "Textures"));
            roots.Add(engineAssets);
        }
        return roots;
    }

    /// <summary>Filesystem-only resolution: no GPU, safe anywhere. Returns null when missing.</summary>
    public static string? ResolveTexturePath(string textureName, IEnumerable<string> searchRoots)
    {
        if (string.IsNullOrWhiteSpace(textureName)) return null;
        // Bare TrenchBroom names ("brick"); tolerate "path/brick" and "brick.png".
        string noExt = textureName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                       textureName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                       textureName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                       textureName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ||
                       textureName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
            ? textureName[..textureName.LastIndexOf('.')]
            : textureName;
        string fileName = noExt.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string bare = Path.GetFileName(fileName);

        var candidates = new List<string> { fileName };
        if (!string.Equals(bare, fileName, StringComparison.Ordinal)) candidates.Add(bare);
        // Case variants for case-sensitive filesystems.
        int count = candidates.Count;
        for (int i = 0; i < count; i++)
        {
            string lower = candidates[i].ToLowerInvariant();
            if (!candidates.Contains(lower)) candidates.Add(lower);
        }

        foreach (var root in searchRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            foreach (var cand in candidates)
            {
                // As-written (already has an extension?) first.
                if (Path.HasExtension(cand))
                {
                    string direct = Path.Combine(root, cand);
                    if (File.Exists(direct)) return direct;
                }
                foreach (var ext in Extensions)
                {
                    string full = Path.Combine(root, cand + ext);
                    if (File.Exists(full)) return full;
                }
            }
        }
        return null;
    }

    /// <summary>Load every texture a map uses (skips nodraw/clip/trigger specials).</summary>
    public static RelicTextureSet LoadForMap(RelicMapData map, IEnumerable<string> searchRoots, Float2? fallbackSize = null)
    {
        var set = new RelicTextureSet();
        if (fallbackSize.HasValue) set.FallbackSize = fallbackSize.Value;
        foreach (var r in searchRoots) set.SearchRoots.Add(r);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in map.Brushes)
            foreach (var f in b.Faces)
                names.Add(f.Texture);
        set.Preload(names);
        return set;
    }
}
