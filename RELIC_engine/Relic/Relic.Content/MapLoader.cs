namespace Relic.Content;

using System.Globalization;
using System.Text;
using Relic.Framework;

public static class MapLoader
{
    public static MapData Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Map file not found: {filePath}");
        var text = File.ReadAllText(filePath);
        return Parse(text);
    }

    public static MapData Parse(string mapText)
    {
        var map = new MapData();
        var tokens = Tokenize(mapText);
        int i = 0;

        while (i < tokens.Count)
        {
            if (tokens[i] != "{") { i++; continue; }
            i++; // skip {

            // Collect entity key-values and brushes
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var brushes = new List<MapBrush>();

            while (i < tokens.Count && tokens[i] != "}")
            {
                if (tokens[i] == "{")
                {
                    // brush start
                    var brush = ParseBrush(tokens, ref i);
                    if (brush != null) brushes.Add(brush);
                }
                else
                {
                    // key value pair
                    string key = tokens[i++];
                    if (i >= tokens.Count) break;
                    // if next is "{" or "}" this is malformed, skip
                    if (tokens[i] == "{" || tokens[i] == "}") continue;
                    string value = tokens[i++];
                    properties[key] = value;
                }
            }

            if (i < tokens.Count && tokens[i] == "}") i++; // skip }

            properties.TryGetValue("classname", out string? classname);
            classname ??= "";

            if (classname.Equals("worldspawn", StringComparison.OrdinalIgnoreCase))
            {
                map.Brushes.AddRange(brushes);
                // worldspawn can have entity properties too, ignore for Phase 1
            }
            else
            {
                var entity = new MapEntity { ClassName = classname };
                foreach (var kv in properties)
                {
                    if (kv.Key.Equals("classname", StringComparison.OrdinalIgnoreCase)) continue;
                    entity.Properties[kv.Key] = kv.Value;
                }

                if (properties.TryGetValue("origin", out string? origin))
                    entity.Position = ParseVector3String(origin);

                // If no classname but has brushes -> treat as brush entity, add brushes to world
                if (string.IsNullOrWhiteSpace(entity.ClassName) && brushes.Count > 0)
                {
                    map.Brushes.AddRange(brushes);
                }
                else if (!string.IsNullOrWhiteSpace(entity.ClassName))
                {
                    map.Entities.Add(entity);
                    // entity brushes (Phase 1: merge into world brushes for collision)
                    if (brushes.Count > 0) map.Brushes.AddRange(brushes);
                }
            }
        }

        return map;
    }

    private static MapBrush? ParseBrush(List<string> tokens, ref int i)
    {
        // caller already at "{"
        if (tokens[i] != "{") return null;
        i++; // skip {
        var brush = new MapBrush();

        while (i < tokens.Count && tokens[i] != "}")
        {
            if (tokens[i] == "(")
            {
                var face = ParseFace(tokens, ref i);
                if (face != null) brush.Faces.Add(face);
            }
            else
            {
                i++; // skip unexpected token inside brush
            }
        }

        if (i < tokens.Count && tokens[i] == "}") i++; // skip }
        return brush.Faces.Count > 0 ? brush : null;
    }

    private static MapFace? ParseFace(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count || tokens[i] != "(") return null;

        var v1 = ParseVec3(tokens, ref i);
        if (v1 == null) return null;
        var v2 = ParseVec3(tokens, ref i);
        if (v2 == null) return null;
        var v3 = ParseVec3(tokens, ref i);
        if (v3 == null) return null;

        if (i >= tokens.Count) return null;
        string texture = tokens[i++];

        // TrenchBroom standard: texture + offsetX offsetY rotation scaleX scaleY
        float offsetX = 0, offsetY = 0, rotation = 0, scaleX = 1, scaleY = 1;

        if (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float ox)) { offsetX = ox; i++; }
        if (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float oy)) { offsetY = oy; i++; }
        if (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float rot)) { rotation = rot; i++; }
        if (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float sx)) { scaleX = sx; i++; }
        if (i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float sy)) { scaleY = sy; i++; }

        var face = new MapFace
        {
            Vertices = new[] { v1.Value, v2.Value, v3.Value },
            Texture = texture,
            TextureShiftX = (int)offsetX,
            TextureShiftY = (int)offsetY,
            TextureRotation = rotation,
            TextureScale = new Vector2(scaleX, scaleY)
        };

        var edge1 = v2.Value - v1.Value;
        var edge2 = v3.Value - v1.Value;
        face.Normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

        return face;
    }

    private static Vector3? ParseVec3(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count || tokens[i] != "(") return null;
        i++;
        if (i + 2 >= tokens.Count) return null;
        if (!float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return null;
        if (!float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return null;
        if (!float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return null;
        if (i >= tokens.Count || tokens[i] != ")") return null;
        i++;
        return new Vector3(x, y, z);
    }

    private static Vector3 ParseVector3String(string s)
    {
        s = s.Trim().Trim('(', ')');
        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            return new Vector3(x, y, z);
        return Vector3.Zero;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        int n = text.Length;
        int p = 0;

        while (p < n)
        {
            char c = text[p];

            // whitespace
            if (char.IsWhiteSpace(c)) { p++; continue; }

            // line comment //
            if (c == '/' && p + 1 < n && text[p + 1] == '/')
            {
                p += 2;
                while (p < n && text[p] != '\n') p++;
                continue;
            }

            // quoted string -> emit without quotes
            if (c == '"')
            {
                p++;
                var sb = new StringBuilder();
                while (p < n && text[p] != '"')
                {
                    // no escape handling needed for .map
                    sb.Append(text[p++]);
                }
                if (p < n && text[p] == '"') p++;
                tokens.Add(sb.ToString());
                continue;
            }

            if (c == '{' || c == '}' || c == '(' || c == ')')
            {
                tokens.Add(c.ToString());
                p++;
                continue;
            }

            // bare word (texture name, number)
            var word = new StringBuilder();
            while (p < n && !char.IsWhiteSpace(text[p]) && text[p] != '{' && text[p] != '}' && text[p] != '(' && text[p] != ')' && text[p] != '"')
            {
                // stop before comment start
                if (text[p] == '/' && p + 1 < n && text[p + 1] == '/') break;
                word.Append(text[p++]);
            }
            if (word.Length > 0) tokens.Add(word.ToString());
        }

        return tokens;
    }
}
