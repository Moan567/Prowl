namespace Relic.Gameplay.Components;

using Relic.Framework;

public sealed class TransformComponent : Component
{
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero; // Euler pitch(x) yaw(y) roll(z) Z-Up
    public Vector3 Scale { get; set; } = Vector3.One;
    public Vector3 Forward => Vector3.Transform(Vector3.Forward, Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z));
    public Vector3 Right => Vector3.Transform(Vector3.Right, Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z));
    public Vector3 Up => Vector3.Transform(Vector3.Up, Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z));
    public Matrix4x4 LocalMatrix => Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z) * Matrix4x4.CreateTranslation(Position);
    public Matrix4x4 WorldMatrix => LocalMatrix;
    public void SetPosition(float x,float y,float z) => Position = new Vector3(x,y,z);
    public void Translate(Vector3 o) => Position += o;
}
