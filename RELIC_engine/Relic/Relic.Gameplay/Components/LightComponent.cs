namespace Relic.Gameplay.Components;

using Relic.Framework;

public enum LightType { Point, Spot, Directional }

public sealed class LightComponent : Component
{
    public LightType Type { get; set; } = LightType.Point;
    public Vector3 Color { get; set; } = new(1, 1, 1); // 0-1
    public float Intensity { get; set; } = 800f;
    public float Range { get; set; } = 512f;
    public float Falloff { get; set; } = 1f;
    public bool CastShadows { get; set; } = false;
    // Spot specific
    public float InnerConeAngle { get; set; } = 20f;
    public float OuterConeAngle { get; set; } = 35f;
    public Vector3 Direction { get; set; } = new(0, 0, -1);

    public static LightComponent CreatePoint(Vector3 color, float intensity, float range, bool shadows = false)
        => new() { Type = LightType.Point, Color = color, Intensity = intensity, Range = range, CastShadows = shadows };

    public static LightComponent CreateSpot(Vector3 color, float intensity, float range, Vector3 direction, float inner = 20f, float outer = 35f)
        => new() { Type = LightType.Spot, Color = color, Intensity = intensity, Range = range, Direction = direction, InnerConeAngle = inner, OuterConeAngle = outer };

    public static LightComponent CreateDirectional(Vector3 color, float intensity, Vector3 direction)
        => new() { Type = LightType.Directional, Color = color, Intensity = intensity, Direction = direction, Range = float.MaxValue };
}
