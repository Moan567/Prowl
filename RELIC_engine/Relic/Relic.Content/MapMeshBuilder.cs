namespace Relic.Content;

using Relic.Framework;

public static class MapMeshBuilder
{
    private const float ClipEpsilon = 0.1f;
    private const float LargeSize = 4096f;

    private struct Plane
    {
        public Vector3 Normal;
        public float Distance;
        public string Texture;
        public Vector2 Scale;
        public float Rotation;
        public int ShiftX;
        public int ShiftY;
    }

    public static GroupedMeshData BuildGrouped(MapData map)
    {
        var grouped = new GroupedMeshData();
        var dict = new Dictionary<string, MeshGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var brush in map.Brushes)
        {
            BuildBrushGrouped(brush, dict);
        }

        foreach (var kv in dict)
            grouped.Groups.Add(kv.Value);

        return grouped;
    }

    public static MeshData Build(MapData map)
    {
        var grouped = BuildGrouped(map);
        var merged = new MeshData();
        uint baseIndex = 0;
        foreach (var g in grouped.Groups)
        {
            foreach (var v in g.Vertices) merged.Vertices.Add(v);
            foreach (var idx in g.Indices) merged.Indices.Add(idx + baseIndex);
            baseIndex += (uint)g.Vertices.Count;
        }
        return merged;
    }

    private static void BuildBrushGrouped(MapBrush brush, Dictionary<string, MeshGroup> dict)
    {
        if (brush.Faces.Count < 4) return; // Need at least tetrahedron

        // Build planes
        var planes = new Plane[brush.Faces.Count];
        var centroid = Vector3.Zero;
        int ptCount = 0;
        for (int i = 0; i < brush.Faces.Count; i++)
        {
            var f = brush.Faces[i];
            // Normal already computed, distance from origin
            float dist = Vector3.Dot(f.Normal, f.Vertices[0]);
            planes[i] = new Plane
            {
                Normal = f.Normal,
                Distance = dist,
                Texture = string.IsNullOrWhiteSpace(f.Texture) ? "default" : f.Texture,
                Scale = f.TextureScale,
                Rotation = f.TextureRotation,
                ShiftX = f.TextureShiftX,
                ShiftY = f.TextureShiftY
            };
            foreach (var v in f.Vertices) { centroid += v; ptCount++; }
        }
        if (ptCount > 0) centroid /= ptCount;

        // For each plane, clip large quad against all other planes
        for (int i = 0; i < planes.Length; i++)
        {
            var plane = planes[i];
            var poly = CreateLargeQuad(plane);

            bool culled = false;
            for (int j = 0; j < planes.Length; j++)
            {
                if (i == j) continue;
                // Determine which side is inside using centroid
                float centroidDist = Vector3.Dot(planes[j].Normal, centroid) - planes[j].Distance;
                bool keepNegative = centroidDist <= 0; // centroid behind plane
                poly = ClipPolygon(poly, planes[j].Normal, planes[j].Distance, keepNegative);
                if (poly.Count < 3) { culled = true; break; }
            }

            if (culled || poly.Count < 3) continue;

            // Remove duplicate / collinear points
            poly = CleanPolygon(poly);
            if (poly.Count < 3) continue;

            // Generate vertices for this polygon
            string tex = plane.Texture;
            if (!dict.TryGetValue(tex, out var group))
            {
                group = new MeshGroup { TextureName = tex };
                dict[tex] = group;
            }

            uint baseVertex = (uint)group.Vertices.Count;

            // Compute UVs and tangents per vertex (for normal mapping)
            for (int v = 0; v < poly.Count; v++)
            {
                var pos = poly[v];
                var (uv, tangent) = ComputeUVAndTangent(pos, plane);
                // Bitangent can be derived in shader via cross(N, T)
                group.Vertices.Add(new MeshVertex(pos, plane.Normal, uv, tangent));
            }

            // Triangulate fan
            for (int k = 1; k < poly.Count - 1; k++)
            {
                group.Indices.Add(baseVertex);
                group.Indices.Add(baseVertex + (uint)(k + 1));
                group.Indices.Add(baseVertex + (uint)k);
                // Note winding: ensure CCW when looking along normal
                // Our clipping preserves order, but fan above may need flipping depending on plane orientation
                // Test with cube: if normals appear inverted, swap k and k+1
            }
        }
    }

    private static List<Vector3> CreateLargeQuad(Plane plane)
    {
        Vector3 center = plane.Normal * plane.Distance;

        // Build orthonormal basis on plane
        Vector3 up = MathF.Abs(plane.Normal.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX;
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, plane.Normal));
        up = Vector3.Cross(plane.Normal, right); // ensure orthonormal

        right *= LargeSize;
        up *= LargeSize;

        return new List<Vector3>(4)
        {
            center - right - up,
            center + right - up,
            center + right + up,
            center - right + up
        };
    }

    private static List<Vector3> ClipPolygon(List<Vector3> poly, Vector3 normal, float dist, bool keepNegative)
    {
        if (poly.Count == 0) return poly;
        var result = new List<Vector3>();
        int count = poly.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 cur = poly[i];
            Vector3 nxt = poly[(i + 1) % count];

            float dCur = Vector3.Dot(normal, cur) - dist;
            float dNxt = Vector3.Dot(normal, nxt) - dist;

            bool curInside = keepNegative ? dCur <= ClipEpsilon : dCur >= -ClipEpsilon;
            bool nxtInside = keepNegative ? dNxt <= ClipEpsilon : dNxt >= -ClipEpsilon;

            if (curInside && nxtInside)
            {
                result.Add(nxt);
            }
            else if (curInside && !nxtInside)
            {
                // leaving -> add intersection
                float t = dCur / (dCur - dNxt);
                var inter = cur + (nxt - cur) * t;
                result.Add(inter);
            }
            else if (!curInside && nxtInside)
            {
                float t = dCur / (dCur - dNxt);
                var inter = cur + (nxt - cur) * t;
                result.Add(inter);
                result.Add(nxt);
            }
            // else both outside -> add nothing
        }
        return result;
    }

    private static List<Vector3> CleanPolygon(List<Vector3> poly)
    {
        if (poly.Count < 3) return poly;
        var cleaned = new List<Vector3>();
        for (int i = 0; i < poly.Count; i++)
        {
            var cur = poly[i];
            var nxt = poly[(i + 1) % poly.Count];
            if ((nxt - cur).LengthSquared() > 0.01f)
                cleaned.Add(cur);
        }
        return cleaned;
    }

    private static Vector2 ComputeUV(Vector3 pos, Plane plane)
    {
        // Simple world-space UV, scaled 1/64, with rotation/shift
        // Choose tangent basis per dominant axis
        Vector3 tangent, bitangent;
        float absX = MathF.Abs(plane.Normal.X);
        float absY = MathF.Abs(plane.Normal.Y);
        float absZ = MathF.Abs(plane.Normal.Z);

        if (absZ >= absX && absZ >= absY)
        {
            tangent = Vector3.UnitX;
            bitangent = Vector3.UnitY;
        }
        else if (absX >= absY)
        {
            tangent = Vector3.UnitZ;
            bitangent = Vector3.UnitY;
        }
        else
        {
            tangent = Vector3.UnitX;
            bitangent = Vector3.UnitZ;
        }

        // Apply rotation
        if (plane.Rotation != 0)
        {
            float rad = plane.Rotation * MathF.PI / 180f;
            float c = MathF.Cos(rad), s = MathF.Sin(rad);
            var t = tangent;
            tangent = new Vector3(t.X * c - t.Y * s, t.X * s + t.Y * c, t.Z);
            bitangent = new Vector3(bitangent.X * c - bitangent.Y * s, bitangent.X * s + bitangent.Y * c, bitangent.Z);
        }

        float scaleX = plane.Scale.X != 0 ? plane.Scale.X : 1f;
        float scaleY = plane.Scale.Y != 0 ? plane.Scale.Y : 1f;

        float u = Vector3.Dot(pos, tangent) / (64f * scaleX) + plane.ShiftX / 64f;
        float v = Vector3.Dot(pos, bitangent) / (64f * scaleY) + plane.ShiftY / 64f;

        return new Vector2(u, v);
    }

    private static (Vector2 uv, Vector3 tangent) ComputeUVAndTangent(Vector3 pos, Plane plane)
    {
        Vector3 tangent, bitangent;
        float absX = MathF.Abs(plane.Normal.X);
        float absY = MathF.Abs(plane.Normal.Y);
        float absZ = MathF.Abs(plane.Normal.Z);
        if (absZ >= absX && absZ >= absY) { tangent = Vector3.UnitX; bitangent = Vector3.UnitY; }
        else if (absX >= absY) { tangent = Vector3.UnitZ; bitangent = Vector3.UnitY; }
        else { tangent = Vector3.UnitX; bitangent = Vector3.UnitZ; }
        if (plane.Rotation != 0)
        {
            float rad = plane.Rotation * MathF.PI / 180f;
            float c = MathF.Cos(rad), s = MathF.Sin(rad);
            var t = tangent;
            tangent = new Vector3(t.X * c - t.Y * s, t.X * s + t.Y * c, t.Z);
            bitangent = new Vector3(bitangent.X * c - bitangent.Y * s, bitangent.X * s + bitangent.Y * c, bitangent.Z);
        }
        tangent.Normalize();
        float scaleX = plane.Scale.X != 0 ? plane.Scale.X : 1f;
        float scaleY = plane.Scale.Y != 0 ? plane.Scale.Y : 1f;
        float u = Vector3.Dot(pos, tangent) / (64f * scaleX) + plane.ShiftX / 64f;
        float v = Vector3.Dot(pos, bitangent) / (64f * scaleY) + plane.ShiftY / 64f;
        return (new Vector2(u, v), tangent);
    }
}
