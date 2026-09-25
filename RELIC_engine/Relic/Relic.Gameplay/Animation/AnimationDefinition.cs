namespace Relic.Gameplay.Animation;

public struct FrameRange
{
    public int Start;
    public int End;
    public float Fps;
    public bool Loop;
    public FrameRange(int start, int end, float fps = 10f, bool loop = true) { Start = start; End = end; Fps = fps; Loop = loop; }
}

public static class AnimationDefinition
{
    private static readonly Dictionary<string, Dictionary<AnimationState, FrameRange>> _defs = new(StringComparer.OrdinalIgnoreCase);

    static AnimationDefinition()
    {
        // dog.mdl: attack 0-7, death 8-16, pain 26-31, run 48-59, stand 69-77, walk 78-85
        _defs["dog.mdl"] = new Dictionary<AnimationState, FrameRange>
        {
            [AnimationState.Idle] = new FrameRange(69, 77, 8f, true),
            [AnimationState.Walk] = new FrameRange(78, 85, 10f, true),
            [AnimationState.Attack] = new FrameRange(0, 7, 12f, false),
            [AnimationState.Pain] = new FrameRange(26, 31, 10f, false),
            [AnimationState.Death] = new FrameRange(8, 16, 8f, false),
        };
        // demon.mdl: stand 0-12, walk 13-20, run 21-26, attack 54-68, pain 39-44, death 45-53
        _defs["demon.mdl"] = new Dictionary<AnimationState, FrameRange>
        {
            [AnimationState.Idle] = new FrameRange(0, 12, 6f, true),
            [AnimationState.Walk] = new FrameRange(13, 20, 8f, true),
            [AnimationState.Attack] = new FrameRange(54, 68, 10f, false),
            [AnimationState.Pain] = new FrameRange(39, 44, 10f, false),
            [AnimationState.Death] = new FrameRange(45, 53, 8f, false),
        };
        // soldier.mdl: stand 0-7, run 73-80, shoot 81-89, pain 40-45, death 8-16
        _defs["soldier.mdl"] = new Dictionary<AnimationState, FrameRange>
        {
            [AnimationState.Idle] = new FrameRange(0, 7, 6f, true),
            [AnimationState.Walk] = new FrameRange(73, 80, 10f, true),
            [AnimationState.Attack] = new FrameRange(81, 89, 12f, false),
            [AnimationState.Pain] = new FrameRange(40, 45, 10f, false),
            [AnimationState.Death] = new FrameRange(8, 16, 8f, false),
        };
        // crate.obj fallback: single frame
        _defs["crate.obj"] = new Dictionary<AnimationState, FrameRange>
        {
            [AnimationState.Idle] = new FrameRange(0, 0, 1f, true),
            [AnimationState.Walk] = new FrameRange(0, 0, 1f, true),
            [AnimationState.Attack] = new FrameRange(0, 0, 1f, false),
            [AnimationState.Pain] = new FrameRange(0, 0, 1f, false),
            [AnimationState.Death] = new FrameRange(0, 0, 1f, false),
        };
    }

    public static bool TryGetRange(string modelPath, AnimationState state, out FrameRange range)
    {
        var key = System.IO.Path.GetFileName(modelPath);
        if (_defs.TryGetValue(key, out var map) && map.TryGetValue(state, out range)) return true;
        // fallback to crate
        if (_defs.TryGetValue("crate.obj", out var fallback) && fallback.TryGetValue(state, out range)) return true;
        range = new FrameRange(0, 0);
        return false;
    }

    public static FrameRange GetRange(string modelPath, AnimationState state)
    {
        TryGetRange(modelPath, state, out var r);
        return r;
    }
}
