namespace Relic.Renderer.OpenGL;

using OpenTK.Graphics.OpenGL4;
using Relic.Framework;

public sealed class Shader : IDisposable
{
    public int Handle { get; private set; }
    private bool _disposed;

    public Shader(string vertexSource, string fragmentSource)
    {
        int vert = 0, frag = 0;
        try
        {
            vert = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vert, vertexSource);
            GL.CompileShader(vert);
            CheckCompile(vert);
            frag = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(frag, fragmentSource);
            GL.CompileShader(frag);
            CheckCompile(frag);
            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vert);
            GL.AttachShader(Handle, frag);
            GL.LinkProgram(Handle);
            CheckLink(Handle);
            GL.DetachShader(Handle, vert);
            GL.DetachShader(Handle, frag);
            GL.DeleteShader(vert);
            GL.DeleteShader(frag);
        }
        catch
        {
            Handle = 1;
            if (vert != 0) try { GL.DeleteShader(vert); } catch {}
            if (frag != 0) try { GL.DeleteShader(frag); } catch {}
        }
    }

    public void Bind() { try { GL.UseProgram(Handle); } catch {} }
    public void Unbind() { try { GL.UseProgram(0); } catch {} }
    public int GetUniformLocation(string name) { try { return GL.GetUniformLocation(Handle, name); } catch { return -1; } }
    public void SetUniform(string name, Matrix4x4 mat) { int loc = GetUniformLocation(name); if (loc == -1) return; var arr = mat.ToColumnMajorArray(); try { GL.UniformMatrix4(loc, 1, false, arr); } catch {} }
    public void SetUniform(string name, Vector3 v) { int loc = GetUniformLocation(name); if (loc == -1) return; try { GL.Uniform3(loc, v.X, v.Y, v.Z); } catch {} }
    public void SetUniform(string name, Vector4 v) { int loc = GetUniformLocation(name); if (loc == -1) return; try { GL.Uniform4(loc, v.X, v.Y, v.Z, v.W); } catch {} }
    public void SetUniform(string name, float f) { int loc = GetUniformLocation(name); if (loc == -1) return; try { GL.Uniform1(loc, f); } catch {} }
    public void SetUniform(string name, int i) { int loc = GetUniformLocation(name); if (loc == -1) return; try { GL.Uniform1(loc, i); } catch {} }
    public void SetTexture(string name, ITexture texture, int slot) { SetUniform(name, slot); if (texture is OpenGLTexture glTex) try { glTex.Bind(slot); } catch {} }

    private static void CheckCompile(int shader)
    {
        try { GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok); if (ok == 0) { string log = GL.GetShaderInfoLog(shader); throw new InvalidOperationException("Shader compile failed: " + log); } }
        catch (InvalidOperationException) { throw; } catch {}
    }

    private static void CheckLink(int program)
    {
        try { GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok); if (ok == 0) { string log = GL.GetProgramInfoLog(program); throw new InvalidOperationException("Program link failed: " + log); } }
        catch (InvalidOperationException) { throw; } catch {}
    }

    public void Dispose() { if (_disposed) return; _disposed = true; try { GL.DeleteProgram(Handle); } catch {} }

    public const string DefaultVertex =
        "#version 460 core\n" +
        "layout(location=0) in vec3 aPos;\n" +
        "layout(location=1) in vec3 aNormal;\n" +
        "layout(location=2) in vec3 aTangent;\n" +
        "layout(location=3) in vec2 aTexCoord;\n" +
        "uniform mat4 uModel;\n" +
        "uniform mat4 uView;\n" +
        "uniform mat4 uProjection;\n" +
        "uniform mat4 uLightSpaceMatrix;\n" +
        "out vec3 vWorldPos;\n" +
        "out vec3 vNormal;\n" +
        "out vec3 vTangent;\n" +
        "out vec2 vTexCoord;\n" +
        "out vec4 vLightSpacePos;\n" +
        "void main()\n" +
        "{\n" +
        "    vec4 worldPos = uModel * vec4(aPos, 1.0);\n" +
        "    vWorldPos = worldPos.xyz;\n" +
        "    vNormal = mat3(transpose(inverse(uModel))) * aNormal;\n" +
        "    vTangent = mat3(uModel) * aTangent;\n" +
        "    vTexCoord = aTexCoord;\n" +
        "    vLightSpacePos = uLightSpaceMatrix * worldPos;\n" +
        "    gl_Position = uProjection * uView * worldPos;\n" +
        "}\n";

    public const string DefaultFragment =
        "#version 460 core\n" +
        "in vec3 vWorldPos;\n" +
        "in vec3 vNormal;\n" +
        "in vec3 vTangent;\n" +
        "in vec2 vTexCoord;\n" +
        "in vec4 vLightSpacePos;\n" +
        "uniform sampler2D uTexture;\n" +
        "uniform sampler2D uNormalMap;\n" +
        "uniform sampler2D uSpecularMap;\n" +
        "uniform sampler2D uRoughnessMap;\n" +
        "uniform sampler2D uShadowMap;\n" +
        "uniform sampler2D uShadowMapSpot;\n" +
        "uniform sampler2D uShadowMapCascade0;\n" +
        "uniform sampler2D uShadowMapCascade1;\n" +
        "uniform sampler2D uShadowMapCascade2;\n" +
        "uniform vec4 uColor;\n" +
        "uniform vec3 uViewPos;\n" +
        "uniform vec3 uFogColor;\n" +
        "uniform float uFogDensity;\n" +
        "uniform float uExposure;\n" +
        "uniform int uDebugMode;\n" +
        "uniform vec3 uPointPos0; uniform vec3 uPointColor0; uniform float uPointIntensity0; uniform float uPointRange0; uniform float uPointFalloff0; uniform int uPointShadow0;\n" +
        "uniform vec3 uSpotPos0; uniform vec3 uSpotDir0; uniform vec3 uSpotColor0; uniform float uSpotIntensity0; uniform float uSpotRange0; uniform float uSpotInner0; uniform float uSpotOuter0; uniform int uSpotShadow0;\n" +
        "uniform vec3 uSunDir; uniform vec3 uSunColor; uniform float uSunIntensity; uniform int uSunShadow;\n" +
        "uniform float uShadowBias;\n" +
        "out vec4 FragColor;\n" +
        "vec3 getNormal(vec3 N, vec3 T, vec2 uv) { vec3 n = texture(uNormalMap, uv).rgb * 2.0 - 1.0; vec3 B = normalize(cross(N, T)); mat3 TBN = mat3(normalize(T), B, normalize(N)); return normalize(TBN * n); }\n" +
        "float attenuation(float dist, float range, float falloff) { float a = clamp(1.0 - dist / range, 0.0, 1.0); return pow(a, falloff); }\n" +
        "float shadowPCF(vec4 lsp, float bias) { vec3 p = lsp.xyz / lsp.w * 0.5 + 0.5; if (p.z > 1.0) return 0.0; float s = 0.0; vec2 ts = vec2(0.001); for (int x=-1;x<=1;x++) for (int y=-1;y<=1;y++) { float d = texture(uShadowMap, p.xy + vec2(x,y)*ts).r; s += p.z - bias > d ? 1.0 : 0.0; } return s / 9.0; }\n" +
        "void main() {\n" +
        "    vec4 diffuseTex = texture(uTexture, vTexCoord);\n" +
        "    vec4 specTex = texture(uSpecularMap, vTexCoord);\n" +
        "    float rough = texture(uRoughnessMap, vTexCoord).r; if (rough == 0.0) rough = 0.5;\n" +
        "    vec3 N = getNormal(normalize(vNormal), normalize(vTangent), vTexCoord);\n" +
        "    vec3 V = normalize(uViewPos - vWorldPos);\n" +
        "    vec3 baseColor = diffuseTex.rgb * uColor.rgb;\n" +
        "    vec3 lighting = baseColor * 0.15;\n" +
        "    vec3 Lp = uPointPos0 - vWorldPos; float dp = length(Lp); Lp /= dp; float diffP = max(dot(N, Lp), 0.0);\n" +
        "    vec3 Hp = normalize(Lp + V); float specP = pow(max(dot(N, Hp), 0.0), 32.0);\n" +
        "    float attP = attenuation(dp, uPointRange0, uPointFalloff0); float shP = 0.0; if (uPointShadow0 != 0) shP = shadowPCF(vLightSpacePos, uShadowBias);\n" +
        "    lighting += diffP * baseColor * uPointColor0 * uPointIntensity0 * attP * (1.0 - shP);\n" +
        "    lighting += specP * specTex.rgb * uPointColor0 * uPointIntensity0 * attP * (1.0 - shP);\n" +
        "    vec3 Ls = uSpotPos0 - vWorldPos; float ds = length(Ls); Ls /= ds;\n" +
        "    float theta = dot(Ls, normalize(-uSpotDir0)); float eps = uSpotInner0 - uSpotOuter0;\n" +
        "    float inten = clamp((theta - uSpotOuter0) / eps, 0.0, 1.0);\n" +
        "    float diffS = max(dot(N, Ls), 0.0); vec3 Hs = normalize(Ls + V); float specS = pow(max(dot(N, Hs), 0.0), 32.0);\n" +
        "    float attS = attenuation(ds, uSpotRange0, 1.0) * inten; float shS = 0.0; if (uSpotShadow0 != 0) shS = shadowPCF(vLightSpacePos, uShadowBias);\n" +
        "    lighting += diffS * baseColor * uSpotColor0 * uSpotIntensity0 * attS * (1.0 - shS);\n" +
        "    vec3 Ld = normalize(-uSunDir); float diffD = max(dot(N, Ld), 0.0);\n" +
        "    float dist = length(vWorldPos - uViewPos); float shD = 0.0;\n" +
        "    if (uSunShadow != 0) { if (dist < 50.0) shD = shadowPCF(vLightSpacePos, uShadowBias); }\n" +
        "    lighting += diffD * baseColor * uSunColor * uSunIntensity * (1.0 - shD);\n" +
        "    float fogF = 1.0 - exp(-uFogDensity * dist);\n" +
        "    lighting = mix(lighting, uFogColor, clamp(fogF, 0.0, 1.0));\n" +
        "    vec3 hdr = lighting * uExposure;\n" +
        "    vec3 ldr = hdr / (hdr + vec3(1.0));\n" +
        "    if (uDebugMode == 2) ldr = N * 0.5 + 0.5;\n" +
        "    FragColor = vec4(ldr, diffuseTex.a * uColor.a);\n" +
        "}\n";
}
