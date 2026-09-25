namespace Relic.Renderer;

public enum HDRFormat { RGBA16F, RGBA32F }

public sealed class HDRFramebuffer : IDisposable
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public HDRFormat Format { get; private set; } = HDRFormat.RGBA16F;
    public float Exposure { get; set; } = 1.1f;
    public bool IsCreated { get; private set; }

    // Console variables
    public static bool R_HDR { get; set; } = true;
    public static float R_Exposure { get; set; } = 1.1f;

    public HDRFramebuffer(int width = 1280, int height = 720, HDRFormat format = HDRFormat.RGBA16F)
    {
        Width = width; Height = height; Format = format;
        Create();
    }

    public void Create()
    {
        // In headless/test we just mark created; real GL would create FBO with RGBA16F texture
        // Actual GL creation is in Relic.Renderer.OpenGL.HDRFramebufferGL when context available
        IsCreated = true;
    }

    public void Resize(int width, int height)
    {
        Width = width; Height = height;
        Create();
    }

    public void Bind() { /* bind HDR FBO */ }
    public void Unbind() { /* bind backbuffer */ }

    public void Dispose() { IsCreated = false; }
}
