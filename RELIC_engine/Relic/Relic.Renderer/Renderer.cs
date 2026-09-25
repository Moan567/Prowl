namespace Relic.Renderer;

using Relic.Framework;

public interface IRenderer : IDisposable
{
    void Initialize();
    void BeginFrame();
    void EndFrame();
    void SetViewport(int x, int y, int width, int height);
    void Clear(ClearFlags flags, Vector4 color, float depth = 1f, int stencil = 0);
    void DrawMesh(IMesh mesh, IMaterial material, Matrix4x4 transform);
    void Submit(IRenderCommand command);
}

[Flags]
public enum ClearFlags
{
    None = 0,
    Color = 1,
    Depth = 2,
    Stencil = 4,
    All = Color | Depth | Stencil
}

public struct Vector4
{
    public float X, Y, Z, W;

    public Vector4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
    public Vector4(Vector3 xyz, float w) { X = xyz.X; Y = xyz.Y; Z = xyz.Z; W = w; }
    public static implicit operator Vector4(Vector3 v) => new(v.X, v.Y, v.Z, 1f);
}

public interface ICamera
{
    Matrix4x4 ViewMatrix { get; }
    Matrix4x4 ProjectionMatrix { get; }
    Matrix4x4 ViewProjectionMatrix { get; }
    Vector3 Position { get; set; }
    Vector3 Rotation { get; set; }
    float FieldOfView { get; set; }
    float AspectRatio { get; set; }
    float NearPlane { get; set; }
    float FarPlane { get; set; }
    void Update();
}

public interface IMesh : IDisposable
{
    int VertexCount { get; }
    int IndexCount { get; }
    VertexLayout Layout { get; }
    void SetData<T>(T[] vertices, uint[] indices) where T : unmanaged;
}

public interface IMaterial : IDisposable
{
    string Name { get; }
    ITexture? DiffuseTexture { get; set; }
    ITexture? NormalTexture { get; set; }
    Vector4 Color { get; set; }
    float Shininess { get; set; }
}

public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    TextureFormat Format { get; }
    void SetData(byte[] data);
}

public enum TextureFormat
{
    R8,
    RG8,
    RGB8,
    RGBA8,
    Depth24,
    Depth24Stencil8
}

public interface IRenderCommand { }

public interface IShader : IDisposable
{
    void Bind();
    void SetUniform(string name, Matrix4x4 value);
    void SetUniform(string name, Vector3 value);
    void SetUniform(string name, Vector4 value);
    void SetUniform(string name, float value);
    void SetUniform(string name, int value);
    void SetTexture(string name, ITexture texture, int slot);
}