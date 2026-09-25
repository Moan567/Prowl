// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Relic;
using Prowl.Runtime.Relic.Map;
using Prowl.Runtime.Resources;
using Prowl.Vector;

using Xunit;

namespace Prowl.Runtime.Test;

/// <summary>
/// RelicFog is the single knob for fog: it writes analytic Scene.Fog and keeps
/// a volumetric ray-march effect in sync on every camera. No manual effect or
/// volume setup, no density double-add.
/// </summary>
public class RelicFogTests : RuntimeTestBase
{
    private RelicFog AddFog(Scene scene)
    {
        var go = CreateGameObject("Fog");
        scene.Add(go);
        return go.AddComponent<RelicFog>();
    }

    private static Camera AddCamera(Scene scene, string name = "Cam")
    {
        var go = new GameObject(name);
        scene.Add(go);
        return go.AddComponent<Camera>();
    }

    [Fact]
    public void Apply_WritesSceneFog()
    {
        var scene = CreateScene();
        var fog = AddFog(scene);
        fog.Mode = RelicFog.RelicFogMode.Linear;
        fog.FogColor = new Color(1f, 0f, 0f, 1f);
        fog.Density = 0.05f;
        fog.FogStart = 10f;
        fog.FogEnd = 50f;
        fog.Volumetric = false;
        fog.Apply();

        Assert.Equal(Scene.FogParams.FogMode.Linear, scene.Fog.Mode);
        Assert.Equal(new Color(1f, 0f, 0f, 1f), scene.Fog.Color);
        Assert.Equal(0.05f, scene.Fog.Density, 5);
        Assert.Equal(10f, scene.Fog.Start, 4);
        Assert.Equal(50f, scene.Fog.End, 4);
    }

    [Fact]
    public void Apply_ClampsNegativeDensity_AndEndAfterStart()
    {
        var scene = CreateScene();
        var fog = AddFog(scene);
        fog.Density = -1f;
        fog.FogStart = 30f;
        fog.FogEnd = 5f;
        fog.Volumetric = false;
        fog.Apply();

        Assert.Equal(0f, scene.Fog.Density, 5);
        Assert.True(scene.Fog.End > scene.Fog.Start);
    }

    [Fact]
    public void Volumetric_AddsEffectOnce_WithPushedValues()
    {
        var scene = CreateScene();
        var cam = AddCamera(scene);
        var fog = AddFog(scene);
        fog.VolumetricDensity = 0.03f;
        fog.VolumetricScattering = 0.7f;
        fog.VolumetricMaxDistance = 150f;
        fog.VolumetricAmbientIntensity = 0.5f;

        fog.Apply();
        fog.Apply(); // idempotent: no duplicate effects

        var effects = cam.Effects.OfType<VolumetricFogEffect>().ToList();
        var effect = Assert.Single(effects);
        Assert.Equal(0.03f, effect.GlobalDensity, 5);
        Assert.Equal(0.7f, effect.Scattering, 4);
        Assert.Equal(150f, effect.MaxDistance, 3);
        Assert.Equal(0.5f, effect.AmbientIntensity, 4);
        Assert.Equal(fog.FogColor, effect.GlobalColorTint);
        Assert.True(fog.AutoVolumetric);
    }

    [Fact]
    public void VolumetricOff_RemovesAutoAdded_KeepsManual()
    {
        var scene = CreateScene();
        var cam = AddCamera(scene);
        var fog = AddFog(scene);

        fog.Apply();
        Assert.Single(cam.Effects.OfType<VolumetricFogEffect>());

        // A user-added effect is adopted (synced), never duplicated...
        var manual = new VolumetricFogEffect();
        cam.Effects.Add(manual);
        fog.Apply();
        Assert.Equal(2, cam.Effects.OfType<VolumetricFogEffect>().Count());

        // ...and survives turning Volumetric off, while the auto-added one goes.
        fog.Volumetric = false;
        fog.Apply();
        var remaining = cam.Effects.OfType<VolumetricFogEffect>().ToList();
        var leftover = Assert.Single(remaining);
        Assert.True(ReferenceEquals(manual, leftover));
        Assert.False(fog.AutoVolumetric);
    }

    [Fact]
    public void ManageOff_LeavesCamerasAlone()
    {
        var scene = CreateScene();
        var cam = AddCamera(scene);
        var fog = AddFog(scene);
        fog.ManageVolumetricEffect = false;
        fog.Apply();

        Assert.Empty(cam.Effects.OfType<VolumetricFogEffect>());
        // Analytic fog still applies — only effect management is skipped.
        Assert.Equal(Scene.FogParams.FogMode.ExponentialSquared, scene.Fog.Mode);
    }

    [Fact]
    public void Import_EnvFogParsesKeys_NoDensityBombVolume()
    {
        const string fogMap = "{\"classname\" \"worldspawn\" " +
            "{ ( -64 -64 0 ) ( 64 -64 0 ) ( 64 64 0 ) brick 0 0 0 1 1 " +
            "( -64 -64 64 ) ( -64 64 64 ) ( 64 64 64 ) brick 0 0 0 1 1 " +
            "( -64 -64 0 ) ( -64 -64 64 ) ( 64 -64 64 ) brick 0 0 0 1 1 " +
            "( -64 64 0 ) ( 64 64 0 ) ( 64 64 64 ) brick 0 0 0 1 1 " +
            "( -64 -64 0 ) ( -64 0 64 ) ( -64 64 0 ) brick 0 0 0 1 1 " +
            "( 64 -64 0 ) ( 64 64 0 ) ( 64 0 64 ) brick 0 0 0 1 1 } }" +
            "{\"classname\" \"player_start\" \"origin\" \"0 0 64\"}" +
            "{\"classname\" \"env_fog\" \"color\" \"255 0 0\" \"density\" \"0.04\" " +
            "\"mode\" \"linear\" \"start\" \"5\" \"end\" \"60\" " +
            "\"volumetric\" \"1\" \"scattering\" \"0.7\" \"maxdistance\" \"120\"}";
        var scene = CreateScene();
        var map = RelicMapParser.Parse(fogMap);
        RelicSceneBuilder.Build(map, scene, new RelicBuildOptions { LoadTextures = false });

        var fog = Assert.Single(scene.FindObjectsOfType<RelicFog>());
        Assert.Equal(RelicFog.RelicFogMode.Linear, fog.Mode);
        Assert.Equal(0.04f, fog.Density, 5);
        Assert.Equal(5f, fog.FogStart, 4);
        Assert.Equal(60f, fog.FogEnd, 4);
        Assert.True(fog.Volumetric);
        Assert.Equal(0.7f, fog.VolumetricScattering, 4);
        Assert.Equal(120f, fog.VolumetricMaxDistance, 3);

        // No global FogVolume stuffing +1.0 density on top of the effect.
        Assert.Empty(scene.FindObjectsOfType<FogVolume>());
    }

    [Fact]
    public void Validator_FlagsMissingAndDuplicateEnvFog()
    {
        var empty = new RelicMapData();
        var issues = RelicMapValidator.Validate(empty);
        Assert.Contains(issues, i => i.Severity == "info" && i.Message.Contains("env_fog"));

        var dup = new RelicMapData();
        dup.Entities.Add(new RelicMapEntity { ClassName = "env_fog" });
        dup.Entities.Add(new RelicMapEntity { ClassName = "env_fog" });
        var issues2 = RelicMapValidator.Validate(dup);
        Assert.Contains(issues2, i => i.Severity == "warning" && i.Message.Contains("Multiple env_fog"));
    }
}
