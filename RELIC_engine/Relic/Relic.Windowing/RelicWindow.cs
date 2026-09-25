namespace Relic.Windowing;

using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

public sealed class RelicWindowSettings
{
    public string Title { get; set; } = "Relic";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool VSync { get; set; } = false;
    public bool StartVisible { get; set; } = true;
    public bool LockCursor { get; set; } = true;
}

public sealed class RelicWindow : IDisposable
{
    private readonly GameWindow _window;
    private bool _disposed;

    public int Width => _window.ClientSize.X;
    public int Height => _window.ClientSize.Y;
    public float AspectRatio => Width / (float)Height;
    public bool IsExiting => _window.IsExiting;

    // High-level events - beginner friendly
    public event Action<float>? UpdateFrame;
    public event Action? RenderFrame;
    public event Action<int,int>? Resized;

    public GameWindow Native => _window;

    public RelicWindow(RelicWindowSettings? settings = null)
    {
        settings ??= new RelicWindowSettings();

        var gws = GameWindowSettings.Default;

        var nws = NativeWindowSettings.Default;
        nws.Title = settings.Title;
        nws.ClientSize = new Vector2i(settings.Width, settings.Height);
        nws.API = ContextAPI.OpenGL;
        nws.APIVersion = new Version(4, 6);
        nws.Profile = ContextProfile.Core;
        nws.Flags = ContextFlags.ForwardCompatible;
        nws.Vsync = settings.VSync ? VSyncMode.On : VSyncMode.Off;
        nws.StartVisible = settings.StartVisible;

        _window = new GameWindow(gws, nws);

        _window.Load += OnLoad;
        _window.Unload += OnUnload;
        _window.UpdateFrame += OnUpdateFrame;
        _window.RenderFrame += OnRenderFrame;
        _window.Resize += OnResize;
        _window.FocusedChanged += e =>
        {
            if (settings.LockCursor)
                _window.CursorState = e.IsFocused ? CursorState.Grabbed : CursorState.Normal;
        };
    }

    private void OnLoad()
    {
        if (Native != null)
        {
            // Ensure cursor grabbed on start
            _window.CursorState = CursorState.Grabbed;
        }
    }

    private void OnUnload() { }

    private void OnUpdateFrame(FrameEventArgs args)
    {
        if (_window.KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape))
            _window.Close();

        UpdateFrame?.Invoke((float)args.Time);
    }

    private void OnRenderFrame(FrameEventArgs args)
    {
        RenderFrame?.Invoke();
        _window.SwapBuffers();
    }

    private void OnResize(ResizeEventArgs e)
    {
        Resized?.Invoke(e.Width, e.Height);
    }

    public void Run() => _window.Run();

    public void Close() => _window.Close();

    public bool IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys key) => _window.KeyboardState.IsKeyDown(key);

    public bool IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton button) => _window.MouseState.IsButtonDown(button);

    public Vector2 MouseDelta => new(_window.MouseState.Delta.X, _window.MouseState.Delta.Y);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.Dispose();
    }
}
