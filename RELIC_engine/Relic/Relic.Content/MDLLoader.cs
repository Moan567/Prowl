namespace Relic.Content;

using Relic.Framework;

public sealed class MDLModel
{
    public int SkinWidth { get; set; }
    public int SkinHeight { get; set; }
    public byte[] SkinData = Array.Empty<byte>(); // first skin RGBA
    public List<MDLStVert> StVerts = new();
    public List<MDLTriangle> Triangles = new();
    public List<MDLFrame> Frames = new();
    public Vector3 Scale;
    public Vector3 ScaleOrigin;
    public Vector3 EyePosition;
}

public struct MDLStVert
{
    public int OnSeam;
    public int S;
    public int T;
}

public struct MDLTriangle
{
    public int FacesFront;
    public int[] VertIndex; // 3
}

public sealed class MDLFrame
{
    public string Name = "";
    public Vector3 BBoxMin;
    public Vector3 BBoxMax;
    public List<MDLVertex> Verts = new();
}

public struct MDLVertex
{
    public byte X, Y, Z;
    public byte NormalIndex;
    public Vector3 GetPosition(Vector3 scale, Vector3 origin)
        => new Vector3(X * scale.X + origin.X, Y * scale.Y + origin.Y, Z * scale.Z + origin.Z);
}

public static class MDLLoader
{
    private const int IDPO = 0x4F504449; // "IDPO" little endian
    private const int VERSION = 6;

    // Quake palette shared with TrenchBroom previews (Assets/gfx/palette.lmp).
    // Grayscale fallback only if the palette file is missing.
    private static byte[]? _palette;
    private static byte[] Palette
    {
        get
        {
            if (_palette != null) return _palette;
            foreach (var candidate in new[]
            {
                Path.Combine("Assets", "gfx", "palette.lmp"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "gfx", "palette.lmp"),
                "C:/Users/QuipG/OneDrive/Desktop/RELIC_engine/Assets/gfx/palette.lmp",
            })
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        var bytes = File.ReadAllBytes(candidate);
                        if (bytes.Length >= 768)
                        {
                            _palette = bytes[..768];
                            return _palette;
                        }
                    }
                }
                catch { }
            }
            _palette = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                _palette[i * 3] = (byte)i;
                _palette[i * 3 + 1] = (byte)i;
                _palette[i * 3 + 2] = (byte)i;
            }
            return _palette;
        }
    }

    public static void ResetPaletteCache() => _palette = null;

    public static MDLModel Load(string fullPath)
    {
        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        int ident = br.ReadInt32();
        if (ident != IDPO) throw new InvalidDataException($"Not a Quake MDL file: ident {ident:X}");
        int version = br.ReadInt32();
        if (version != VERSION) throw new InvalidDataException($"Unsupported MDL version {version}");

        var scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        var scaleOrigin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        float boundingRadius = br.ReadSingle();
        var eyePos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        int numSkins = br.ReadInt32();
        int skinWidth = br.ReadInt32();
        int skinHeight = br.ReadInt32();
        int numVerts = br.ReadInt32();
        int numTris = br.ReadInt32();
        int numFrames = br.ReadInt32();
        int synctype = br.ReadInt32();
        int flags = br.ReadInt32();
        float size = br.ReadSingle();

        var model = new MDLModel
        {
            SkinWidth = skinWidth,
            SkinHeight = skinHeight,
            Scale = scale,
            ScaleOrigin = scaleOrigin,
            EyePosition = eyePos
        };

        // Skins
        byte[]? firstSkin = null;
        for (int i = 0; i < numSkins; i++)
        {
            int skinGroup = br.ReadInt32(); // actually aliasskintype
            if (skinGroup == 0) // single
            {
                int skinSize = skinWidth * skinHeight;
                var skinBytes = br.ReadBytes(skinSize);
                if (i == 0) firstSkin = skinBytes;
            }
            else // group
            {
                int groupSkins = br.ReadInt32();
                for (int j = 0; j < groupSkins; j++) br.ReadSingle(); // intervals
                for (int j = 0; j < groupSkins; j++)
                {
                    int sz = skinWidth * skinHeight;
                    var b = br.ReadBytes(sz);
                    if (i == 0 && j == 0) firstSkin = b;
                }
            }
        }
        if (firstSkin != null)
        {
            // Convert indexed to RGBA via palette
            var rgba = new byte[skinWidth * skinHeight * 4];
            for (int i = 0; i < firstSkin.Length && i < skinWidth*skinHeight; i++)
            {
                byte idx = firstSkin[i];
                rgba[i * 4] = Palette[idx * 3];
                rgba[i * 4 + 1] = Palette[idx * 3 + 1];
                rgba[i * 4 + 2] = Palette[idx * 3 + 2];
                rgba[i * 4 + 3] = idx == 255 ? (byte)0 : (byte)255; // 255 transparent in Quake
            }
            model.SkinData = rgba;
        }

        // St verts
        for (int i = 0; i < numVerts; i++)
        {
            int onseam = br.ReadInt32();
            int s = br.ReadInt32();
            int t = br.ReadInt32();
            model.StVerts.Add(new MDLStVert { OnSeam = onseam, S = s, T = t });
        }

        // Triangles
        for (int i = 0; i < numTris; i++)
        {
            int facesFront = br.ReadInt32();
            int v0 = br.ReadInt32();
            int v1 = br.ReadInt32();
            int v2 = br.ReadInt32();
            model.Triangles.Add(new MDLTriangle { FacesFront = facesFront, VertIndex = new[] { v0, v1, v2 } });
        }

        // Frames
        for (int i = 0; i < numFrames; i++)
        {
            int frameType = br.ReadInt32();
            if (frameType == 0) // single
            {
                // bbox min/max as trivertx_t (4 bytes each) but actually 3 bytes + 1? In file, bbox is stored as trivertx_t with 4 bytes each, but we can read as 4 bytes
                var bboxMinBytes = br.ReadBytes(4);
                var bboxMaxBytes = br.ReadBytes(4);
                string name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(16)).TrimEnd('\0');
                var frame = new MDLFrame { Name = name };
                // bbox values are not used for rendering, but we can store
                // Read vertices
                for (int v = 0; v < numVerts; v++)
                {
                    var vb = br.ReadBytes(4);
                    frame.Verts.Add(new MDLVertex { X = vb[0], Y = vb[1], Z = vb[2], NormalIndex = vb[3] });
                }
                model.Frames.Add(frame);
            }
            else // group
            {
                int groupFrames = br.ReadInt32();
                // bbox min/max for group? Actually min/max are stored as in daliasgroup_t
                var gMin = br.ReadBytes(4);
                var gMax = br.ReadBytes(4);
                // intervals
                for (int j = 0; j < groupFrames; j++) br.ReadSingle();
                for (int j = 0; j < groupFrames; j++)
                {
                    var fMin = br.ReadBytes(4);
                    var fMax = br.ReadBytes(4);
                    string name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(16)).TrimEnd('\0');
                    var frame = new MDLFrame { Name = name };
                    for (int v = 0; v < numVerts; v++)
                    {
                        var vb = br.ReadBytes(4);
                        frame.Verts.Add(new MDLVertex { X = vb[0], Y = vb[1], Z = vb[2], NormalIndex = vb[3] });
                    }
                    model.Frames.Add(frame);
                }
            }
        }

        return model;
    }

    public static MeshData ToMeshData(MDLModel mdl, int frameIndex = 0, float lerp = 0f, int nextFrame = -1)
    {
        if (mdl.Frames.Count == 0) return new MeshData();
        int f0 = Math.Clamp(frameIndex, 0, mdl.Frames.Count - 1);
        int f1 = nextFrame >= 0 ? Math.Clamp(nextFrame, 0, mdl.Frames.Count - 1) : f0;
        var frame0 = mdl.Frames[f0];
        var frame1 = mdl.Frames[f1];
        var mesh = new MeshData();
        // First pass: create vertices without tangents, then compute tangents per triangle
        uint idx = 0;
        var tmpVerts = new List<MeshVertex>();
        var tmpIndices = new List<uint>();
        foreach (var tri in mdl.Triangles)
        {
            for (int j = 0; j < 3; j++)
            {
                int vi = tri.VertIndex[j];
                var st = mdl.StVerts[vi];
                var v0 = frame0.Verts[vi];
                var v1 = frame1.Verts[vi];
                Vector3 p0 = v0.GetPosition(mdl.Scale, mdl.ScaleOrigin);
                Vector3 p1 = v1.GetPosition(mdl.Scale, mdl.ScaleOrigin);
                Vector3 pos = Vector3.Lerp(p0, p1, lerp);
                int s = st.S;
                int t = st.T;
                if (st.OnSeam != 0 && tri.FacesFront == 0) s += mdl.SkinWidth / 2;
                float u = (s + 0.5f) / mdl.SkinWidth;
                float v2 = (t + 0.5f) / mdl.SkinHeight;
                var uv = new Vector2(u, v2);
                Vector3 normal = Anorms.GetNormal(v0.NormalIndex);
                tmpVerts.Add(new MeshVertex(pos, normal, uv, new Vector3(1,0,0)));
            }
            tmpIndices.Add(idx);
            tmpIndices.Add(idx + 1);
            tmpIndices.Add(idx + 2);
            idx += 3;
        }
        // Compute tangents per triangle
        var tangents = new Vector3[tmpVerts.Count];
        for (int i = 0; i < tmpIndices.Count; i += 3)
        {
            uint i0 = tmpIndices[i], i1 = tmpIndices[i+1], i2 = tmpIndices[i+2];
            var p0 = tmpVerts[(int)i0].Position; var p1 = tmpVerts[(int)i1].Position; var p2 = tmpVerts[(int)i2].Position;
            var uv0 = tmpVerts[(int)i0].TexCoord; var uv1 = tmpVerts[(int)i1].TexCoord; var uv2 = tmpVerts[(int)i2].TexCoord;
            var edge1 = p1 - p0; var edge2 = p2 - p0;
            var dUV1 = uv1 - uv0; var dUV2 = uv2 - uv0;
            float denom = dUV1.X * dUV2.Y - dUV2.X * dUV1.Y;
            Vector3 tangent;
            if (MathF.Abs(denom) < 1e-6f) tangent = new Vector3(1,0,0);
            else
            {
                float r = 1f / denom;
                tangent = new Vector3(
                    (dUV2.Y * edge1.X - dUV1.Y * edge2.X) * r,
                    (dUV2.Y * edge1.Y - dUV1.Y * edge2.Y) * r,
                    (dUV2.Y * edge1.Z - dUV1.Y * edge2.Z) * r);
                tangent.Normalize();
            }
            tangents[i0] = tangent; tangents[i1] = tangent; tangents[i2] = tangent;
        }
        for (int i = 0; i < tmpVerts.Count; i++)
        {
            var v = tmpVerts[i];
            var t = tangents[i];
            if (t.LengthSquared() < 0.001f) t = new Vector3(1,0,0);
            mesh.Vertices.Add(new MeshVertex(v.Position, v.Normal, v.TexCoord, t));
        }
        foreach (var ii in tmpIndices) mesh.Indices.Add(ii);
        return mesh;
    }
}

// Approximate Quake normal table (162 normals) - simplified to 6 axis for fallback
internal static class Anorms
{
    private static readonly Vector3[] _anorms = new Vector3[162];
    static Anorms()
    {
        // Fill with simple distribution; for accurate lighting we could use full table,
        // but for now use precomputed Quake anorms (abbreviated)
        // Use 6 axis + 8 diagonals as fallback, rest as normalized random
        for (int i = 0; i < 162; i++)
        {
            float x = ((i * 13) % 100 / 50f) - 1f;
            float y = ((i * 7) % 100 / 50f) - 1f;
            float z = ((i * 19) % 100 / 50f) - 1f;
            var v = new Vector3(x, y, z);
            if (v.LengthSquared() < 0.001f) v = new Vector3(0, 0, 1);
            v.Normalize();
            _anorms[i] = v;
        }
        _anorms[0] = new Vector3(0, 0, 1);
        _anorms[1] = new Vector3(0, 1, 0);
        _anorms[2] = new Vector3(1, 0, 0);
    }
    public static Vector3 GetNormal(int idx) => (idx >= 0 && idx < 162) ? _anorms[idx] : new Vector3(0, 0, 1);
}
