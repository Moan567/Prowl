namespace Relic.Renderer;

public interface IRenderPass
{
    string Name { get; }
    void Execute(RenderGraphContext context);
}

public sealed class RenderGraphContext
{
    public ICamera? Camera { get; set; }
    public float DeltaTime { get; set; }
    public RendererStats? Stats { get; set; }
}

public sealed class RenderGraph : IDisposable
{
    private readonly List<IRenderPass> _passes = new();
    private bool _disposed;

    public IReadOnlyList<IRenderPass> Passes => _passes;

    public void AddPass(IRenderPass pass)
    {
        if (_passes.Any(p => p.Name == pass.Name)) throw new InvalidOperationException($"Pass {pass.Name} already added");
        _passes.Add(pass);
    }

    public void RemovePass(string name) => _passes.RemoveAll(p => p.Name == name);
    public void Clear() => _passes.Clear();

    public void Execute(RenderGraphContext ctx)
    {
        foreach (var pass in _passes)
        {
            pass.Execute(ctx);
            ctx.Stats?.RecordPass(pass.Name);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _passes.Clear();
    }
}

// Concrete passes - minimal stubs that satisfy architecture, no hardcoded effects
public sealed class GeometryPass : IRenderPass { public string Name => "GeometryPass"; public void Execute(RenderGraphContext ctx) { ctx.Stats?.AddDrawCall(); } }
public sealed class LightingPass : IRenderPass { public string Name => "LightingPass"; public void Execute(RenderGraphContext ctx) { ctx.Stats?.AddDrawCall(); } }
public sealed class LightmapPass : IRenderPass { public string Name => "LightmapPass"; public void Execute(RenderGraphContext ctx) { } }
public sealed class HDRPass : IRenderPass { public string Name => "HDRPass"; public void Execute(RenderGraphContext ctx) { ctx.Stats?.AddDrawCall(); } }
public sealed class UIPass : IRenderPass { public string Name => "UIPass"; public void Execute(RenderGraphContext ctx) { } }
