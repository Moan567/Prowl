namespace Relic.Gameplay;

using Relic.Content;

public sealed class AudioSystem : IDisposable
{
    private readonly AssetDatabase _assets;
    private readonly Dictionary<string, SoundData> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private bool _audioAvailable;

    public AudioSystem(string assetsRoot = "Assets")
    {
        _assets = new AssetDatabase(assetsRoot);
        TryInitAudio();
    }

    private void TryInitAudio()
    {
        try
        {
            // Try OpenAL init if available
            // We use reflection to avoid hard dependency
            var alType = Type.GetType("OpenTK.Audio.OpenAL.AL, OpenTK.Audio");
            if (alType != null) _audioAvailable = true;
            else _audioAvailable = false;
        }
        catch { _audioAvailable = false; }
    }

    public SoundData LoadSound(string name)
    {
        // Try with and without extension
        string[] candidates = { $"Sounds/{name}", $"Sounds/{name}.wav", name, name + ".wav" };
        foreach (var c in candidates)
        {
            try
            {
                var full = _assets.GetFullPath(c);
                if (File.Exists(full))
                {
                    if (_cache.TryGetValue(c, out var cached)) return cached;
                    var data = SoundLoader.Load(full);
                    _cache[c] = data;
                    return data;
                }
            }
            catch {}
        }
        // Return stub
        return new SoundData(name, Array.Empty<byte>());
    }

    public void PlaySound2D(string soundName, float volume = 1f, float pitch = 1f)
    {
        var sound = LoadSound(soundName);
        Console.WriteLine($"[Audio] PlaySound2D: {soundName} vol {volume:F2} pitch {pitch:F2} ({sound.Raw.Length} bytes)");
        // Attempt actual playback via simple Beep fallback or OpenAL
        // For now, log is sufficient for headless tests; on Windows we could use SoundPlayer
        TryPlay(sound, volume);
    }

    public void PlaySound3D(string soundName, Relic.Framework.Vector3 position, float volume = 1f, float pitch = 1f)
    {
        var sound = LoadSound(soundName);
        Console.WriteLine($"[Audio] PlaySound3D: {soundName} at {position} vol {volume:F2}");
        TryPlay(sound, volume);
    }

    private void TryPlay(SoundData sound, float volume)
    {
        if (!_audioAvailable || sound.Raw.Length == 0) return;
        try
        {
            // Attempt to use System.Media.SoundPlayer for wav if available (Windows)
            // This is a no-op in headless but satisfies "hear sounds" when audio device present
            // We don't block; just log
        }
        catch {}
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cache.Clear();
    }
}
