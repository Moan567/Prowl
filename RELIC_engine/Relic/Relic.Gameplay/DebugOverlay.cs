namespace Relic.Gameplay;

using Relic.Framework;
using Relic.Physics;

public sealed class DebugOverlay
{
    public float Fps { get; private set; }
    public Vector3 PlayerPosition { get; private set; }
    public Vector3 PlayerVelocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public int EntityCount { get; private set; }
    public PhysicsWorld? World { get; set; }
    public Relic.Renderer.RendererStats? RendererStats { get; set; }

    private float _accumTime;
    private int _frames;
    private float _fpsUpdateInterval = 0.25f;

    public void Update(float dt, EntityManager manager, CharacterController? player)
    {
        _accumTime += dt;
        _frames++;
        if (_accumTime >= _fpsUpdateInterval)
        {
            Fps = _frames / _accumTime;
            _accumTime = 0f;
            _frames = 0;
        }
        EntityCount = manager.Count;
        if (player != null)
        {
            PlayerPosition = player.Position;
            PlayerVelocity = player.Velocity;
            IsGrounded = player.IsOnGround;
        }
        RendererStats?.SetFrameTime(dt * 1000f);
    }

    public void SetRendererStats(Relic.Renderer.RendererStats stats) => RendererStats = stats;

    public string GetText()
    {
        string baseText = $"FPS: {Fps:F0}\nPos: {PlayerPosition.X:F1},{PlayerPosition.Y:F1},{PlayerPosition.Z:F1}\nVel: {PlayerVelocity.X:F1},{PlayerVelocity.Y:F1},{PlayerVelocity.Z:F1}\nGrounded: {IsGrounded}\nEntities: {EntityCount}";
        if (RendererStats != null) baseText += $"\n{RendererStats.GetText()}";
        return baseText;
    }

    public string GetTitle()
        => $"Relic - FPS {Fps:F0} | Pos {PlayerPosition.X:F1},{PlayerPosition.Y:F1},{PlayerPosition.Z:F1} | Grounded {IsGrounded} | Entities {EntityCount}" + (RendererStats != null ? $" | {RendererStats.GetText()}" : "");
}
