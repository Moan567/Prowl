// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Vector;

namespace Prowl.Runtime.Relic.Map;

/// <summary>
/// Source-of-truth data for a Valve .map file (TrenchBroom).
/// Units: 1 unit = 1 inch. Quake axes: X forward, Y right, Z up.
/// Prowl uses Y-up, so conversion happens in <see cref="RelicSceneBuilder"/>.
/// </summary>
public sealed class RelicMapData
{
    public List<RelicMapBrush> Brushes { get; } = new();
    public List<RelicMapEntity> Entities { get; } = new();
    public Dictionary<string, string> WorldspawnProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string SourcePath { get; set; } = string.Empty;
}

public sealed class RelicMapEntity
{
    public string ClassName { get; set; } = string.Empty;
    /// <summary>Origin in Quake space (Z-up inches).</summary>
    public Float3 OriginQuake { get; set; } = Float3.Zero;
    public bool HasOrigin { get; set; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<RelicMapBrush> Brushes { get; } = new();

    public string GetString(string key, string fallback = "") =>
        Properties.TryGetValue(key, out var v) ? v : fallback;

    public float GetFloat(string key, float fallback = 0f)
    {
        if (Properties.TryGetValue(key, out var v) &&
            float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f))
            return f;
        return fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        if (Properties.TryGetValue(key, out var v) &&
            int.TryParse(v, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var i))
            return i;
        return fallback;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        if (Properties.TryGetValue(key, out var v))
        {
            if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        }
        return fallback;
    }
}

public sealed class RelicMapBrush
{
    public List<RelicMapFace> Faces { get; } = new();
    /// <summary>Owning entity classname for brush entities (func_door, trigger_*, ...). Empty = worldspawn.</summary>
    public string OwnerClassName { get; set; } = string.Empty;
    public Dictionary<string, string> OwnerProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RelicMapFace
{
    public Float3 P1, P2, P3;
    /// <summary>Plane normal in Quake space, computed at parse time.</summary>
    public Float3 Normal;
    public float Dist;
    public string Texture { get; set; } = string.Empty;
    public float OffsetU, OffsetV;
    public float Rotation;
    public float ScaleU = 1f, ScaleV = 1f;
    /// <summary>
    /// Valve220 explicit UV axes (Quake space). When true, UVs project onto these
    /// axes (rotation is baked into them by TrenchBroom) instead of the standard
    /// dominant-plane projection.
    /// </summary>
    public bool HasValveAxes;
    public Float3 ValveUAxis = Float3.UnitX;
    public Float3 ValveVAxis = Float3.UnitY;
}
