// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Vector;

namespace Prowl.Runtime.Relic.Map;

/// <summary>
/// Normal visualization for brush-geometry diagnosis. Draws gizmo arrows so every
/// generated face can be verified by eye: arrows must point AWAY from the brush
/// interior on every face, inside rooms and outside.
/// Colors: green = outward (correct), red = inward (inverted — fix generation).
/// </summary>
public static class RelicGeometryDebug
{
    /// <summary>Draws one arrow per brush face plane (normal from the plane point).</summary>
    public static void DrawFaceNormals(
        IReadOnlyList<RelicMapBrush> brushes,
        float unitScale = RelicBrushBuilder.DefaultUnitScale,
        float length = 0.5f)
    {
        foreach (var brush in brushes)
        {
            if (brush.Faces.Count == 0) continue;
            var planes = RelicBrushBuilder.GetFixedPlanes(brush);
            var centerQ = Float3.Zero;
            foreach (var p in planes) centerQ += p.Normal * p.Dist;
            centerQ /= Math.Max(1, planes.Count);

            for (int fi = 0; fi < brush.Faces.Count; fi++)
            {
                var nProwl = RelicBrushBuilder.QuakeNormalToProwl(planes[fi].Normal);
                var origin = RelicBrushBuilder.QuakeToProwl(planes[fi].Normal * planes[fi].Dist, unitScale);
                var centerP = RelicBrushBuilder.QuakeToProwl(centerQ, unitScale);
                bool outward = Float3.Dot(nProwl, origin - centerP) > 0;
                var color = outward ? Color.Green : Color.Red;
                Debug.DrawLine(origin, origin + nProwl * length, color);
            }
        }
    }

    /// <summary>Draws the clipped polygon edges plus the face normal from the polygon centroid.</summary>
    public static void DrawPolygonNormals(
        IReadOnlyList<RelicMapBrush> brushes,
        float unitScale = RelicBrushBuilder.DefaultUnitScale,
        float length = 0.5f)
    {
        foreach (var brush in brushes)
        {
            if (brush.Faces.Count == 0) continue;
            var planes = RelicBrushBuilder.GetFixedPlanes(brush);
            var centerQ = Float3.Zero;
            foreach (var p in planes) centerQ += p.Normal * p.Dist;
            centerQ /= Math.Max(1, planes.Count);
            var centerP = RelicBrushBuilder.QuakeToProwl(centerQ, unitScale);

            for (int fi = 0; fi < brush.Faces.Count; fi++)
            {
                var poly = RelicBrushBuilder.ClipFacePolygon(brush, planes, fi);
                if (poly.Count < 3) continue;
                var nProwl = RelicBrushBuilder.QuakeNormalToProwl(planes[fi].Normal);

                Float3 centroid = Float3.Zero;
                for (int k = 0; k < poly.Count; k++)
                {
                    var a = RelicBrushBuilder.QuakeToProwl(poly[k], unitScale);
                    var b = RelicBrushBuilder.QuakeToProwl(poly[(k + 1) % poly.Count], unitScale);
                    centroid += a;
                    Debug.DrawLine(a, b, new Color(0.4f, 0.7f, 1f, 1f));
                }
                centroid /= poly.Count;
                bool outward = Float3.Dot(nProwl, centroid - centerP) > 0;
                var color = outward ? Color.Green : Color.Red;
                Debug.DrawLine(centroid, centroid + nProwl * length, color);
            }
        }
    }

    /// <summary>Draws per-vertex normals of built geometry (every stride-th vertex).</summary>
    public static void DrawTriangleNormals(
        RelicBrushBuilder.BuiltGeometry geo,
        int stride = 4,
        float length = 0.25f)
    {
        if (stride < 1) stride = 1;
        for (int i = 0; i < geo.Vertices.Length; i += stride)
        {
            var n = geo.Normals[i];
            if (Float3.LengthSquared(n) < 1e-12f) continue;
            Debug.DrawLine(geo.Vertices[i], geo.Vertices[i] + Float3.Normalize(n) * length, Color.Yellow);
        }
    }
}
