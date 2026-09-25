namespace Relic.Gameplay.Materials;

using Relic.Framework;
using Relic.Renderer;

public sealed class Material
{
    public string Name { get; }
    public string DiffuseTextureName { get; set; }
    public string NormalTextureName { get; set; } = "";
    public string SpecularTextureName { get; set; } = "";
    public string EmissionTextureName { get; set; } = "";
    public string RoughnessTextureName { get; set; } = "";
    public Vector4 TintColor { get; set; } = new(1,1,1,1);
    public Material(string name, string diffuseTextureName = "white")
    {
        Name = name;
        DiffuseTextureName = diffuseTextureName;
    }
}
