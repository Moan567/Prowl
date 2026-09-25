namespace Relic.Gameplay.Components;

using Relic.Gameplay.Materials;

public sealed class RenderComponent : Component
{
    public Material? Material { get; set; }
    public bool Visible { get; set; } = true;
    public string MeshGroup { get; set; } = string.Empty;
}
