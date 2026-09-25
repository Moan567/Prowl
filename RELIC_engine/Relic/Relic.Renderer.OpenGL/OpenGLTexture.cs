namespace Relic.Renderer.OpenGL;

using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Relic.Renderer;

public sealed class OpenGLTexture : ITexture
{
    public int Handle { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureFormat Format { get; private set; }
    private bool _disposed;

    public OpenGLTexture(int width, int height, byte[] rgba, TextureFormat format = TextureFormat.RGBA8, bool generateMipmaps = true)
    {
        Width = width;
        Height = height;
        Format = format;
        Handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(generateMipmaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        if (generateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private OpenGLTexture(int handle, int width, int height, TextureFormat format)
    {
        Handle = handle;
        Width = width;
        Height = height;
        Format = format;
    }

    public static OpenGLTexture FromFile(string path)
    {
        if (!File.Exists(path))
            return WhiteFallback();

        try
        {
            using var img = Image.Load<Rgba32>(path);
            // ImageSharp loads top-to-bottom, OpenGL expects bottom-to-top -> flip
            img.Mutate(ctx => ctx.Flip(FlipMode.Vertical));
            var rgba = new byte[img.Width * img.Height * 4];
            img.CopyPixelDataTo(rgba);
            return new OpenGLTexture(img.Width, img.Height, rgba);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Relic] Failed to load texture {path}: {ex.Message}");
            return WhiteFallback();
        }
    }

    public static OpenGLTexture WhiteFallback()
    {
        var white = new byte[] { 255, 255, 255, 255 };
        return new OpenGLTexture(1, 1, white, TextureFormat.RGBA8, false);
    }

    public void SetData(byte[] data) => throw new NotSupportedException("Use constructor for immutable textures in Phase 2");

    public void Bind(int slot = 0)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + slot);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GL.DeleteTexture(Handle);
    }
}

public static class TextureLoader
{
    private static readonly Dictionary<string, OpenGLTexture> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static OpenGLTexture Load(string textureName, string assetsRoot = "Assets")
    {
        // texture lookup: wall01 -> Assets/Textures/wall01.png
        // Also try wall01.png, brick, brick.png etc - case insensitive
        if (_cache.TryGetValue(textureName, out var cached)) return cached;

        string[] candidates =
        {
            Path.Combine(assetsRoot, "Textures", textureName),
            Path.Combine(assetsRoot, "Textures", textureName + ".png"),
            Path.Combine(assetsRoot, "Textures", textureName + ".jpg"),
            Path.Combine(assetsRoot, textureName + ".png"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                var tex = OpenGLTexture.FromFile(c);
                _cache[textureName] = tex;
                Console.WriteLine($"[Relic] Loaded texture '{textureName}' -> {c} ({tex.Width}x{tex.Height})");
                return tex;
            }
        }

        // Fallback
        Console.WriteLine($"[Relic] Texture '{textureName}' not found, using white fallback (searched {string.Join(", ", candidates)})");
        var fallback = OpenGLTexture.WhiteFallback();
        _cache[textureName] = fallback;
        return fallback;
    }

    public static void Clear()
    {
        foreach (var t in _cache.Values) t.Dispose();
        _cache.Clear();
    }
}
