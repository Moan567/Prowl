namespace Relic.Renderer.OpenGL;

using Relic.Framework;
using Relic.Windowing;
using OpenTK.Windowing.GraphicsLibraryFramework;

public sealed class FreeCamera
{
    public Camera Camera { get; }
    private readonly RelicWindow _window;

    public float MoveSpeed { get; set; } = 300f;
    public float SprintMultiplier { get; set; } = 2.5f;
    public float MouseSensitivity { get; set; } = 0.0025f;

    private float _yaw;
    private float _pitch;

    public FreeCamera(Camera camera, RelicWindow window, Vector3 startPos = default, float startYaw = 0, float startPitch = 0)
    {
        Camera = camera;
        _window = window;
        _yaw = startYaw;
        _pitch = startPitch;
        if (startPos != default) Camera.Position = startPos;
        else if (Camera.Position == Vector3.Zero) Camera.Position = new Vector3(0, 0, 80);
        Camera.Rotation = new Vector3(_pitch, _yaw, 0);
        Camera.MarkViewDirty();
    }

    public void Update(float deltaTime)
    {
        // Mouse look
        var delta = _window.MouseDelta;
        _yaw += delta.X * MouseSensitivity;
        _pitch -= delta.Y * MouseSensitivity;
        _pitch = Math.Clamp(_pitch, -1.553f, 1.553f); // ~89 degrees

        Camera.Rotation = new Vector3(_pitch, _yaw, 0);
        Camera.MarkViewDirty();

        // Movement
        float speed = MoveSpeed * deltaTime;
        if (_window.IsKeyDown(Keys.LeftShift) || _window.IsKeyDown(Keys.RightShift))
            speed *= SprintMultiplier;

        Vector3 forward = Camera.Forward;
        Vector3 right = Camera.Right;
        Vector3 up = Vector3.Up;

        Vector3 move = Vector3.Zero;
        if (_window.IsKeyDown(Keys.W)) move += forward;
        if (_window.IsKeyDown(Keys.S)) move -= forward;
        if (_window.IsKeyDown(Keys.A)) move -= right;
        if (_window.IsKeyDown(Keys.D)) move += right;
        if (_window.IsKeyDown(Keys.Space)) move += up;
        if (_window.IsKeyDown(Keys.LeftControl) || _window.IsKeyDown(Keys.RightControl)) move -= up;

        if (move.LengthSquared() > 0)
        {
            move.Normalize();
            Camera.Position += move * speed;
            Camera.MarkViewDirty();
        }

        Camera.Update();
    }
}
