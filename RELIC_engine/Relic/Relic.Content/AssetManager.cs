namespace Relic.Content;

using Relic.Framework;

public sealed class AssetManager
{
    private readonly string _assetsRoot;
    private readonly Dictionary<string, byte[]> _cache = new();

    public AssetManager(string? assetsRoot = null)
    {
        _assetsRoot = assetsRoot ?? "Assets";
        if (!Directory.Exists(_assetsRoot))
            Directory.CreateDirectory(_assetsRoot);
    }

    public string GetFullPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(_assetsRoot, relativePath));
    }

    public byte[] LoadRaw(string relativePath)
    {
        string fullPath = GetFullPath(relativePath);
        
        if (_cache.TryGetValue(fullPath, out var cached))
            return cached;

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Asset not found: {fullPath}");

        var data = File.ReadAllBytes(fullPath);
        _cache[fullPath] = data;
        return data;
    }

    public string LoadText(string relativePath)
    {
        var bytes = LoadRaw(relativePath);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public MapData LoadMap(string relativePath)
    {
        var text = LoadText(relativePath);
        return MapLoader.Parse(text);
    }

    public void ClearCache()
    {
        _cache.Clear();
    }
}