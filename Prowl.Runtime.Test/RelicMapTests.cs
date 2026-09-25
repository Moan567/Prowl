// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Relic;
using Prowl.Runtime.Relic.Map;
using Prowl.Runtime.Resources;

using Xunit;

namespace Prowl.Runtime.Test;

/// <summary>Tests for the RELIC pipeline: .map (source of truth) -> scene.</summary>
public class RelicMapTests : RuntimeTestBase
{
    private const string SampleMap = """
        {
        "classname" "worldspawn"
        {
        ( -64 -64 -16 ) ( -64 -63 -16 ) ( -64 -64 0 ) brick 0 0 0 1 1
        ( 64 64 -16 ) ( 64 64 -15 ) ( 64 65 -16 ) brick 0 0 0 1 1
        ( -64 -64 -16 ) ( -63 -64 -16 ) ( -64 -63 -16 ) floor 0 0 0 1 1
        ( -64 -64 128 ) ( -64 -63 128 ) ( -63 -64 128 ) ceiling 0 0 0 1 1
        ( -64 -64 -16 ) ( -64 -64 128 ) ( -63 -64 -16 ) wall 0 0 0 1 1
        ( 64 64 -16 ) ( 64 64 128 ) ( 65 64 -16 ) wall 0 0 0 1 1
        }
        }
        {
        "classname" "player_start"
        "origin" "0 0 32"
        "angle" "90"
        }
        {
        "classname" "light"
        "origin" "0 0 96"
        "color" "255 220 180"
        "intensity" "800"
        "radius" "512"
        }
        {
        "classname" "enemy_soldier"
        "origin" "128 0 32"
        "health" "100"
        }
        {
        "classname" "trigger_once"
        "target" "door1"
        {
        ( 0 0 0 ) ( 0 1 0 ) ( 0 0 1 ) trigger 0 0 0 1 1
        ( 64 64 64 ) ( 64 64 65 ) ( 64 65 64 ) trigger 0 0 0 1 1
        ( 0 0 0 ) ( 1 0 0 ) ( 0 1 0 ) trigger 0 0 0 1 1
        ( 0 0 64 ) ( 0 1 64 ) ( 1 0 64 ) trigger 0 0 0 1 1
        ( 0 0 0 ) ( 0 0 1 ) ( 1 0 0 ) trigger 0 0 0 1 1
        ( 64 0 0 ) ( 64 0 1 ) ( 65 0 0 ) trigger 0 0 0 1 1
        }
        }
        """;

    [Fact]
    public void Parser_ReadsWorldspawnAndEntities()
    {
        var map = RelicMapParser.Parse(SampleMap);
        Assert.True(map.Brushes.Count > 0);
        Assert.Contains(map.Entities, e => e.ClassName == "player_start");
        Assert.Contains(map.Entities, e => e.ClassName == "light");
        Assert.Contains(map.Entities, e => e.ClassName == "enemy_soldier");
        var trigger = map.Entities.First(e => e.ClassName == "trigger_once");
        Assert.Equal("door1", trigger.GetString("target"));
        Assert.True(trigger.Brushes.Count > 0);
    }

    [Fact]
    public void BrushBuilder_CubeProducesTriangles()
    {
        var map = RelicMapParser.Parse(SampleMap);
        var worldBrushes = map.Brushes.Where(b => string.IsNullOrEmpty(b.OwnerClassName)).ToList();
        var geo = RelicBrushBuilder.BuildBrushes(worldBrushes);
        Assert.True(geo.Indices.Length >= 12); // at least a few triangles survived clipping
        Assert.Equal(geo.Vertices.Length, geo.Normals.Length);
        Assert.Equal(geo.Vertices.Length, geo.UVs.Length);
        var groups = RelicBrushBuilder.GroupByTexture(geo);
        Assert.NotEmpty(groups);
    }

    [Fact]
    public void Validator_FlagsMissingPlayerStart()
    {
        var map = RelicMapParser.Parse("""
            { "classname" "worldspawn"
            { ( 0 0 0 ) ( 0 1 0 ) ( 0 0 1 ) wall 0 0 0 1 1
              ( 64 64 64 ) ( 64 64 65 ) ( 64 65 64 ) wall 0 0 0 1 1
              ( 0 0 0 ) ( 1 0 0 ) ( 0 1 0 ) wall 0 0 0 1 1
              ( 0 0 64 ) ( 0 1 64 ) ( 1 0 64 ) wall 0 0 0 1 1
              ( 0 0 0 ) ( 0 0 1 ) ( 1 0 0 ) wall 0 0 0 1 1
              ( 64 0 0 ) ( 64 0 1 ) ( 65 0 0 ) wall 0 0 0 1 1 } }
            """);
        var issues = RelicMapValidator.Validate(map);
        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("player_start"));
    }

    [Fact]
    public void SceneBuilder_SpawnsWorldAndEntities()
    {
        var scene = CreateScene();
        var map = RelicMapParser.Parse(SampleMap);
        var report = RelicSceneBuilder.Build(map, scene, new RelicBuildOptions());
        Assert.True(report.WorldTriangles > 0);
        Assert.True(report.HasPlayerSpawn);
        Assert.NotEmpty(scene.FindObjectsOfType<RelicPlayerSpawn>());
        Assert.NotEmpty(scene.FindObjectsOfType<RelicEnemy>());
        Assert.NotEmpty(scene.FindObjectsOfType<PointLight>());
        Assert.NotEmpty(scene.FindObjectsOfType<RelicTrigger>());
        Assert.NotEmpty(scene.FindObjectsOfType<RelicPlayerController>());
    }

    [Fact]
    public void CoordinateConversion_IsDocumented()
    {
        // Quake (inches, Z-up) -> Prowl (meters, Y-up): (x, y, z) -> (x, z, -y) * scale.
        var p = RelicBrushBuilder.QuakeToProwl(new Prowl.Vector.Float3(32, 64, 96), 1f / 32f);
        Assert.Equal(1f, p.X, 4);
        Assert.Equal(3f, p.Y, 4);
        Assert.Equal(-2f, p.Z, 4);
    }
}
