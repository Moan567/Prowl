namespace Relic.Renderer.OpenGL;

using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Relic.Content;
using Relic.Renderer;

public sealed class OpenGLMesh : IMesh
{
    private int _vao;
    private int _vbo;
    private int _ebo;
    private bool _disposed;

    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public VertexLayout Layout { get; }

    public OpenGLMesh(VertexLayout? layout = null)
    {
        Layout = layout ?? VertexLayout.Default;
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();
    }

    public void SetData<T>(T[] vertices, uint[] indices) where T : unmanaged
    {
        VertexCount = vertices.Length;
        IndexCount = indices.Length;

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * Marshal.SizeOf<T>(), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        // Layout: Position(0)=vec3, Normal(1)=vec3, Tangent(2)=vec3, TexCoord(3)=vec2
        // MeshVertex is 11 floats tightly packed (3+3+3+2)
        int stride = MeshVertex.Stride;
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(3);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, 9 * sizeof(float));

        GL.BindVertexArray(0);
    }

    public void SetMeshData(MeshData data)
    {
        SetData(data.Vertices.ToArray(), data.Indices.ToArray());
    }

    public void SetMeshGroup(MeshGroup group)
    {
        SetData(group.Vertices.ToArray(), group.Indices.ToArray());
    }

    public void Bind() => GL.BindVertexArray(_vao);
    public void Unbind() => GL.BindVertexArray(0);

    public void Draw()
    {
        Bind();
        GL.DrawElements(PrimitiveType.Triangles, IndexCount, DrawElementsType.UnsignedInt, 0);
        Unbind();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
    }
}
