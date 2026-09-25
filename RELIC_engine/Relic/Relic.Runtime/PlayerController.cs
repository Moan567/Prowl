namespace Relic.Runtime;

using Relic.Framework;
using Relic.Physics;
using Relic.Windowing;
using Relic.Renderer.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

public sealed class PlayerController
{
    public CharacterController Character { get; }
    private readonly RelicWindow _window;
    private readonly Camera _camera;

    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool SprintHeld => _window.IsKeyDown(Keys.LeftShift) || _window.IsKeyDown(Keys.RightShift);

    private float _yaw;
    private float _pitch;
    private bool _wasJumpDown;

    public PlayerController(CharacterController character, Camera camera, RelicWindow window)
    {
        Character = character;
        _camera = camera;
        _window = window;
        _yaw = character.Yaw;
        _pitch = 0;
        SyncCamera();
    }

    public void Update(float dt)
    {
        // Mouse look — yaw around Z, pitch limited
        var d = _window.MouseDelta;
        _yaw += d.X * MouseSensitivity;
        _pitch -= d.Y * MouseSensitivity;
        _pitch = Math.Clamp(_pitch, -1.553f, 1.553f); // +-89 degrees
        Character.SetYaw(_yaw);

        // Build wish dir from WASD (horizontal plane, Z=0)
        Vector3 wish = Vector3.Zero;
        Vector3 fwd = Character.GetForward(); // horizontal
        Vector3 right = Character.GetRight();
        if (_window.IsKeyDown(Keys.W)) wish += fwd;
        if (_window.IsKeyDown(Keys.S)) wish -= fwd;
        if (_window.IsKeyDown(Keys.A)) wish -= right;
        if (_window.IsKeyDown(Keys.D)) wish += right;

        if (wish.LengthSquared() > 0) wish.Normalize();

        bool jumpDown = _window.IsKeyDown(Keys.Space);
        bool jumpPressed = jumpDown && !_wasJumpDown;
        _wasJumpDown = jumpDown;

        // Let character handle physics (Z-up)
        Character.Move(wish, SprintHeld, jumpPressed, dt);

        SyncCamera();
    }

    private void SyncCamera()
    {
        // Camera follows eye position, looks along character yaw/pitch with Z-up
        Vector3 eye = Character.EyePosition;
        _camera.Position = eye;
        // Build forward with Z up: yaw around Z, pitch up
        Vector3 forward = new(
            MathF.Cos(_pitch) * MathF.Cos(_yaw),
            MathF.Cos(_pitch) * MathF.Sin(_yaw),
            MathF.Sin(_pitch)
        );
        Vector3 target = eye + forward;
        // Use Z-up LookAt
        var view = Matrix4x4.CreateLookAt(eye, target, new Vector3(0, 0, 1));
        // We cannot set ViewMatrix directly, so set Position/Rotation and mark dirty then override?
        // Simpler: set Rotation to represent yaw/pitch and update via custom?
        // Our Camera currently uses Y-up math. For Z-up we need to override matrices manually.
        // Workaround: set camera Position and directly assign view via reflection? Easier: update Camera to support Z-up LookAt.
        // For now we set camera internals via LookAt helper that uses Z-up
        _camera.LookAtZUp(eye, target);
    }
}
