namespace Relic.Content;

public sealed class AssetDatabase
{
    private readonly string _root;
    private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AssetDatabase(string root = "Assets")
    {
        _root = root;
        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        foreach (var dir in new[] { "Models", "Textures", "Sounds", "Materials", "Maps" })
        {
            var path = Path.Combine(_root, dir);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }

    public string GetFullPath(string relative)
        => Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string relative) => File.Exists(GetFullPath(relative));

    public string LoadText(string relative)
        => File.ReadAllText(GetFullPath(relative));

    public T GetOrLoad<T>(string relative, Func<string, T> loader) where T : class
    {
        if (_cache.TryGetValue(relative, out var obj) && obj is T t) return t;
        var full = GetFullPath(relative);
        var loaded = loader(full);
        _cache[relative] = loaded;
        return loaded;
    }

    public void Clear() => _cache.Clear();

    // Convenience shortcuts
    public RelicMaterial LoadMaterial(string name) => MaterialLoader.Load(GetFullPath($"Materials/{name}.mat"), name);
    public RelicMaterial LoadMaterialByPath(string relative) => MaterialLoader.Load(GetFullPath(relative), Path.GetFileNameWithoutExtension(relative));
    public ModelData LoadModel(string name) => ModelLoader.Load(GetFullPath($"Models/{name}"));
    public MDLModel LoadMDL(string name) => MDLLoader.Load(GetFullPath($"Models/{name}"));
    public SoundData LoadSound(string name) => SoundLoader.Load(GetFullPath($"Sounds/{name}"));

    public IEnumerable<string> ListMaterials() => Directory.Exists(Path.Combine(_root, "Materials")) ? Directory.GetFiles(Path.Combine(_root, "Materials"), "*.mat").Select(Path.GetFileNameWithoutExtension)! : Enumerable.Empty<string>();
    public IEnumerable<string> ListModels() => Directory.Exists(Path.Combine(_root, "Models")) ? Directory.GetFiles(Path.Combine(_root, "Models"), "*.*").Select(Path.GetFileName)! : Enumerable.Empty<string>();

    // TrenchBroom integration queries.
    public const string TextureGroup = "Relic";
    public IEnumerable<string> ListTextures()
    {
        var dir = Path.Combine(_root, "Textures");
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        return Directory.GetFiles(dir)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)!;
    }
    public IEnumerable<string> ListTemplates()
    {
        var dir = Path.Combine(_root, "Templates");
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        return Directory.GetFiles(dir, "*.map").Select(Path.GetFileName)!;
    }

    // Model preview info for TrenchBroom: TrenchBroom renders .obj directly;
    // Quake .mdl entities fall back to the FGD bounding box.
    public (bool PreviewSupported, string Fallback) GetModelPreview(string modelName)
    {
        var ext = Path.GetExtension(modelName).ToLowerInvariant();
        if (ext == ".obj") return (true, "mesh");
        if (ext == ".mdl") return (false, "boundingbox");
        return (false, "boundingbox");
    }
}

public sealed class RelicMaterial
{
    public string Name { get; }
    public string DiffuseTexture { get; set; } = "brick.png";
    public string NormalTexture { get; set; } = "";
    public string SpecularTexture { get; set; } = "";
    public string EmissionTexture { get; set; } = "";
    public string RoughnessTexture { get; set; } = "";
    public System.Numerics.Vector4 Tint { get; set; } = new(1,1,1,1);
    public RelicMaterial(string name) => Name = name;
}

public sealed class ModelData
{
    public List<Relic.Framework.Vector3> Positions { get; } = new();
    public List<Relic.Framework.Vector2> TexCoords { get; } = new();
    public List<Relic.Framework.Vector3> Normals { get; } = new();
    public List<Relic.Framework.Vector3> Tangents { get; } = new();
    public List<uint> Indices { get; } = new();
    public Relic.Content.MeshData ToMeshData()
    {
        // Ensure tangents
        if (Tangents.Count != Positions.Count) GenerateTangents();
        var md = new MeshData();
        for (int i = 0; i < Positions.Count; i++)
        {
            var p = Positions[i];
            var n = i < Normals.Count ? Normals[i] : new Relic.Framework.Vector3(0,0,1);
            var uv = i < TexCoords.Count ? TexCoords[i] : Relic.Framework.Vector2.Zero;
            var t = i < Tangents.Count ? Tangents[i] : new Relic.Framework.Vector3(1,0,0);
            md.Vertices.Add(new MeshVertex(p, n, uv, t));
        }
        foreach (var idx in Indices) md.Indices.Add(idx);
        return md;
    }

    public void GenerateTangents()
    {
        Tangents.Clear();
        Tangents.AddRange(new Relic.Framework.Vector3[Positions.Count]);
        for (int i = 0; i < Indices.Count; i += 3)
        {
            if (i + 2 >= Indices.Count) break;
            uint i0 = Indices[i], i1 = Indices[i+1], i2 = Indices[i+2];
            if (i0 >= Positions.Count || i1 >= Positions.Count || i2 >= Positions.Count) continue;
            var p0 = Positions[(int)i0]; var p1 = Positions[(int)i1]; var p2 = Positions[(int)i2];
            var uv0 = i0 < (uint)TexCoords.Count ? TexCoords[(int)i0] : Relic.Framework.Vector2.Zero;
            var uv1 = i1 < (uint)TexCoords.Count ? TexCoords[(int)i1] : Relic.Framework.Vector2.Zero;
            var uv2 = i2 < (uint)TexCoords.Count ? TexCoords[(int)i2] : Relic.Framework.Vector2.Zero;
            var edge1 = p1 - p0;
            var edge2 = p2 - p0;
            var dUV1 = uv1 - uv0;
            var dUV2 = uv2 - uv0;
            float denom = dUV1.X * dUV2.Y - dUV2.X * dUV1.Y;
            Relic.Framework.Vector3 tangent;
            if (MathF.Abs(denom) < 1e-6f) tangent = new Relic.Framework.Vector3(1,0,0);
            else
            {
                float r = 1f / denom;
                tangent = new Relic.Framework.Vector3(
                    (dUV2.Y * edge1.X - dUV1.Y * edge2.X) * r,
                    (dUV2.Y * edge1.Y - dUV1.Y * edge2.Y) * r,
                    (dUV2.Y * edge1.Z - dUV1.Y * edge2.Z) * r);
                tangent.Normalize();
            }
            Tangents[(int)i0] = tangent;
            Tangents[(int)i1] = tangent;
            Tangents[(int)i2] = tangent;
        }
        // Fill any zero tangents
        for (int i = 0; i < Tangents.Count; i++) if (Tangents[i].LengthSquared() < 0.001f) Tangents[i] = new Relic.Framework.Vector3(1,0,0);
    }
}

public sealed class SoundData
{
    public string Path { get; }
    public byte[] Raw { get; }
    public SoundData(string path, byte[] raw) { Path = path; Raw = raw; }
}
