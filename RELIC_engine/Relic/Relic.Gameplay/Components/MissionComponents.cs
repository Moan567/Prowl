namespace Relic.Gameplay.Components;

using Relic.Framework;

// Phase 10: mission system (objectives, checkpoints, mission flow).
public sealed class ObjectiveComponent : Component
{
    public string ObjectiveId { get; set; } = "obj1";
    public string Description { get; set; } = "";
    public int Required { get; set; } = 1;
    public int Progress { get; private set; }
    public bool IsComplete => Progress >= Required;
    public event Action? OnCompleted;
    private bool _fired;

    public void AddProgress(int amount = 1)
    {
        if (IsComplete) return;
        Progress = Math.Min(Required, Progress + amount);
        if (IsComplete && !_fired)
        {
            _fired = true;
            OnCompleted?.Invoke();
        }
    }
}

public sealed class CheckpointComponent : Component
{
    public string CheckpointId { get; set; } = "cp1";
    public bool Activated { get; private set; }
    public Vector3 SavedPosition { get; private set; }
    public event Action? OnActivated;

    public void Activate(Vector3 playerPosition)
    {
        Activated = true;
        SavedPosition = playerPosition;
        OnActivated?.Invoke();
    }
}

public sealed class MissionStartComponent : Component
{
    public string MissionName { get; set; } = "mission1";
    public bool Started { get; private set; }
    public event Action? OnStarted;

    public void Start()
    {
        if (Started) return;
        Started = true;
        OnStarted?.Invoke();
    }
}

public sealed class MissionEndComponent : Component
{
    public bool IsComplete { get; private set; }
    public event Action? OnCompleted;

    public bool CheckAll(IEnumerable<ObjectiveComponent> objectives)
    {
        if (IsComplete) return true;
        if (objectives.All(o => o.IsComplete))
        {
            IsComplete = true;
            OnCompleted?.Invoke();
            return true;
        }
        return false;
    }
}
