namespace Relic.Framework;

public sealed class GameTime
{
    public float TotalTime { get; private set; }
    public float DeltaTime { get; private set; }
    public int FrameCount { get; private set; }
    public float FramesPerSecond { get; private set; }

    private float _fpsAccum;
    private int _fpsFrames;

    internal void Tick(float deltaTime)
    {
        DeltaTime = deltaTime;
        TotalTime += deltaTime;
        FrameCount++;

        _fpsAccum += deltaTime;
        _fpsFrames++;

        if (_fpsAccum >= 1f)
        {
            FramesPerSecond = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }
    }
}
