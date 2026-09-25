namespace Relic.Renderer.OpenGL;

using Relic.Framework;
using Relic.Renderer;

// Relic is permanently Z-Up (X forward at yaw 0, Y right, Z up)
public sealed class Camera : ICamera
{
    private Matrix4x4 _view;
    private Matrix4x4 _proj;
    private bool _dirtyView = true;
    private bool _dirtyProj = true;

    public Vector3 Position { get; set; } = new(0, 0, 3);
    public Vector3 Rotation { get; set; } = Vector3.Zero; // pitch(x), yaw(y), roll(z) in radians — Z-Up
    public float FieldOfView { get; set; } = 75f * MathF.PI / 180f;
    public float AspectRatio { get; set; } = 16f / 9f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 4096f;

    public Matrix4x4 ViewMatrix
    {
        get { if (_dirtyView) RecalcView(); return _view; }
    }

    public Matrix4x4 ProjectionMatrix
    {
        get { if (_dirtyProj) RecalcProj(); return _proj; }
    }

    public Matrix4x4 ViewProjectionMatrix => ProjectionMatrix * ViewMatrix;

    // Z-Up forward: yaw around Z, pitch up toward +Z
    public Vector3 Forward
    {
        get
        {
            float yaw = Rotation.Y;
            float pitch = Rotation.X;
            return new Vector3(
                MathF.Cos(pitch) * MathF.Cos(yaw),
                MathF.Cos(pitch) * MathF.Sin(yaw),
                MathF.Sin(pitch)
            );
        }
    }

    public Vector3 Right
    {
        get
        {
            float yaw = Rotation.Y;
            return new Vector3(-MathF.Sin(yaw), MathF.Cos(yaw), 0);
        }
    }

    public Vector3 Up => new Vector3(0, 0, 1);

    public void Update()
    {
        if (_dirtyView) RecalcView();
        if (_dirtyProj) RecalcProj();
    }

    private void RecalcView()
    {
        var target = Position + Forward;
        _view = Matrix4x4.CreateLookAt(Position, target, new Vector3(0, 0, 1));
        _dirtyView = false;
    }

    private void RecalcProj()
    {
        _proj = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
        _dirtyProj = false;
    }

    public void SetRotation(float pitch, float yaw)
    {
        Rotation = new Vector3(pitch, yaw, 0);
        _dirtyView = true;
    }

    public void LookAt(Vector3 eye, Vector3 target)
    {
        Position = eye;
        var dir = Vector3.Normalize(target - eye);
        float yaw = MathF.Atan2(dir.Y, dir.X);
        float pitch = MathF.Asin(Math.Clamp(dir.Z, -1f, 1f));
        Rotation = new Vector3(pitch, yaw, 0);
        _dirtyView = true;
    }

    // Kept for compatibility with PlayerController (now just calls LookAt)
    public void LookAtZUp(Vector3 eye, Vector3 target) => LookAt(eye, target);

    public void SetViewMatrix(Matrix4x4 view)
    {
        _view = view;
        _dirtyView = false;
    }

    public void MarkViewDirty() => _dirtyView = true;
    public void MarkProjectionDirty() => _dirtyProj = true;
}
