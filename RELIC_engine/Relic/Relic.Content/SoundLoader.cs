namespace Relic.Content;

public static class SoundLoader
{
    public static SoundData Load(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            // Return stub with empty data; AudioSystem will handle missing gracefully
            return new SoundData(fullPath, Array.Empty<byte>());
        }
        var raw = File.ReadAllBytes(fullPath);
        return new SoundData(fullPath, raw);
    }
}
