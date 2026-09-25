namespace Relic.Renderer;

public sealed class RendererStats
{
    public int DrawCalls { get; private set; }
    public int Triangles { get; private set; }
    public int VisibleLights { get; private set; }
    public int VisibleEntities { get; private set; }
    public float FrameTimeMs { get; private set; }
    public float GpuTimeMs { get; private set; }
    private readonly Dictionary<string, int> _passCounts = new();

    public void Reset()
    {
        DrawCalls = 0;
        Triangles = 0;
        VisibleLights = 0;
        VisibleEntities = 0;
        FrameTimeMs = 0;
        GpuTimeMs = 0;
        _passCounts.Clear();
    }

    public void AddDrawCall(int count = 1) => DrawCalls += count;
    public void AddTriangles(int count) => Triangles += count;
    public void SetVisibleLights(int count) => VisibleLights = count;
    public void SetVisibleEntities(int count) => VisibleEntities = count;
    public void SetFrameTime(float ms) => FrameTimeMs = ms;
    public void SetGpuTime(float ms) => GpuTimeMs = ms;
    public void RecordPass(string name)
    {
        if (_passCounts.ContainsKey(name)) _passCounts[name]++; else _passCounts[name] = 1;
    }

    public string GetText()
        => $"DrawCalls: {DrawCalls} Tris: {Triangles} Lights: {VisibleLights} Ents: {VisibleEntities} Frame: {FrameTimeMs:F1}ms GPU: {GpuTimeMs:F1}ms";
}
