// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Relic;
using Prowl.Runtime.Relic.Map;
using Prowl.Vector;

using Xunit;

namespace Prowl.Runtime.Test;

/// <summary>
/// Texture pipeline tests: TrenchBroom texture names -> files -> materials + UVs.
/// Filesystem resolution is GPU-free; image decoding/material paths are covered
/// by a 1x1 PNG end-to-end test. Missing textures must degrade to the
/// procedural fallback and be reported, never throw.
/// </summary>
public class RelicTextureTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "RelicTexTest_" + Guid.NewGuid().ToString("N"));

    // Minimal valid 1x1 RGBA PNG (transparent).
    private static readonly byte[] TinyPng = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    public void Dispose()
    {
        try { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string WriteFile(string relative, byte[]? bytes = null)
    {
        string full = Path.Combine(_tmp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes ?? new byte[] { 0x00 });
        return full;
    }

    [Fact]
    public void Resolve_FindsPngByBareName()
    {
        string root = Path.Combine(_tmp, "tex");
        WriteFile(Path.Combine("tex", "brick.png"));
        WriteFile(Path.Combine("tex", "metal.jpg"));
        Assert.Equal(Path.Combine(root, "brick.png"), RelicTextureLibrary.ResolveTexturePath("brick", new[] { root }));
        Assert.Equal(Path.Combine(root, "metal.jpg"), RelicTextureLibrary.ResolveTexturePath("metal", new[] { root }));
    }

    [Fact]
    public void Resolve_MissingReturnsNull()
    {
        string root = Path.Combine(_tmp, "empty");
        Directory.CreateDirectory(root);
        Assert.Null(RelicTextureLibrary.ResolveTexturePath("brick", new[] { root }));
        Assert.Null(RelicTextureLibrary.ResolveTexturePath("", new[] { root }));
    }

    [Fact]
    public void LoadForMap_NoRoots_AllMissingNoThrow()
    {
        var map = new RelicMapData();
        var brush = new RelicMapBrush();
        brush.Faces.Add(new RelicMapFace { Texture = "brick" });
        brush.Faces.Add(new RelicMapFace { Texture = "clip" }); // nodraw special: skipped silently
        map.Brushes.Add(brush);

        var set = RelicTextureLibrary.LoadForMap(map, Array.Empty<string>());
        Assert.Contains("brick", set.Missing);
        Assert.DoesNotContain("clip", set.Missing);
        Assert.Equal(new Float2(64, 64), set.GetSize("brick"));
        Assert.False(set.TryGetTexture("brick", out _));
    }

    [Fact]
    public void TextureSet_LoadsRealPng_EndToEnd()
    {
        WriteFile(Path.Combine("tex", "tiny.png"), TinyPng);
        var set = new RelicTextureSet();
        set.SearchRoots.Add(Path.Combine(_tmp, "tex"));

        Assert.True(set.TryGetTexture("tiny", out var tex));
        Assert.True(tex.IsValid());
        Assert.Equal(new Float2(1, 1), set.GetSize("tiny"));

        var mat = RelicMaterialLibrary.Get("tiny", set);
        Assert.True(mat.IsValid());
        Assert.True(RelicMaterialLibrary.IsTextured("tiny", set));
        Assert.DoesNotContain("tiny", set.Missing);
    }

    [Fact]
    public void Material_FallsBackWhenMissing()
    {
        var set = new RelicTextureSet(); // no roots
        var mat = RelicMaterialLibrary.Get("ghost_tex", set);
        Assert.True(mat.IsValid());
        Assert.False(RelicMaterialLibrary.IsTextured("ghost_tex", set));
        Assert.Contains("ghost_tex", set.Missing);
    }

    private static RelicMapFace StdFace()
    {
        return new RelicMapFace
        {
            Texture = "brick", OffsetU = 0, OffsetV = 0, Rotation = 0, ScaleU = 1, ScaleV = 1
        };
    }

    private static RelicBrushBuilder.FixedPlane FloorPlane()
    {
        return new RelicBrushBuilder.FixedPlane { Normal = new Float3(0, 0, 1), Dist = 0 };
    }

    [Fact]
    public void UV_StandardMapsTexelDensity()
    {
        // 64px texture at scale 1: 64 world units per tile.
        var uv = RelicBrushBuilder.ComputeUV(new Float3(64, 32, 0), FloorPlane(), StdFace(), new Float2(64, 64));
        Assert.Equal(1f, uv.X, 4);
        Assert.Equal(0.5f, uv.Y, 4);
    }

    [Fact]
    public void UV_ScaleHalvesSpan_OffsetShiftsByTexels()
    {
        var f = StdFace();
        f.ScaleU = 2; f.ScaleV = 2;
        var uv = RelicBrushBuilder.ComputeUV(new Float3(64, 64, 0), FloorPlane(), f, new Float2(64, 64));
        Assert.Equal(0.5f, uv.X, 4);

        var g = StdFace();
        g.OffsetU = 32; g.OffsetV = 16; // texels -> +0.5 / +0.25 tiles
        var uv2 = RelicBrushBuilder.ComputeUV(new Float3(0, 0, 0), FloorPlane(), g, new Float2(64, 64));
        Assert.Equal(0.5f, uv2.X, 4);
        Assert.Equal(0.25f, uv2.Y, 4);
    }

    [Fact]
    public void UV_Rotation90MapsAccordingly()
    {
        var f = StdFace();
        f.Rotation = 90;
        var uv = RelicBrushBuilder.ComputeUV(new Float3(64, 0, 0), FloorPlane(), f, new Float2(64, 64));
        Assert.Equal(0f, uv.X, 4);
        Assert.Equal(1f, uv.Y, 4);
    }

    [Fact]
    public void UV_Valve220ProjectsOntoAxes()
    {
        var f = StdFace();
        f.HasValveAxes = true;
        f.ValveUAxis = new Float3(0, 1, 0);
        f.ValveVAxis = new Float3(0, 0, 1);
        var uv = RelicBrushBuilder.ComputeUV(new Float3(0, 64, 128), FloorPlane(), f, new Float2(64, 64));
        Assert.Equal(1f, uv.X, 4);
        Assert.Equal(2f, uv.Y, 4);
    }

    [Fact]
    public void UV_DegenerateSizeFallsBack_NoNaN()
    {
        var uv = RelicBrushBuilder.ComputeUV(new Float3(64, 64, 0), FloorPlane(), StdFace(), Float2.Zero);
        Assert.False(float.IsNaN(uv.X) || float.IsNaN(uv.Y));
    }

    [Fact]
    public void Builder_WithSizeProvider_KeepsTriangles_ScalesUVs()
    {
        var map = RelicMapParser.Parse("{\"classname\" \"worldspawn\" " +
            "{ ( -64 -64 0 ) ( 64 -64 0 ) ( 64 64 0 ) brick 0 0 0 1 1 " +
            "( -64 -64 64 ) ( -64 64 64 ) ( 64 64 64 ) brick 0 0 0 1 1 " +
            "( -64 -64 0 ) ( -64 -64 64 ) ( 64 -64 64 ) brick 0 0 0 1 1 " +
            "( -64 64 0 ) ( 64 64 0 ) ( 64 64 64 ) brick 0 0 0 1 1 " +
            "( -64 -64 0 ) ( -64 0 64 ) ( -64 64 0 ) brick 0 0 0 1 1 " +
            "( 64 -64 0 ) ( 64 64 0 ) ( 64 0 64 ) brick 0 0 0 1 1 } }");
        // 128px textures -> 128-unit box spans exactly 1 tile.
        var geo = RelicBrushBuilder.BuildBrushes(map.Brushes, 1f / 32f, _ => new Float2(128, 128));
        Assert.Equal(12, geo.Indices.Length / 3);
        foreach (var uv in geo.UVs)
            Assert.True(Math.Abs(uv.X) <= 1.001f && Math.Abs(uv.Y) <= 1.001f);
    }

    [Fact]
    public void TrenchBroomDiscovery_FindsRepoLayout()
    {
        // <tmp>/TrenchBroom-x/TrenchBroom.exe + <tmp>/RELIC_engine/Assets/{Relic.fgd,Textures,Maps}
        string proj = Path.Combine(_tmp, "game");
        Directory.CreateDirectory(proj);
        WriteFile(Path.Combine("TrenchBroom-x", "TrenchBroom.exe"));
        WriteFile(Path.Combine("RELIC_engine", "Assets", "Relic.fgd"));
        Directory.CreateDirectory(Path.Combine(_tmp, "RELIC_engine", "Assets", "Textures"));
        Directory.CreateDirectory(Path.Combine(_tmp, "RELIC_engine", "Assets", "Maps"));

        string? exe = RelicTrenchBroom.FindExecutable(proj);
        Assert.NotNull(exe);
        Assert.EndsWith("TrenchBroom.exe", exe);

        string? assets = RelicTrenchBroom.FindEngineAssetsDir(proj);
        Assert.NotNull(assets);
        Assert.Equal("Relic.fgd", Path.GetFileName(RelicTrenchBroom.FindFgd(proj)));
        Assert.EndsWith("Textures", RelicTrenchBroom.FindTextureDir(proj));
        Assert.EndsWith("Maps", RelicTrenchBroom.FindMapsDir(proj));
        Assert.EndsWith("RELIC_engine", RelicTrenchBroom.FindGamePath(proj));
    }
}
