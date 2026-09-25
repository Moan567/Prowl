namespace Relic.Gameplay.Components;

using Relic.Framework;

public sealed class FogComponent : Component
{
    public Vector3 Color { get; set; } = new(120f/255f, 140f/255f, 180f/255f);
    public float Density { get; set; } = 0.002f;
    public float Start { get; set; } = 64f;
    public float End { get; set; } = 1024f;
}

public sealed class PostProcessComponent : Component
{
    public bool BloomEnabled { get; set; } = true;
    public float BloomIntensity { get; set; } = 1.0f;
    public float BloomThreshold { get; set; } = 1.0f;
    public float Exposure { get; set; } = 1.1f;
    public string ToneMap { get; set; } = "ACES"; // Reinhard or ACES
}
