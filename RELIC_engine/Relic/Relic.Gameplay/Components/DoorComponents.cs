namespace Relic.Gameplay.Components;

using Relic.Framework;

// Phase 10: doors & movers. One component covers func_door,
// func_door_rotating, func_button, func_elevator, func_platform, func_train.
// Movement is data-driven (speed/wait/locked/targetname/target from .map).
public enum DoorKind
{
    Door,
    RotatingDoor,
    Button,
    Elevator,
    Platform,
    Train,
}

public enum MoverState
{
    Closed,
    Opening,
    Open,
    Closing,
}

public sealed class DoorComponent : Component
{
    public DoorKind Kind { get; set; } = DoorKind.Door;
    public float Speed { get; set; } = 100f;
    public float Wait { get; set; } = 3f;
    public bool Locked { get; set; }
    public Vector3 Direction { get; set; } = new(0, 0, 1);
    public float Distance { get; set; } = 64f;
    public float RotateAngle { get; set; } = 90f;

    public MoverState State { get; private set; } = MoverState.Closed;
    public int OpenCount { get; private set; }
    public event Action? OnOpened;
    public event Action? OnClosed;

    private Vector3 _closedPos;
    private Vector3 _openPos;
    private float _closedYaw;
    private bool _initialized;
    private float _waitTimer = -1f;

    private void EnsureInit()
    {
        if (_initialized || Entity == null) return;
        _initialized = true;
        _closedPos = Entity.Transform.Position;
        _openPos = _closedPos + Direction * Distance;
        _closedYaw = Entity.Transform.Rotation.Y;
    }

    public void Open()
    {
        EnsureInit();
        if (Locked || State == MoverState.Open || State == MoverState.Opening) return;
        State = MoverState.Opening;
        _waitTimer = -1f;
        OpenCount++;
        OnOpened?.Invoke();
    }

    public void Close()
    {
        EnsureInit();
        if (State == MoverState.Closed || State == MoverState.Closing) return;
        State = MoverState.Closing;
    }

    public void Toggle()
    {
        if (State == MoverState.Closed || State == MoverState.Closing) Open();
        else Close();
    }

    public void Use() => Toggle();

    public override void Update(float deltaTime)
    {
        EnsureInit();
        if (Entity == null) return;
        if (Kind == DoorKind.RotatingDoor)
        {
            float targetYaw = (State == MoverState.Open || State == MoverState.Opening)
                ? _closedYaw + RotateAngle * MathF.PI / 180f : _closedYaw;
            var rot = Entity.Transform.Rotation;
            float diff = targetYaw - rot.Y;
            float step = Speed * MathF.PI / 180f * deltaTime;
            if (MathF.Abs(diff) <= step)
            {
                rot.Y = targetYaw;
                if (State == MoverState.Opening) { State = MoverState.Open; _waitTimer = Wait; }
                else if (State == MoverState.Closing) { State = MoverState.Closed; OnClosed?.Invoke(); }
            }
            else rot.Y += MathF.Sign(diff) * step;
            Entity.Transform.Rotation = rot;
        }
        else
        {
            Vector3 target = (State == MoverState.Open || State == MoverState.Opening) ? _openPos : _closedPos;
            Vector3 pos = Entity.Transform.Position;
            Vector3 delta = target - pos;
            float len = delta.Length();
            float step = Speed * deltaTime;
            if (len <= step)
            {
                Entity.Transform.Position = target;
                if (State == MoverState.Opening) { State = MoverState.Open; _waitTimer = Wait; }
                else if (State == MoverState.Closing) { State = MoverState.Closed; OnClosed?.Invoke(); }
            }
            else Entity.Transform.Position = pos + delta / len * step;
        }

        if (State == MoverState.Open && Kind != DoorKind.Button)
        {
            if (Wait >= 0f)
            {
                if (_waitTimer < 0f) _waitTimer = Wait;
                _waitTimer -= deltaTime;
                if (_waitTimer <= 0f) Close();
            }
        }
        else if (State == MoverState.Open && Kind == DoorKind.Button)
        {
            if (_waitTimer < 0f) _waitTimer = Wait;
            _waitTimer -= deltaTime;
            if (_waitTimer <= 0f) Close();
        }
    }
}
