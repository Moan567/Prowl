namespace Relic.Framework;

public sealed class EngineSettings
{
    public string WindowTitle { get; set; } = "Relic";
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public bool VSync { get; set; } = true;
    public string AssetsRoot { get; set; } = "Assets";
    public float TargetFrameRate { get; set; } = 60f;
}
