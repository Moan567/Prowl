// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Prowl.Vector;

namespace Prowl.Runtime.Relic.Map;

/// <summary>
/// Valve .map parser (TrenchBroom standard + Valve220 tolerant).
/// The .map file is the source of truth — see RELIC pipeline.
/// </summary>
public static class RelicMapParser
{
    public static RelicMapData Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Map file not found: {filePath}");
        var text = File.ReadAllText(filePath);
        var data = Parse(text);
        data.SourcePath = filePath;
        return data;
    }

    public static RelicMapData Parse(string mapText)
    {
        var map = new RelicMapData();
        var tokens = Tokenize(mapText);
        int i = 0;

        while (i < tokens.Count)
        {
            if (tokens[i] != "{") { i++; continue; }
            i++; // skip {

            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var brushes = new List<RelicMapBrush>();

            while (i < tokens.Count && tokens[i] != "}")
            {
                if (tokens[i] == "{")
                {
                    var brush = ParseBrush(tokens, ref i);
                    if (brush != null) brushes.Add(brush);
                }
                else
                {
                    string key = tokens[i++];
                    if (i >= tokens.Count) break;
                    if (tokens[i] == "{" || tokens[i] == "}") continue;
                    string value = tokens[i++];
                    properties[key] = value;
                }
            }

            if (i < tokens.Count && tokens[i] == "}") i++;

            properties.TryGetValue("classname", out string? classname);
            classname ??= string.Empty;

            if (classname.Equals("worldspawn", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var kv in properties)
                    if (!kv.Key.Equals("classname", StringComparison.OrdinalIgnoreCase))
                        map.WorldspawnProperties[kv.Key] = kv.Value;
                foreach (var b in brushes) { b.OwnerClassName = string.Empty; map.Brushes.Add(b); }
            }
            else if (string.IsNullOrWhiteSpace(classname) && brushes.Count > 0)
            {
                // Brushes with no classname belong to the world.
                foreach (var b in brushes) map.Brushes.Add(b);
            }
            else if (!string.IsNullOrWhiteSpace(classname))
            {
                var entity = new RelicMapEntity { ClassName = classname };
                foreach (var kv in properties)
                {
                    if (kv.Key.Equals("classname", StringComparison.OrdinalIgnoreCase)) continue;
                    entity.Properties[kv.Key] = kv.Value;
                }
                if (properties.TryGetValue("origin", out string? origin))
                {
                    entity.OriginQuake = ParseVector3(origin);
                    entity.HasOrigin = true;
                }
                foreach (var b in brushes)
                {
                    b.OwnerClassName = classname;
                    foreach (var kv in entity.Properties)
                        b.OwnerProperties[kv.Key] = kv.Value;
                    entity.Brushes.Add(b);
                }
                map.Entities.Add(entity);
                // Solid entities also contribute collision by default (triggers excluded at build time).
                foreach (var b in brushes) map.Brushes.Add(b);
            }
        }

        return map;
    }

    private static RelicMapBrush? ParseBrush(List<string> tokens, ref int i)
    {
        if (tokens[i] != "{") return null;
        i++;
        var brush = new RelicMapBrush();
        while (i < tokens.Count && tokens[i] != "}")
        {
            if (tokens[i] == "(")
            {
                var face = ParseFace(tokens, ref i);
                if (face != null) brush.Faces.Add(face);
            }
            else i++;
        }
        if (i < tokens.Count && tokens[i] == "}") i++;
        return brush.Faces.Count > 0 ? brush : null;
    }

    private static RelicMapFace? ParseFace(List<string> tokens, ref int i)
    {
        var v1 = ParseVec3(tokens, ref i);
        if (v1 == null) return null;
        var v2 = ParseVec3(tokens, ref i);
        if (v2 == null) return null;
        var v3 = ParseVec3(tokens, ref i);
        if (v3 == null) return null;
        if (i >= tokens.Count) return null;

        string texture = tokens[i++];

        // Detect Valve220: texture [ ux uy uz off ] [ vx vy vz off ] rot sx sy
        float offsetU = 0, offsetV = 0, rotation = 0, scaleU = 1, scaleV = 1;
        bool hasValveAxes = false;
        var uAxis = Float3.UnitX;
        var vAxis = Float3.UnitY;
        if (i < tokens.Count && tokens[i] == "[")
        {
            hasValveAxes = true;
            i++; // [
            float ux = 0, uy = 0, uz = 0;
            if (TryFloat(tokens, ref i, out var tx)) ux = tx;
            if (TryFloat(tokens, ref i, out var ty)) uy = ty;
            if (TryFloat(tokens, ref i, out var tz)) uz = tz;
            uAxis = new Float3(ux, uy, uz);
            if (TryFloat(tokens, ref i, out var ou)) offsetU = ou;
            if (i < tokens.Count && tokens[i] == "]") i++;
            if (i < tokens.Count && tokens[i] == "[") i++;
            float vx = 0, vy = 0, vz = 0;
            if (TryFloat(tokens, ref i, out var qx)) vx = qx;
            if (TryFloat(tokens, ref i, out var qy)) vy = qy;
            if (TryFloat(tokens, ref i, out var qz)) vz = qz;
            vAxis = new Float3(vx, vy, vz);
            if (TryFloat(tokens, ref i, out var ov)) offsetV = ov;
            if (i < tokens.Count && tokens[i] == "]") i++;
            if (TryFloat(tokens, ref i, out var rot)) rotation = rot;
            if (TryFloat(tokens, ref i, out var su)) scaleU = su <= 0 ? 1 : su;
            if (TryFloat(tokens, ref i, out var sv)) scaleV = sv <= 0 ? 1 : sv;
        }
        else
        {
            if (TryFloat(tokens, ref i, out var ox)) offsetU = ox;
            if (TryFloat(tokens, ref i, out var oy)) offsetV = oy;
            if (TryFloat(tokens, ref i, out var rot)) rotation = rot;
            if (TryFloat(tokens, ref i, out var sx)) scaleU = sx <= 0 ? 1 : sx;
            if (TryFloat(tokens, ref i, out var sy)) scaleV = sy <= 0 ? 1 : sy;
        }

        var face = new RelicMapFace
        {
            P1 = v1.Value, P2 = v2.Value, P3 = v3.Value,
            Texture = texture,
            OffsetU = offsetU, OffsetV = offsetV,
            Rotation = rotation, ScaleU = scaleU, ScaleV = scaleV,
            HasValveAxes = hasValveAxes, ValveUAxis = uAxis, ValveVAxis = vAxis
        };
        var e1 = v2.Value - v1.Value;
        var e2 = v3.Value - v1.Value;
        var n = Float3.Cross(e1, e2);
        float len = Float3.Length(n);
        face.Normal = len > 1e-8f ? n / len : Float3.UnitY;
        face.Dist = Float3.Dot(face.Normal, v1.Value);
        return face;
    }

    private static Float3? ParseVec3(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count || tokens[i] != "(") return null;
        i++;
        if (i + 2 >= tokens.Count) return null;
        if (!float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return null;
        if (!float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return null;
        if (!float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return null;
        if (i >= tokens.Count || tokens[i] != ")") return null;
        i++;
        return new Float3(x, y, z);
    }

    private static bool TryFloat(List<string> tokens, ref int i, out float value)
    {
        value = 0;
        if (i >= tokens.Count) return false;
        string t = tokens[i];
        if (t == "{" || t == "}" || t == "(" || t == ")" || t == "[") return false;
        if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { i++; return true; }
        return false;
    }

    private static void SkipFloats(List<string> tokens, ref int i, int count)
    {
        for (int k = 0; k < count; k++)
        {
            float dummy = 0;
            TryFloat(tokens, ref i, out dummy);
        }
    }

    public static Float3 ParseVector3(string s)
    {
        s = s.Trim().Trim('(', ')');
        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            return new Float3(x, y, z);
        return Float3.Zero;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        int n = text.Length, p = 0;
        while (p < n)
        {
            char c = text[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < n && text[p + 1] == '/')
            {
                p += 2;
                while (p < n && text[p] != '\n') p++;
                continue;
            }
            if (c == '"')
            {
                p++;
                var sb = new StringBuilder();
                while (p < n && text[p] != '"') sb.Append(text[p++]);
                if (p < n && text[p] == '"') p++;
                tokens.Add(sb.ToString());
                continue;
            }
            if (c == '{' || c == '}' || c == '(' || c == ')' || c == '[' || c == ']')
            {
                tokens.Add(c.ToString());
                p++;
                continue;
            }
            var word = new StringBuilder();
            while (p < n && !char.IsWhiteSpace(text[p]) && text[p] != '{' && text[p] != '}' &&
                   text[p] != '(' && text[p] != ')' && text[p] != '"' && text[p] != '[' && text[p] != ']')
            {
                if (text[p] == '/' && p + 1 < n && text[p + 1] == '/') break;
                word.Append(text[p++]);
            }
            if (word.Length > 0) tokens.Add(word.ToString());
        }
        return tokens;
    }
}
