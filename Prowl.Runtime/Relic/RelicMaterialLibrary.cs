// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// Procedural materials for map textures: texture name -> Prowl Material.
// Real texture loading (WAD / PNG folders) can replace ColorFor() later.

using System;
using System.Collections.Generic;

using Prowl.Runtime.Relic.Map;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime.Relic;

/// <summary>
/// PBR materials from .map texture names. With a texture set, faces get their
/// real TrenchBroom textures; without one (or when a texture file is missing),
/// a deterministic procedural color keeps the "press Import, walk around
/// instantly" promise with zero setup.
/// </summary>
public static class RelicMaterialLibrary
{
    private static readonly Dictionary<string, Material> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static Material Get(string textureName, RelicTextureSet? textures = null)
    {
        if (string.IsNullOrWhiteSpace(textureName)) textureName = "__default__";
        if (textures != null && textures.TryGetTexture(textureName, out var tex) && tex.IsValid())
            return GetTextured(textureName, tex);
        if (_cache.TryGetValue(textureName, out var cached) && cached.IsValid()) return cached;
        var mat = Material.DefaultMaterial;
        try { mat.Name = $"M_{textureName}"; } catch { }
        mat.SetColor("_MainColor", ColorFor(textureName));
        mat.SetFloat("_Roughness", 0.85f);
        mat.SetFloat("_Metallic", 0.0f);
        _cache[textureName] = mat;
        return mat;
    }

    /// <summary>Whether the named texture resolved to a real image (vs procedural fallback).</summary>
    public static bool IsTextured(string textureName, RelicTextureSet? textures) =>
        textures != null && textures.TryGetTexture(textureName, out var tex) && tex.IsValid();

    private static Material GetTextured(string textureName, Texture2D tex)
    {
        string key = "T:" + textureName.ToLowerInvariant();
        if (_cache.TryGetValue(key, out var cached) && cached.IsValid()) return cached;
        var mat = Material.DefaultMaterial;
        try { mat.Name = $"M_{textureName}"; } catch { }
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_MainColor", Color.White);
        mat.SetFloat("_Roughness", 0.85f);
        mat.SetFloat("_Metallic", 0.0f);
        _cache[key] = mat;
        return mat;
    }

    public static void Clear()
    {
        _cache.Clear();
    }

    /// <summary>Stable hash -> muted industrial color so adjacent textures are distinguishable.</summary>
    public static Color ColorFor(string textureName)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in textureName.ToLowerInvariant())
            {
                h ^= c;
                h *= 16777619;
            }
            float hue = (h % 360) / 360f;
            // Muted industrial palette: moderate saturation, mid value.
            return FromHsv(hue, 0.32f, 0.62f);
        }
    }

    private static Color FromHsv(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1 - Math.Abs((h * 6) % 2 - 1));
        float m = v - c;
        float r = 0, g = 0, b = 0;
        float hh = h * 6;
        if (hh < 1) { r = c; g = x; }
        else if (hh < 2) { r = x; g = c; }
        else if (hh < 3) { g = c; b = x; }
        else if (hh < 4) { g = x; b = c; }
        else if (hh < 5) { r = x; b = c; }
        else { r = c; b = x; }
        return new Color(r + m, g + m, b + m, 1f);
    }
}
