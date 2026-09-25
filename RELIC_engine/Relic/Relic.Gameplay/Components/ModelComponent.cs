namespace Relic.Gameplay.Components;

public sealed class ModelComponent : Component
{
    public string ModelPath { get; set; } = "crate.obj";
    public bool IsLoaded { get; set; }
    public Relic.Content.ModelData? ModelData { get; set; }
    public Relic.Content.RelicMaterial? Material { get; set; }
}
