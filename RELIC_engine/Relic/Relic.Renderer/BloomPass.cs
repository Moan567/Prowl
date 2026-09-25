namespace Relic.Renderer;

// BloomPass pipeline: Bright Extract -> Downsample -> Gaussian Blur -> Upsample -> Composite
public sealed class BloomPass : IRenderPass
{
    public string Name => "BloomPass";
    public bool Enabled { get; set; } = true;
    public float Intensity { get; set; } = 1.0f;
    public float Threshold { get; set; } = 1.0f;

    // Console variables
    public static bool R_Bloom { get; set; } = true;
    public static float R_BloomIntensity { get; set; } = 1.0f;
    public static float R_BloomThreshold { get; set; } = 1.0f;

    public void Execute(RenderGraphContext context)
    {
        if (!Enabled || !R_Bloom) return;
        // Stub: pipeline steps would be executed here
        BrightExtract();
        Downsample();
        GaussianBlur();
        Upsample();
        Composite();
        context.Stats?.AddDrawCall(2);
    }

    private void BrightExtract() { }
    private void Downsample() { }
    private void GaussianBlur() { }
    private void Upsample() { }
    private void Composite() { }
}
