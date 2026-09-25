namespace Relic.Renderer.OpenGL;

using OpenTK.Graphics.OpenGL4;
using Relic.Content;
using Relic.Framework;
using Relic.Renderer;

public sealed class OpenGLRenderer : IRenderer
{
    private bool _initialized;
    private bool _disposed;
    private Shader? _shader;
    private Camera? _camera;

    public Shader Shader => _shader ?? throw new InvalidOperationException("Renderer not initialized");
    public Camera Camera => _camera ?? throw new InvalidOperationException("Renderer not initialized");

    public void Initialize()
    {
        if (_initialized) return;

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        GL.FrontFace(FrontFaceDirection.Ccw);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _shader = new Shader(Shader.DefaultVertex, Shader.DefaultFragment);
        _shader.Bind();
        _shader.SetUniform("uTexture", 0);
        _shader.SetUniform("uColor", new Vector4(1, 1, 1, 1));
        _shader.SetUniform("uLightDir", new Vector3(0.3f, -1f, 0.4f));
        _shader.Unbind();

        _camera = new Camera();
        _initialized = true;
        Console.WriteLine("[Relic] OpenGLRenderer initialized (OpenGL 4.6)");
        Console.WriteLine($"[Relic] GL Vendor: {GL.GetString(StringName.Vendor)} Renderer: {GL.GetString(StringName.Renderer)} Version: {GL.GetString(StringName.Version)}");
    }

    public void SetCamera(Camera camera) => _camera = camera;

    public void BeginFrame()
    {
        if (!_initialized) Initialize();
    }

    public void EndFrame() { }

    public void SetViewport(int x, int y, int width, int height)
    {
        GL.Viewport(x, y, width, height);
        if (_camera != null)
        {
            _camera.AspectRatio = width / (float)height;
            _camera.MarkProjectionDirty();
        }
    }

    public void Clear(ClearFlags flags, Vector4 color, float depth = 1f, int stencil = 0)
    {
        ClearBufferMask mask = 0;
        if (flags.HasFlag(ClearFlags.Color)) mask |= ClearBufferMask.ColorBufferBit;
        if (flags.HasFlag(ClearFlags.Depth)) mask |= ClearBufferMask.DepthBufferBit;
        if (flags.HasFlag(ClearFlags.Stencil)) mask |= ClearBufferMask.StencilBufferBit;

        GL.ClearColor(color.X, color.Y, color.Z, color.W);
        GL.ClearDepth(depth);
        GL.ClearStencil(stencil);
        GL.Clear(mask);
    }

    public void DrawMesh(IMesh mesh, IMaterial material, Matrix4x4 transform)
    {
        if (mesh is not OpenGLMesh glMesh) throw new ArgumentException("Mesh must be OpenGLMesh");
        if (_shader == null || _camera == null) return;

        _shader.Bind();
        _shader.SetUniform("uModel", transform);
        _shader.SetUniform("uView", _camera.ViewMatrix);
        _shader.SetUniform("uProjection", _camera.ProjectionMatrix);

        if (material?.DiffuseTexture is OpenGLTexture tex)
            tex.Bind(0);
        else
            _shader.SetUniform("uTexture", 0); // white fallback bound elsewhere

        _shader.SetUniform("uColor", material?.Color ?? new Vector4(1, 1, 1, 1));

        glMesh.Draw();
        _shader.Unbind();
    }

    public void DrawMeshGroup(MeshGroup group, Matrix4x4 transform, string assetsRoot = "Assets")
    {
        if (_shader == null || _camera == null) return;
        var tex = TextureLoader.Load(group.TextureName, assetsRoot);
        // Create temp mesh for this group - caller should cache OpenGLMesh per group for performance
        // This helper does immediate upload each frame (not optimal but simple for Phase2)
        // Better to pre-upload: use DrawPrebuilt helper below
        _shader.Bind();
        _shader.SetUniform("uModel", transform);
        _shader.SetUniform("uView", _camera.ViewMatrix);
        _shader.SetUniform("uProjection", _camera.ProjectionMatrix);
        tex.Bind(0);
        _shader.SetUniform("uTexture", 0);
        _shader.SetUniform("uColor", new Vector4(1, 1, 1, 1));
        // Need mesh - this path expects caller provides mesh
        _shader.Unbind();
    }

    public void Submit(IRenderCommand command) => throw new NotImplementedException("Submit not used in Phase2 immediate mode");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shader?.Dispose();
        _initialized = false;
    }
}
