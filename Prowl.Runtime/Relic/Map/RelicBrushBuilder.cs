// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime.Relic.Map;

/// <summary>
/// Builds Prowl <see cref="Mesh"/> data from Quake brushes via plane clipping.
/// Quake space (inches, Z-up) is converted to Prowl space (meters, Y-up):
/// Prowl = (qx, qz, -qy) * UnitScale. Default UnitScale = 1/32 (32 units = 1m).
/// </summary>
public static class RelicBrushBuilder
{
    public const float DefaultUnitScale = 1f / 32f;

    public sealed class BuiltGeometry
    {
        public Float3[] Vertices = Array.Empty<Float3>();
        public Float3[] Normals = Array.Empty<Float3>();
        public Float2[] UVs = Array.Empty<Float2>();
        public uint[] Indices = Array.Empty<uint>();
        /// <summary>Per-vertex texture name, parallel to Vertices.</summary>
        public string[] Textures = Array.Empty<string>();
        public AABB Bounds;
    }

    public sealed class MaterialGroup
    {
        public string TextureName = string.Empty;
        public List<Float3> Vertices { get; } = new();
        public List<Float3> Normals { get; } = new();
        public List<Float2> UVs { get; } = new();
        public List<uint> Indices { get; } = new();
    }

    /// <summary>Convert a Quake-space point (inches, Z-up) to Prowl-space (meters, Y-up).</summary>
    public static Float3 QuakeToProwl(Float3 q, float unitScale = DefaultUnitScale) =>
        new Float3(q.X * unitScale, q.Z * unitScale, -q.Y * unitScale);

    public static Float3 QuakeNormalToProwl(Float3 q) =>
        Float3.NormalizeSafe(new Float3(q.X, q.Z, -q.Y), Float3.UnitY);

    /// <param name="textureSizeOf">Real texture pixel sizes for TB-accurate UVs.
    /// Defaults to 64x64 (Quake-standard texel density) when absent.</param>
    public static BuiltGeometry BuildBrushes(
        IReadOnlyList<RelicMapBrush> brushes,
        float unitScale = DefaultUnitScale,
        Func<string, Float2>? textureSizeOf = null)
    {
        var verts = new List<Float3>();
        var normals = new List<Float3>();
        var uvs = new List<Float2>();
        var indices = new List<uint>();
        var textures = new List<string>();
        var boundsMin = new Float3(float.MaxValue, float.MaxValue, float.MaxValue);
        var boundsMax = new Float3(float.MinValue, float.MinValue, float.MinValue);
        bool hasAny = false;

        foreach (var brush in brushes)
        {
            var planes = FixPlanes(brush);
            for (int fi = 0; fi < brush.Faces.Count; fi++)
            {
                var face = brush.Faces[fi];
                var plane = planes[fi];
                var poly = ClipFacePolygon(brush, planes, fi);
                if (poly.Count < 3) continue;

                var nProwl = QuakeNormalToProwl(plane.Normal);
                var texSize = textureSizeOf?.Invoke(face.Texture) ?? new Float2(64, 64);
                uint baseIndex = (uint)verts.Count;
                for (int k = 0; k < poly.Count; k++)
                {
                    var pProwl = QuakeToProwl(poly[k], unitScale);
                    verts.Add(pProwl);
                    normals.Add(nProwl);
                    uvs.Add(ComputeUV(poly[k], plane, face, texSize));
                    textures.Add(face.Texture);
                    boundsMin = Maths.Min(boundsMin, pProwl);
                    boundsMax = Maths.Max(boundsMax, pProwl);
                    hasAny = true;
                }
                // Triangle fan (poly is convex after clipping). The clipped polygon
                // is wound counter-clockwise seen from outside the brush (along the
                // outward face normal) — the same convention as the engine's own
                // primitives — so the fan preserves that order: (v0, vk, vk+1).
                // Emitting (v0, vk+1, vk) instead mirrors every triangle, making
                // faces render only from the inside under backface culling.
                for (int k = 1; k < poly.Count - 1; k++)
                {
                    uint i0 = baseIndex;
                    uint i1 = baseIndex + (uint)k;
                    uint i2 = baseIndex + (uint)(k + 1);
                    EnsureWinding(verts, ref i1, ref i2, i0, nProwl);
                    indices.Add(i0);
                    indices.Add(i1);
                    indices.Add(i2);
                }
            }
        }

        var geo = new BuiltGeometry
        {
            Vertices = verts.ToArray(),
            Normals = normals.ToArray(),
            UVs = uvs.ToArray(),
            Indices = indices.ToArray(),
            Textures = textures.ToArray(),
            Bounds = hasAny ? new AABB(boundsMin, boundsMax) : new AABB(Float3.Zero, Float3.Zero)
        };
        return geo;
    }

    /// <summary>Split flat geometry into per-texture groups (one Mesh + Material each).</summary>
    public static List<MaterialGroup> GroupByTexture(BuiltGeometry geo)
    {
        var groups = new Dictionary<string, MaterialGroup>(StringComparer.OrdinalIgnoreCase);
        var order = new List<MaterialGroup>();
        var indexRemap = new Dictionary<uint, uint>();

        for (int t = 0; t < geo.Indices.Length; t += 3)
        {
            uint i0 = geo.Indices[t];
            string tex = geo.Textures.Length > (int)i0 ? geo.Textures[i0] : "__nodraw__";
            if (IsNodraw(tex)) continue;
            if (!groups.TryGetValue(tex, out var g))
            {
                g = new MaterialGroup { TextureName = tex };
                groups[tex] = g;
                order.Add(g);
            }
        }

        foreach (var g in order)
        {
            indexRemap.Clear();
            for (int t = 0; t < geo.Indices.Length; t += 3)
            {
                uint triIdx0 = geo.Indices[t];
                string tex = geo.Textures.Length > (int)triIdx0 ? geo.Textures[triIdx0] : string.Empty;
                if (!tex.Equals(g.TextureName, StringComparison.OrdinalIgnoreCase)) continue;
                for (int k = 0; k < 3; k++)
                {
                    uint src = geo.Indices[t + k];
                    if (!indexRemap.TryGetValue(src, out uint dst))
                    {
                        dst = (uint)g.Vertices.Count;
                        indexRemap[src] = dst;
                        g.Vertices.Add(geo.Vertices[src]);
                        g.Normals.Add(geo.Normals[src]);
                        g.UVs.Add(geo.UVs[src]);
                    }
                    g.Indices.Add(dst);
                }
            }
        }
        return order;
    }

    public static Mesh BuildMesh(MaterialGroup group, string name = "RelicBrushMesh")
    {
        var mesh = new Mesh();
        mesh.Vertices = group.Vertices.ToArray();
        mesh.Normals = group.Normals.ToArray();
        mesh.UV = group.UVs.ToArray();
        mesh.Indices = group.Indices.ToArray();
        mesh.MeshTopology = Topology.Triangles;
        mesh.RecalculateBounds();
        mesh.Upload();
        try { mesh.Name = name; } catch { }
        return mesh;
    }

    /// <summary>Axis-aligned bounds of brushes in Prowl space (for triggers/movers).</summary>
    public static AABB ComputeBrushBounds(IReadOnlyList<RelicMapBrush> brushes, float unitScale = DefaultUnitScale)
    {
        var geo = BuildBrushes(brushes, unitScale);
        return geo.Bounds;
    }

    public static bool IsNodraw(string texture) =>
        texture.Equals("nodraw", StringComparison.OrdinalIgnoreCase) ||
        texture.Equals("__nodraw__", StringComparison.OrdinalIgnoreCase) ||
        texture.StartsWith("skip", StringComparison.OrdinalIgnoreCase) ||
        texture.StartsWith("clip", StringComparison.OrdinalIgnoreCase) ||
        texture.StartsWith("trigger", StringComparison.OrdinalIgnoreCase) ||
        texture.StartsWith("*", StringComparison.Ordinal);

    // ---------------- internals ----------------

    public struct FixedPlane { public Float3 Normal; public float Dist; }

    private static List<FixedPlane> FixPlanes(RelicMapBrush brush)
    {
        // Rough center: average of face points.
        var center = Float3.Zero;
        int count = 0;
        foreach (var f in brush.Faces)
        {
            center += f.P1; center += f.P2; center += f.P3;
            count += 3;
        }
        if (count > 0) center /= count;

        var planes = new List<FixedPlane>(brush.Faces.Count);
        foreach (var f in brush.Faces)
        {
            var n = f.Normal;
            var d = f.Dist;
            // Inside of brush is negative side; flip if center is outside.
            if (Float3.Dot(center, n) > d + 0.01f)
            {
                n = -n;
                d = -d;
            }
            planes.Add(new FixedPlane { Normal = n, Dist = d });
        }
        return planes;
    }

    /// <summary>
    /// Clips one brush face down to its visible polygon (Quake space, inches, Z-up).
    /// The returned polygon is convex and wound counter-clockwise seen from outside
    /// the brush (along the outward face normal): the base quad is built on a
    /// right-handed (u, v, n) basis and Sutherland–Hodgman clipping preserves order.
    /// </summary>
    public static List<Float3> ClipFacePolygon(RelicMapBrush brush, List<FixedPlane> planes, int faceIndex)
    {
        var poly = CreateBasePolygon(planes[faceIndex]);
        for (int pj = 0; pj < brush.Faces.Count; pj++)
        {
            if (pj == faceIndex) continue;
            poly = ClipPolygon(poly, planes[pj]);
            if (poly.Count < 3) break;
        }
        return poly;
    }

    /// <summary>
    /// Permanent winding guard: verifies the triangle (i0, i1, i2) agrees with the
    /// outward face normal by the right-hand rule and swaps i1/i2 when inverted.
    /// Degenerate triangles (zero area) are left untouched.
    /// </summary>
    public static void EnsureWinding(List<Float3> verts, ref uint i1, ref uint i2, uint i0, Float3 outwardNormal)
    {
        var g = Float3.Cross(verts[(int)i1] - verts[(int)i0], verts[(int)i2] - verts[(int)i0]);
        if (Float3.LengthSquared(g) < 1e-16f) return; // degenerate — nothing to correct
        if (Float3.Dot(g, outwardNormal) < 0)
        {
            (i1, i2) = (i2, i1);
        }
    }

    /// <summary>Outward-facing planes for a brush (inside of brush is the negative side).</summary>
    public static List<FixedPlane> GetFixedPlanes(RelicMapBrush brush) => FixPlanes(brush);

    private static List<Float3> CreateBasePolygon(FixedPlane plane)
    {
        // Build orthonormal basis on the plane, then a large quad.
        var n = Float3.NormalizeSafe(plane.Normal, Float3.UnitY);
        var up = Math.Abs(n.Z) < 0.9f ? Float3.UnitZ : Float3.UnitY;
        var u = Float3.NormalizeSafe(Float3.Cross(up, n), Float3.UnitX);
        var v = Float3.NormalizeSafe(Float3.Cross(n, u), Float3.UnitZ);
        var origin = n * plane.Dist;
        const float S = 8192f;
        return new List<Float3>
        {
            origin - u * S - v * S,
            origin + u * S - v * S,
            origin + u * S + v * S,
            origin - u * S + v * S,
        };
    }

    private static List<Float3> ClipPolygon(List<Float3> poly, FixedPlane plane)
    {
        const float Eps = 0.05f;
        var outPoly = new List<Float3>(poly.Count + 1);
        if (poly.Count == 0) return outPoly;
        for (int i = 0; i < poly.Count; i++)
        {
            var cur = poly[i];
            var nxt = poly[(i + 1) % poly.Count];
            float dCur = Float3.Dot(plane.Normal, cur) - plane.Dist;
            float dNxt = Float3.Dot(plane.Normal, nxt) - plane.Dist;
            bool inCur = dCur <= Eps;
            bool inNxt = dNxt <= Eps;
            if (inCur) outPoly.Add(cur);
            if (inCur != inNxt)
            {
                float t = dCur / (dCur - dNxt);
                outPoly.Add(cur + (nxt - cur) * t);
            }
        }
        return outPoly;
    }

    /// <summary>
    /// TrenchBroom-compatible UVs. Standard format: dominant-plane projection with
    /// rotation, texel offsets and per-axis scale. Valve220: projection onto the
    /// explicit UV axes (rotation is baked into them by TrenchBroom).
    /// UV space is tiles (1.0 = one texture repetition), matching TB's preview.
    /// </summary>
    internal static Float2 ComputeUV(Float3 quakePoint, FixedPlane plane, RelicMapFace face, Float2 texSize)
    {
        float texW = texSize.X > 0 ? texSize.X : 64f;
        float texH = texSize.Y > 0 ? texSize.Y : 64f;
        float su = face.ScaleU <= 0 ? 1 : face.ScaleU;
        float sv = face.ScaleV <= 0 ? 1 : face.ScaleV;

        if (face.HasValveAxes)
        {
            float u = (Float3.Dot(quakePoint, face.ValveUAxis) + face.OffsetU) / texW / su;
            float v = (Float3.Dot(quakePoint, face.ValveVAxis) + face.OffsetV) / texH / sv;
            return new Float2(u, v);
        }

        var an = new Float3(Math.Abs(plane.Normal.X), Math.Abs(plane.Normal.Y), Math.Abs(plane.Normal.Z));
        float bu, bv;
        if (an.Z >= an.X && an.Z >= an.Y)
        {
            bu = quakePoint.X;
            bv = quakePoint.Y;
        }
        else if (an.X >= an.Y)
        {
            bu = quakePoint.Y;
            bv = quakePoint.Z;
        }
        else
        {
            bu = quakePoint.X;
            bv = quakePoint.Z;
        }

        // TB-style rotation (counter-clockwise in UV space), then scale + texel offset.
        float rad = face.Rotation * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        float ru = bu * cos - bv * sin;
        float rv = bu * sin + bv * cos;
        return new Float2(ru / (texW * su) + face.OffsetU / texW,
                          rv / (texH * sv) + face.OffsetV / texH);
    }
}
