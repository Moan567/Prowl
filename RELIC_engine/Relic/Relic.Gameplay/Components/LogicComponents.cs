namespace Relic.Gameplay.Components;

// Phase 10: logic system. Data-driven puzzle / mission-flow primitives.
public sealed class LogicTimerComponent : Component
{
    public float Interval { get; set; } = 5f;
    public bool StartOn { get; set; } = true;
    public int FireCount { get; private set; }
    public event Action? OnFired;
    private float _timer;

    public override void Update(float deltaTime)
    {
        if (!StartOn || !Enabled) return;
        _timer += deltaTime;
        if (_timer >= Interval)
        {
            _timer = 0f;
            FireCount++;
            OnFired?.Invoke();
        }
    }
}

public sealed class LogicRandomComponent : Component
{
    public int Options { get; set; } = 2;
    public int Seed { get; set; } = 12345;
    public int LastIndex { get; private set; } = -1;
    public int RollCount { get; private set; }
    private Random? _random;

    public int Roll()
    {
        _random ??= new Random(Seed);
        if (Options <= 0) return -1;
        LastIndex = _random.Next(Options);
        RollCount++;
        return LastIndex;
    }
}

public sealed class LogicRelayComponent : Component
{
    public string Target { get; set; } = "";
    public int FireCount { get; private set; }
    public event Action<string>? OnFire;

    public void Fire()
    {
        FireCount++;
        OnFire?.Invoke(Target);
    }
}

public sealed class LogicCounterComponent : Component
{
    public int TargetCount { get; set; } = 3;
    public int Count { get; private set; }
    public bool Reached => Count >= TargetCount;
    public event Action? OnReached;
    private bool _fired;

    public void Add(int amount = 1)
    {
        Count += amount;
        if (!_fired && Reached)
        {
            _fired = true;
            OnReached?.Invoke();
        }
    }

    public void Reset() { Count = 0; _fired = false; }
}

public sealed class LogicCompareComponent : Component
{
    public float A { get; set; }
    public float B { get; set; }
    public string Op { get; set; } = "=="; // ==, !=, <, <=, >, >=
    public bool Result { get; private set; }
    public event Action<bool>? OnEvaluated;

    public bool Evaluate()
    {
        Result = Op switch
        {
            "==" => A == B,
            "!=" => A != B,
            "<" => A < B,
            "<=" => A <= B,
            ">" => A > B,
            ">=" => A >= B,
            _ => false,
        };
        OnEvaluated?.Invoke(Result);
        return Result;
    }
}
