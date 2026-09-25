// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System;
using System.Collections.Generic;

using Prowl.Vector;

namespace Prowl.Runtime.Relic.Map;

/// <summary>
/// Relic geometry validator: diagnoses one-sided / inverted brush faces at the
/// source (mesh generation), so backface culling can stay enabled.
/// </summary>
public static class RelicGeometryValidator
{
    public sealed class MeshIssue
    {
        public int TriangleIndex;
        public string Texture = string.Empty;
        public string Kind = string.Empty; // InvertedWinding | Degenerate | NormalMismatch
        public float Agreement;
        public override string ToString() =>
            $"tri {TriangleIndex} ('{Texture}'): {Kind} (winding/normal agreement {Agreement:F3})";
    }

    public sealed class BrushFaceReport
    {
        public int BrushIndex;
        public int FaceIndex;
        public string Texture = string.Empty;
        /// <summary>Face normal in Prowl space (meters, Y-up).</summary>
        public Float3 Normal;
        /// <summary>Face center in Prowl space.</summary>
        public Float3 FaceCenter;
        /// <summary>Brush center in Prowl space.</summary>
        public Float3 BrushCenter;
        /// <summary>Facing direction: FaceCenter - BrushCenter.</summary>
        public Float3 FacingDirection => FaceCenter - BrushCenter;
        /// <summary>Dot(Normal, FacingDirection). Must be &gt; 0 for outward faces.</summary>
        public float FacingDot;
        public bool FacesOutward => FacingDot > 0;
        public int VertexCount;
        public bool WindingOK;
        public override string ToString() =>
            $"brush {BrushIndex} face {FaceIndex} ('{Texture}'): normal={Normal} center={FaceCenter} " +
            $"brushCenter={BrushCenter} facingDot={FacingDot:F3} outward={FacesOutward} windingOK={WindingOK}";
    }

    /// <summary>
    /// Validates flat built geometry: every triangle's index winding must agree with
    /// its vertex normals by the right-hand rule. Returns one issue per bad triangle.
    /// </summary>
    public static List<MeshIssue> ValidateMesh(RelicBrushBuilder.BuiltGeometry geo)
    {
        var issues = new List<MeshIssue>();
        for (int t = 0; t + 2 < geo.Indices.Length; t += 3)
        {
            uint i0 = geo.Indices[t], i1 = geo.Indices[t + 1], i2 = geo.Indices[t + 2];
            var g = Float3.Cross(geo.Vertices[i1] - geo.Vertices[i0], geo.Vertices[i2] - geo.Vertices[i0]);
            string tex = geo.Textures.Length > (int)i0 ? geo.Textures[i0] : "?";
            if (Float3.LengthSquared(g) < 1e-16f)
            {
                issues.Add(new MeshIssue { TriangleIndex = t / 3, Texture = tex, Kind = "Degenerate", Agreement = 0 });
                continue;
            }
            var n = Float3.Normalize(geo.Normals[i0] + geo.Normals[i1] + geo.Normals[i2]);
            float agree = Float3.Dot(Float3.Normalize(g), n);
            if (agree <= 0)
                issues.Add(new MeshIssue { TriangleIndex = t / 3, Texture = tex, Kind = "InvertedWinding", Agreement = agree });
        }
        return issues;
    }

    /// <summary>
    /// Validates brush faces before meshing: for every face, Dot(FaceNormal,
    /// FaceCenter - BrushCenter) must be &gt; 0, and the clipped polygon winding
    /// must agree with the outward normal.
    /// </summary>
    public static List<BrushFaceReport> ValidateBrushes(
        IReadOnlyList<RelicMapBrush> brushes, float unitScale = RelicBrushBuilder.DefaultUnitScale)
    {
        var reports = new List<BrushFaceReport>();
        for (int bi = 0; bi < brushes.Count; bi++)
        {
            var brush = brushes[bi];
            if (brush.Faces.Count == 0) continue;
            var planes = RelicBrushBuilder.GetFixedPlanes(brush);

            // Brush center: average of face-plane points (Quake space).
            var centerQ = Float3.Zero;
            foreach (var p in planes) centerQ += p.Normal * p.Dist;
            centerQ /= Math.Max(1, planes.Count);
            var centerP = RelicBrushBuilder.QuakeToProwl(centerQ, unitScale);

            for (int fi = 0; fi < brush.Faces.Count; fi++)
            {
                var poly = RelicBrushBuilder.ClipFacePolygon(brush, planes, fi);
                var nProwl = RelicBrushBuilder.QuakeNormalToProwl(planes[fi].Normal);
                var rep = new BrushFaceReport
                {
                    BrushIndex = bi,
                    FaceIndex = fi,
                    Texture = brush.Faces[fi].Texture,
                    Normal = nProwl,
                    BrushCenter = centerP,
                    VertexCount = poly.Count
                };
                if (poly.Count >= 3)
                {
                    var fcQ = Float3.Zero;
                    foreach (var v in poly) fcQ += v;
                    fcQ /= poly.Count;
                    rep.FaceCenter = RelicBrushBuilder.QuakeToProwl(fcQ, unitScale);
                    rep.FacingDot = Float3.Dot(nProwl, rep.FaceCenter - centerP);
                    // Winding: fan (v0, vk, vk+1) geometric normal must agree with nProwl.
                    var g = Float3.Cross(
                        RelicBrushBuilder.QuakeToProwl(poly[1], unitScale) - RelicBrushBuilder.QuakeToProwl(poly[0], unitScale),
                        RelicBrushBuilder.QuakeToProwl(poly[2], unitScale) - RelicBrushBuilder.QuakeToProwl(poly[0], unitScale));
                    rep.WindingOK = Float3.LengthSquared(g) < 1e-16f || Float3.Dot(g, nProwl) > 0;
                }
                reports.Add(rep);
            }
        }
        return reports;
    }

    /// <summary>Logs a full per-face diagnostic to the console.</summary>
    public static void LogBrushReport(IReadOnlyList<RelicMapBrush> brushes, float unitScale = RelicBrushBuilder.DefaultUnitScale)
    {
        foreach (var r in ValidateBrushes(brushes, unitScale))
        {
            if (!r.FacesOutward || !r.WindingOK || r.VertexCount < 3)
                Debug.LogWarning($"[RelicGeo] BAD  {r}");
            else
                Debug.Log($"[RelicGeo] ok   {r}");
        }
    }

    /// <summary>Logs mesh-level winding issues. Empty output means culling-safe.</summary>
    public static void LogMeshIssues(RelicBrushBuilder.BuiltGeometry geo)
    {
        var issues = ValidateMesh(geo);
        if (issues.Count == 0)
        {
            Debug.Log($"[RelicGeo] mesh OK: {geo.Indices.Length / 3} triangles, all windings agree with normals.");
            return;
        }
        foreach (var i in issues)
            Debug.LogWarning($"[RelicGeo] BAD  {i}");
    }
}
