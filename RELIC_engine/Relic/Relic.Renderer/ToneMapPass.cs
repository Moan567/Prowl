namespace Relic.Renderer;

public enum ToneMapMode { Reinhard, ACES }

public sealed class ToneMapPass : IRenderPass
{
    public string Name => "ToneMapPass";
    public ToneMapMode Mode { get; set; } = ToneMapMode.ACES;
    public float Exposure { get; set; } = 1.1f;

    // Console variable
    public static string R_ToneMap { get; set; } = "ACES"; // "Reinhard" or "ACES"

    public void Execute(RenderGraphContext context)
    {
        // Parse console var
        if (Enum.TryParse<ToneMapMode>(R_ToneMap, true, out var m)) Mode = m;
        Exposure = HDRFramebuffer.R_Exposure;
        // Stub: would apply tone mapping to HDR buffer
        if (Mode == ToneMapMode.Reinhard) ApplyReinhard();
        else ApplyACES();
    }

    private void ApplyReinhard() { /* LDR = color / (1+color) */ }
    private void ApplyACES() { /* ACES filmic */ }
}
