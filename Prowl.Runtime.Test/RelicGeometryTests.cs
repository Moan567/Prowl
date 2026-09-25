// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.AssetImporting;
using Prowl.Runtime.Relic;
using Prowl.Runtime.Relic.Map;
using Prowl.Runtime.Resources;
using Prowl.Vector;

using Xunit;

namespace Prowl.Runtime.Test;

/// <summary>
/// One-sided-geometry regression tests (Relic brush pipeline).
/// Convention under test: a triangle's index winding must agree with its normals
/// by the right-hand rule — dot(normalize(cross(v1-v0, v2-v0)), normal) &gt; 0 —
/// exactly like the engine's own Cube.obj primitive. Backface culling stays ON;
/// the fix lives in mesh generation, not in materials or raster state.
/// </summary>
public class RelicGeometryTests : RuntimeTestBase
{
    private static string SimpleCubePath =>
        Path.Combine(AppContext.BaseDirectory, "Relic", "SimpleCube.map");

    private static float Agreement(Float3 a, Float3 b, Float3 c, Float3 n)
    {
        var g = Float3.Cross(b - a, c - a);
        if (Float3.LengthSquared(g) < 1e-12f) return float.NaN; // degenerate
        return Float3.Dot(Float3.Normalize(g), n);
    }

    // Ground truth: the engine's own cube primitive. Whatever winding/normal
    // relationship IT has is the convention brush output must match, because
    // both render through the same Cull Back raster state.
    [Fact]
    public void EngineConvention_CubeObj_WindingAgreesWithNormals()
    {
        using var stream = EmbeddedResources.GetStream("Assets/Defaults/Cube.obj");
        var result = new ModelImporter().Import(stream, "Cube.obj",
            new ModelImporterSettings { ImportMaterials = false, ImportAnimations = false });
        Assert.NotEmpty(result.Meshes);

        int checkedTris = 0;
        foreach (var mesh in result.Meshes)
        {
            var verts = mesh.Vertices;
            var normals = mesh.Normals;
            var indices = mesh.Indices;
            Assert.True(indices.Length % 3 == 0);
            for (int t = 0; t < indices.Length; t += 3)
            {
                var n = Float3.Normalize(normals[indices[t]] + normals[indices[t + 1]] + normals[indices[t + 2]]);
                float agree = Agreement(verts[indices[t]], verts[indices[t + 1]], verts[indices[t + 2]], n);
                if (float.IsNaN(agree)) continue;
                Assert.True(agree > 0.99f, $"Imported cube tri {t / 3} disagrees with its normals ({agree}).");
                checkedTris++;
            }
        }
        Assert.True(checkedTris >= 12);
    }

    [Fact]
    public void SimpleCube_Builds12Triangles()
    {
        var map = RelicMapParser.Load(SimpleCubePath);
        var brush = Assert.Single(map.Brushes);
        Assert.Equal(6, brush.Faces.Count);
        var geo = RelicBrushBuilder.BuildBrushes(map.Brushes);
        Assert.Equal(12, geo.Indices.Length / 3);
        // One texture -> one material group, 24 verts (4 per face, split normals).
        var groups = RelicBrushBuilder.GroupByTexture(geo);
        Assert.Single(groups);
        Assert.Equal(12, groups[0].Indices.Count / 3);
    }

    [Fact]
    public void SimpleCube_NormalsPointOutward()
    {
        var map = RelicMapParser.Load(SimpleCubePath);
        var geo = RelicBrushBuilder.BuildBrushes(map.Brushes);
        var center = geo.Bounds.Center;
        for (int i = 0; i < geo.Vertices.Length; i++)
        {
            float facing = Float3.Dot(geo.Normals[i], geo.Vertices[i] - center);
            Assert.True(facing > 0, $"Vertex {i} normal does not point outward (dot={facing}).");
        }
    }

    [Fact]
    public void SimpleCube_WindingAgreesWithNormals()
    {
        var map = RelicMapParser.Load(SimpleCubePath);
        var geo = RelicBrushBuilder.BuildBrushes(map.Brushes);
        for (int t = 0; t < geo.Indices.Length; t += 3)
        {
            uint i0 = geo.Indices[t], i1 = geo.Indices[t + 1], i2 = geo.Indices[t + 2];
            var n = Float3.Normalize(geo.Normals[i0] + geo.Normals[i1] + geo.Normals[i2]);
            float agree = Agreement(geo.Vertices[i0], geo.Vertices[i1], geo.Vertices[i2], n);
            Assert.True(agree > 0.99f,
                $"Brush tri {t / 3} is inverted: winding disagrees with face normal ({agree}). " +
                "This triangle is culled from outside with Cull Back enabled.");
        }
    }

    [Fact]
    public void SimpleCube_ValidatorReportsClean()
    {
        var map = RelicMapParser.Load(SimpleCubePath);
        var geo = RelicBrushBuilder.BuildBrushes(map.Brushes);
        var meshIssues = RelicGeometryValidator.ValidateMesh(geo);
        Assert.Empty(meshIssues);
        var brushReports = RelicGeometryValidator.ValidateBrushes(map.Brushes);
        Assert.Equal(6, brushReports.Count);
        Assert.All(brushReports, r => Assert.True(r.FacesOutward,
            $"Face {r.FaceIndex} ('{r.Texture}') normal does not point away from brush center."));
    }

    [Fact]
    public void SimpleCube_SceneBuildSpawnsRoomAndPlayer()
    {
        var scene = CreateScene();
        var map = RelicMapParser.Load(SimpleCubePath);
        var report = RelicSceneBuilder.Build(map, scene, new RelicBuildOptions());
        Assert.Equal(12, report.WorldTriangles);
        Assert.True(report.HasPlayerSpawn);
    }

    [Fact]
    public void CoordinateConversion_PreservesWindingHandedness()
    {
        // QuakeToProwl must be a proper rotation (det +1), not a mirror:
        // a CCW triangle in Quake space must stay CCW in Prowl space.
        var a = new Float3(0, 0, 0);
        var b = new Float3(32, 0, 0);
        var c = new Float3(32, 32, 0); // +z geometric normal (CCW from +z)
        var pa = RelicBrushBuilder.QuakeToProwl(a);
        var pb = RelicBrushBuilder.QuakeToProwl(b);
        var pc = RelicBrushBuilder.QuakeToProwl(c);
        var nIn = Float3.Normalize(Float3.Cross(b - a, c - a));
        var nOut = Float3.Normalize(Float3.Cross(pb - pa, pc - pa));
        // Rotation preserves the normal direction mapping (x,y,z)->(x,z,-y).
        var mapped = RelicBrushBuilder.QuakeNormalToProwl(nIn);
        Assert.True(Float3.Dot(nOut, mapped) > 0.99f,
            "Coordinate conversion flips winding handedness; triangle order must be corrected.");
    }
}
