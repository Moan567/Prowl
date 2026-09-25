namespace Relic.Renderer.OpenGL;

using OpenTK.Graphics.OpenGL4;
using Relic.Framework;
using Relic.Renderer;

/// <summary>
/// Lightweight debug text renderer for HUD. Renders Health/Ammo/Weapon on screen using orthographic quads.
/// Uses a simple 8x8 bitmap font embedded; no external dependencies.
/// </summary>
public sealed class DebugTextRenderer : IDisposable
{
    private int _vao, _vbo;
    private Shader? _textShader;
    private int _fontTexture;
    private bool _initialized;
    private bool _disposed;

    // Simple 8x8 font for ASCII 32..127, each char 8 bytes (rows). 0 = empty, 1 = pixel.
    // Generated procedural fallback: border box + diagonal for visibility.
    private static readonly byte[][] _font = GenerateFont();

    private static byte[][] GenerateFont()
    {
        var f = new byte[128][];
        for (int c = 0; c < 128; c++)
        {
            f[c] = new byte[8];
            // For readable HUD we provide proper glyphs for needed chars; fallback to box
            // Simple: draw char code as pattern
            for (int r = 0; r < 8; r++) f[c][r] = 0;
        }
        // Define glyphs for 0-9, A-Z, a-z, :, /, ., space
        // Use 5x7 style: we set bits manually for needed chars
        void Set(int c, string[] rows) // 7 rows of 5 bits
        {
            for (int r = 0; r < 7 && r < rows.Length; r++)
            {
                byte b = 0;
                for (int col = 0; col < 5; col++)
                {
                    if (col < rows[r].Length && rows[r][col] != ' ' && rows[r][col] != '.')
                        b |= (byte)(1 << (7 - (col + 1)));
                }
                f[c][1 + r] = b;
            }
        }
        // Digits
        Set('0', new[]{" *** ","*   *","*   *","*   *","*   *","*   *"," *** "});
        Set('1', new[]{"  *  "," **  ","  *  ","  *  ","  *  ","  *  "," *** "});
        Set('2', new[]{" *** ","*   *","    *","  ** "," *   ","*    ","*****"});
        Set('3', new[]{"*****","    *"," *** ","    *","    *","*   *"," *** "});
        Set('4', new[]{"   * ","  ** "," * * ","*  * ","*****","   * ","   * "});
        Set('5', new[]{"*****","*    ","**** ","    *","    *","*   *"," *** "});
        Set('6', new[]{" *** ","*    ","*    ","**** ","*   *","*   *"," *** "});
        Set('7', new[]{"*****","    *","   * ","  *  "," *   "," *   "," *   "});
        Set('8', new[]{" *** ","*   *","*   *"," *** ","*   *","*   *"," *** "});
        Set('9', new[]{" *** ","*   *","*   *"," ****","    *","    *"," *** "});
        Set('H', new[]{"*   *","*   *","*   *","*****","*   *","*   *","*   *"});
        Set('e', new[]{"     "," *** ","*   *","*****","*    ","*   *"," *** "});
        Set('a', new[]{"     ","     "," *** ","    *"," ****","*   *"," *** "});
        Set('l', new[]{" *   "," *   "," *   "," *   "," *   "," *   "," *** "});
        Set('t', new[]{" *   "," *   ","*****"," *   "," *   "," *   ","  ***"});
        Set('A', new[]{" *** ","*   *","*   *","*****","*   *","*   *","*   *"});
        Set('m', new[]{"     ","     ","* ***","** * ","* * *","*   *","*   *"});
        Set('o', new[]{"     ","     "," *** ","*   *","*   *","*   *"," *** "});
        Set('W', new[]{"*   *","*   *","*   *","* * *","** **","*   *","*   *"});
        Set('p', new[]{"     ","     ","**** ","*   *","*   *","**** ","*    "});
        Set('n', new[]{"     ","     ","* ** ","**  *","*   *","*   *","*   *"});
        Set(':', new[]{"     ","  *  ","     ","     ","  *  ","     ","     "});
        Set('/', new[]{"    *","   * ","  *  "," *   ","*    ","     ","     "});
        Set(':', new[]{"     ","  *  ","     ","     ","  *  ","     ","     "});
        Set('-', new[]{"     ","     ","     ","*****","     ","     ","     "});
        Set('R', new[]{"**** ","*   *","*   *","**** ","* *  ","*  * ","*   *"});
        Set('i', new[]{"  *  ","     ","  *  ","  *  ","  *  ","  *  "," *** "});
        Set('f', new[]{"  ** "," *   "," *   ","*****"," *   "," *   "," *   "});
        Set('c', new[]{"     ","     "," *** ","*   *","*    ","*   *"," *** "});
        Set(' ', new[]{"     ","     ","     ","     ","     ","     ","     "});
        Set('S', new[]{" *** ","*   *","*    "," *** ","    *","*   *"," *** "});
        Set('D', new[]{"**** ","*   *","*   *","*   *","*   *","*   *","**** "});
        Set('b', new[]{"*    ","*    ","* ** ","**  *","*   *","*   *"," *** "});
        Set('g', new[]{"     ","     "," *** ","*   *","*   *"," ****","    *"," *** "});
        Set('u', new[]{"     ","     ","*   *","*   *","*   *","*   *"," *** "});
        Set('r', new[]{"     ","     ","* ** ","**   ","*    ","*    ","*    "});
        Set('P', new[]{"**** ","*   *","*   *","**** ","*    ","*    ","*    "});
        Set('k', new[]{"*   *","*  * ","* *  ","**   ","* *  ","*  * ","*   *"});
        Set('d', new[]{"    *","    *","  ** "," * * ","*   *","*   *"," *** "});
        Set('C', new[]{" *** ","*   *","*    ","*    ","*    ","*   *"," *** "});
        Set('N', new[]{"*   *","**  *","* * *","*  **","*   *","*   *","*   *"});
        return f;
    }

    public void Initialize()
    {
        if (_initialized) return;
        _textShader = new Shader(TextVertex, TextFragment);
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        // Font texture 128x48 (16*8, 6*8) with 95 chars
        const int cols = 16, rows = 6;
        const int cell = 8;
        int texW = cols * cell;
        int texH = rows * cell;
        var data = new byte[texW * texH * 4];
        for (int ch = 32; ch < 127; ch++)
        {
            int idx = ch - 32;
            int cx = (idx % cols) * cell;
            int cy = (idx / cols) * cell;
            var glyph = _font[ch];
            for (int r = 0; r < 8; r++)
            {
                byte bits = glyph[r];
                for (int c = 0; c < 8; c++)
                {
                    bool on = (bits & (1 << (7 - c))) != 0;
                    int px = cx + c;
                    int py = cy + r;
                    int off = (py * texW + px) * 4;
                    data[off] = 255;
                    data[off + 1] = 255;
                    data[off + 2] = 255;
                    data[off + 3] = (byte)(on ? 255 : 0);
                }
            }
        }
        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texW, texH, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        _initialized = true;
    }

    public void DrawText(string text, float x, float y, float scale, Vector4 color, int screenWidth, int screenHeight)
    {
        if (!_initialized) Initialize();
        if (_textShader == null) return;
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        var proj = MatrixExt.CreateOrthographicOffCenter(0, screenWidth, 0, screenHeight, -1, 1);
        // Simple ortho: use identity view and proj via shader uniform
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        _textShader.Bind();
        _textShader.SetUniform("uProjection", proj);
        _textShader.SetUniform("uColor", color);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        _textShader.SetUniform("uTexture", 0);
        // For each char, build quad
        float cursorX = x;
        float cursorY = y;
        const int cols = 16;
        foreach (char ch in text)
        {
            if (ch == '\n') { cursorX = x; cursorY -= 16 * scale; continue; }
            if (ch < 32 || ch >= 127) { cursorX += 8 * scale; continue; }
            int idx = ch - 32;
            float u0 = (idx % cols) * 8f / 128f;
            float v0 = (idx / cols) * 8f / 48f;
            float u1 = u0 + 8f / 128f;
            float v1 = v0 + 8f / 48f;
            float w = 8 * scale;
            float h = 8 * scale;
            // Two triangles
            float[] verts = {
                cursorX, cursorY, u0, v1,
                cursorX+w, cursorY, u1, v1,
                cursorX+w, cursorY+h, u1, v0,
                cursorX, cursorY, u0, v1,
                cursorX+w, cursorY+h, u1, v0,
                cursorX, cursorY+h, u0, v0,
            };
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * 4, verts, BufferUsageHint.DynamicDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * 4, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * 4, 2 * 4);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            cursorX += 8 * scale;
        }
        _textShader.Unbind();
        GL.BindVertexArray(0);
        GL.Enable(EnableCap.DepthTest);
    }

    public void DrawHud(int screenWidth, int screenHeight, float health, float maxHealth, int ammo, int maxAmmo, string weapon, int reserve)
    {
        DrawText($"Health: {health:F0}/{maxHealth:F0}", 10, screenHeight - 30, 2f, new Vector4(1,1,1,1), screenWidth, screenHeight);
        DrawText($"Ammo: {ammo}/{maxAmmo} ({reserve})", 10, screenHeight - 60, 2f, new Vector4(1,1,0.2f,1), screenWidth, screenHeight);
        DrawText($"Weapon: {weapon}", 10, screenHeight - 90, 2f, new Vector4(0.6f,0.8f,1f,1), screenWidth, screenHeight);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        _textShader?.Dispose();
        if (_fontTexture != 0) GL.DeleteTexture(_fontTexture);
    }

    private const string TextVertex = @"#version 460 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUV;
uniform mat4 uProjection;
out vec2 vUV;
void main(){ gl_Position = uProjection * vec4(aPos, 0.0, 1.0); vUV = aUV; }";

    private const string TextFragment = @"#version 460 core
in vec2 vUV;
uniform sampler2D uTexture;
uniform vec4 uColor;
out vec4 FragColor;
void main(){ vec4 c = texture(uTexture, vUV); FragColor = vec4(uColor.rgb, uColor.a * c.a); if(FragColor.a < 0.05) discard; }";
}

// Helper to create ortho without relying on Math CreateOrthographicOffCenter (add if missing)
internal static class MatrixExt
{
    public static Matrix4x4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        float rl = 1f / (right - left);
        float tb = 1f / (top - bottom);
        float fn = 1f / (far - near);
        return new Matrix4x4(
            2*rl, 0, 0, 0,
            0, 2*tb, 0, 0,
            0, 0, -2*fn, 0,
            -(right+left)*rl, -(top+bottom)*tb, -(far+near)*fn, 1
        );
    }
}
