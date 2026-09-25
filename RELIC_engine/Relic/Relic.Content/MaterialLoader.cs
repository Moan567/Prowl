namespace Relic.Content;

public static class MaterialLoader
{
    public static RelicMaterial Load(string fullPath, string name)
    {
        var mat = new RelicMaterial(name);
        if (!File.Exists(fullPath))
        {
            // Fallback to defaults, try to infer diffuse from name
            mat.DiffuseTexture = $"{name}.png";
            return mat;
        }
        foreach (var raw in File.ReadAllLines(fullPath))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            var parts = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0].ToLowerInvariant())
            {
                case "diffuse":
                case "texture":
                case "map":
                    if (parts.Length >= 2) mat.DiffuseTexture = parts[1];
                    break;
                case "normal":
                case "normalmap":
                case "bump":
                    if (parts.Length >= 2) mat.NormalTexture = parts[1];
                    break;
                case "specular":
                case "spec":
                    if (parts.Length >= 2) mat.SpecularTexture = parts[1];
                    break;
                case "emission":
                case "emissive":
                    if (parts.Length >= 2) mat.EmissionTexture = parts[1];
                    break;
                case "roughness":
                case "rough":
                    if (parts.Length >= 2) mat.RoughnessTexture = parts[1];
                    break;
                case "tint":
                case "color":
                    if (parts.Length >= 4)
                    {
                        if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) &&
                            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g) &&
                            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
                        {
                            float a = 1f;
                            if (parts.Length >= 5) float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a);
                            mat.Tint = new System.Numerics.Vector4(r,g,b,a);
                        }
                    }
                    break;
            }
        }
        return mat;
    }
}
