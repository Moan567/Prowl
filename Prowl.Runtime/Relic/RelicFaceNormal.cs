// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime.Relic;

/// <summary>
/// Applies a custom normal onto individual brush faces. The importer fills
/// <see cref="Ranges"/> (face index, texture, base normal); add entries to
/// <see cref="Overrides"/> to re-aim specific faces (flat shading, sloped-light
/// fixes, stylized lighting). Edits apply live in the Inspector and on enable.
/// </summary>
[AddComponentMenu("Relic/Brush/Face Normals")]
public sealed class RelicFaceNormal : MonoBehaviour
{
    [Serializable]
    public struct FaceInfo
    {
        public int FaceIndex;
        public string Texture;
        public int StartVertex;
        public int VertexCount;
        public Float3 BaseNormal;
    }

    [Serializable]
    public struct NormalOverride
    {
        public int FaceIndex;
        public Float3 Normal;
    }

    /// <summary>Mesh these normals belong to. Falls back to a sibling MeshRenderer.</summary>
    public AssetRef<Mesh> Mesh;

    /// <summary>Faces of this mesh, recorded at import (read-only reference).</summary>
    public List<FaceInfo> Ranges = new();

    /// <summary>Custom normals by face index. Empty = keep imported normals.</summary>
    public List<NormalOverride> Overrides = new();

    /// <summary>Parse TrenchBroom brush-entity keys: _normal (all faces) and _normalN (face N).</summary>
    public static Dictionary<int, Float3> ParseMapKeys(Dictionary<string, string> properties)
    {
        var result = new Dictionary<int, Float3>();
        foreach (var kv in properties)
        {
            Float3 n;
            if (kv.Key.Equals("_normal", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseNormal(kv.Value, out n)) result[-1] = n;
            }
            else if (kv.Key.StartsWith("_normal", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(kv.Key[7..], out int idx) && idx >= 0)
            {
                if (TryParseNormal(kv.Value, out n)) result[idx] = n;
            }
        }
        return result;
    }

    /// <summary>
    /// Parse a TrenchBroom-space normal ("x y z", Z-up inches) into a
    /// Prowl-space unit normal, matching the conversion applied to geometry.
    /// Component <see cref="Overrides"/> are already in mesh (Prowl) space.
    /// </summary>
    public static bool TryParseNormal(string s, out Float3 normal)
    {
        normal = Float3.UnitY;
        var v = Relic.Map.RelicMapParser.ParseVector3(s);
        if (Float3.LengthSquared(v) < 1e-8f) return false;
        normal = Relic.Map.RelicBrushBuilder.QuakeNormalToProwl(Float3.Normalize(v));
        return true;
    }

    /// <summary>Apply map-key overrides directly onto built (pre-grouping) geometry.</summary>
    public static void ApplyMapOverrides(
        Relic.Map.RelicBrushBuilder.BuiltGeometry geo, Dictionary<int, Float3> overrides)
    {
        if (overrides.Count == 0) return;
        foreach (var range in geo.FaceRanges)
        {
            Float3 n;
            if (!overrides.TryGetValue(range.FaceIndex, out n) &&
                !overrides.TryGetValue(-1, out n))
                continue;
            for (int i = 0; i < range.VertexCount; i++)
            {
                int v = range.StartVertex + i;
                if (v >= 0 && v < geo.Normals.Length) geo.Normals[v] = n;
            }
        }
        // Keep the recorded base normals in sync with what was applied.
        for (int i = 0; i < geo.FaceRanges.Count; i++)
        {
            var r = geo.FaceRanges[i];
            Float3 n;
            if (overrides.TryGetValue(r.FaceIndex, out n) || overrides.TryGetValue(-1, out n))
            {
                r.Normal = n;
                geo.FaceRanges[i] = r;
            }
        }
    }

    public override void OnValidate() => Apply();

    public override void OnEnable() => Apply();

    /// <summary>Rewrite mesh normals from Ranges + Overrides. Idempotent.</summary>
    public void Apply()
    {
        if (Ranges.Count == 0) return;
        Mesh m = Mesh.Res;
        if (!m.IsValid())
        {
            var mr = GetComponent<MeshRenderer>();
            if (!mr.IsValid()) return;
            AssetRef<Mesh> rmesh = mr.Mesh;
            rmesh.EnsureLoaded();
            m = rmesh.Res;
            if (!m.IsValid()) return;
        }

        var byFace = new Dictionary<int, Float3>();
        foreach (var o in Overrides)
        {
            if (Float3.LengthSquared(o.Normal) < 1e-8f) continue;
            byFace[o.FaceIndex] = Float3.Normalize(o.Normal);
        }

        var normals = new Float3[m.VertexCount];
        foreach (var r in Ranges)
        {
            Float3 n = byFace.TryGetValue(r.FaceIndex, out var custom) ? custom : r.BaseNormal;
            for (int i = 0; i < r.VertexCount; i++)
            {
                int v = r.StartVertex + i;
                if (v >= 0 && v < normals.Length) normals[v] = n;
            }
        }
        m.Normals = normals;
        try { m.Upload(); } catch { }
    }
}
