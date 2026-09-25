namespace Relic.Gameplay.Materials;

using Relic.Renderer;

public sealed class MaterialManager
{
    private readonly Dictionary<string, Material> _materials = new(StringComparer.OrdinalIgnoreCase);

    public Material GetOrCreate(string textureName, Vector4? tint = null)
    {
        string key = textureName + (tint.HasValue ? $"_{tint.Value.X}_{tint.Value.Y}_{tint.Value.Z}_{tint.Value.W}" : "");
        if (_materials.TryGetValue(key, out var m)) return m;
        var mat = new Material(textureName, textureName);
        if (tint.HasValue) mat.TintColor = tint.Value;
        _materials[key] = mat;
        return mat;
    }

    public Material Create(string name, string diffuse, Vector4 tint)
    {
        var m = new Material(name, diffuse) { TintColor = tint };
        _materials[name] = m;
        return m;
    }

    public bool TryGet(string name, out Material? material) => _materials.TryGetValue(name, out material);
    public IEnumerable<Material> All => _materials.Values;
    public void Clear() => _materials.Clear();
}
