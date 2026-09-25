namespace Relic.Gameplay.Components;

using Relic.Content;
using Relic.Framework;
using Relic.Gameplay.Animation;

public sealed class AnimatedModelComponent : Component
{
    public MDLModel? Model { get; set; }
    public string ModelPath { get; set; } = "";
    public float FrameRate { get; set; } = 10f; // fps
    public bool Loop { get; set; } = true;
    public float Time { get; private set; }
    public int CurrentFrame { get; private set; }
    public int NextFrame { get; private set; }
    public float Lerp { get; private set; }

    // For idle animation, we consider all frames as idle loop
    public int StartFrame { get; set; } = 0;
    public int EndFrame { get; set; } = -1; // -1 means all
    public AnimationState CurrentState { get; private set; } = AnimationState.Idle;

    public void SetModel(MDLModel model, string path)
    {
        Model = model;
        ModelPath = path;
        if (EndFrame < 0) EndFrame = model.Frames.Count - 1;
        CurrentFrame = StartFrame;
        NextFrame = Math.Min(StartFrame + 1, EndFrame);
        Time = 0;
        Lerp = 0;
        // Default to Idle if definition exists
        SetAnimation(AnimationState.Idle);
    }

    public void SetAnimation(AnimationState state)
    {
        if (CurrentState == state && Model != null) return;
        if (Model == null) { CurrentState = state; return; }
        if (!AnimationDefinition.TryGetRange(ModelPath, state, out var range))
            return;
        CurrentState = state;
        StartFrame = Math.Clamp(range.Start, 0, Model.Frames.Count - 1);
        EndFrame = Math.Clamp(range.End, StartFrame, Model.Frames.Count - 1);
        FrameRate = range.Fps;
        Loop = range.Loop;
        CurrentFrame = StartFrame;
        NextFrame = Math.Min(StartFrame + 1, EndFrame);
        Time = 0;
        Lerp = 0;
    }

    public override void Update(float deltaTime)
    {
        if (Model == null || Model.Frames.Count <= 1) return;
        Time += deltaTime;
        float frameDuration = 1f / FrameRate;
        while (Time >= frameDuration)
        {
            Time -= frameDuration;
            CurrentFrame++;
            if (CurrentFrame > EndFrame)
            {
                if (Loop) CurrentFrame = StartFrame;
                else CurrentFrame = EndFrame;
            }
            NextFrame = CurrentFrame + 1;
            if (NextFrame > EndFrame) NextFrame = Loop ? StartFrame : EndFrame;
        }
        Lerp = Time / frameDuration;
    }

    public MeshData GetMeshData()
    {
        if (Model == null) return new MeshData();
        return MDLLoader.ToMeshData(Model, CurrentFrame, Lerp, NextFrame);
    }

    public byte[] GetSkinData()
    {
        if (Model == null || Model.SkinData.Length == 0) return Array.Empty<byte>();
        return Model.SkinData;
    }

    public int SkinWidth => Model?.SkinWidth ?? 0;
    public int SkinHeight => Model?.SkinHeight ?? 0;
}
