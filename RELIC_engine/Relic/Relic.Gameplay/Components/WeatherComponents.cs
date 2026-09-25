namespace Relic.Gameplay.Components;

using Relic.Framework;

// Phase 10: environment weather systems (env_rain, env_snow, env_lightning,
// env_wind, env_skybox, env_ambient). Data-driven; tick-based simulation.
public enum WeatherKind
{
    Rain,
    Snow,
    Lightning,
    Wind,
    Skybox,
    Ambient,
}

public sealed class WeatherComponent : Component
{
    public WeatherKind Kind { get; set; } = WeatherKind.Rain;
    public float Density { get; set; } = 0.5f;
    public float Intensity { get; set; } = 1f;
    public string Skybox { get; set; } = "";
    public Vector3 Direction { get; set; } = new(0, 0, -1);

    public int TickCount { get; private set; }
    public int StrikeCount { get; private set; }
    public float Time { get; private set; }
    public event Action? OnStrike;
    private float _strikeTimer = 4f;

    public override void Update(float deltaTime)
    {
        if (!Enabled) return;
        Time += deltaTime;
        TickCount++;
        if (Kind == WeatherKind.Lightning)
        {
            _strikeTimer -= deltaTime;
            if (_strikeTimer <= 0f)
            {
                _strikeTimer = 4f + (Time % 3f);
                StrikeCount++;
                OnStrike?.Invoke();
            }
        }
    }
}
