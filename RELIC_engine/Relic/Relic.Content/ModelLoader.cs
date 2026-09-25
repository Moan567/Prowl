namespace Relic.Content;

using Relic.Framework;

public static class ModelLoader
{
    public static ModelData Load(string fullPath)
    {
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"Model not found: {fullPath}");
        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        if (ext == ".obj") return LoadObj(fullPath);
        if (ext == ".mdl")
        {
            var mdl = MDLLoader.Load(fullPath);
            var mesh = MDLLoader.ToMeshData(mdl, 0);
            var modelData = new ModelData();
            foreach (var v in mesh.Vertices)
            {
                modelData.Positions.Add(v.Position);
                modelData.Normals.Add(v.Normal);
                modelData.TexCoords.Add(v.TexCoord);
            }
            foreach (var idx in mesh.Indices) modelData.Indices.Add(idx);
            return modelData;
        }
        throw new NotSupportedException($"Unsupported model format: {ext}");
    }

    public static MDLModel LoadMDL(string fullPath) => MDLLoader.Load(fullPath);

    private static ModelData LoadObj(string path)
    {
        var model = new ModelData();
        var positions = new List<Vector3>();
        var texcoords = new List<Vector2>();
        var normals = new List<Vector3>();
        var vertices = new Dictionary<(int,int,int), uint>();
        uint indexCounter = 0;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("#") || line.Length==0) continue;
            var parts = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            if (parts[0]=="v" && parts.Length>=4)
            {
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                    positions.Add(new Vector3(x,y,z));
            }
            else if (parts[0]=="vt" && parts.Length>=3)
            {
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    texcoords.Add(new Vector2(u,v));
            }
            else if (parts[0]=="vn" && parts.Length>=4)
            {
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                    normals.Add(new Vector3(x,y,z));
            }
            else if (parts[0]=="f")
            {
                // triangulate fan
                var faceVerts = new List<uint>();
                for(int i=1;i<parts.Length;i++)
                {
                    var seg = parts[i].Split('/');
                    int pi = seg.Length>0 && int.TryParse(seg[0], out var v) ? v-1 : -1;
                    int ti = seg.Length>1 && int.TryParse(seg[1], out var vt) ? vt-1 : -1;
                    int ni = seg.Length>2 && int.TryParse(seg[2], out var vn) ? vn-1 : -1;
                    var key = (pi,ti,ni);
                    if (!vertices.TryGetValue(key, out var idx))
                    {
                        var p = pi>=0 && pi<positions.Count ? positions[pi] : Vector3.Zero;
                        var uv = ti>=0 && ti<texcoords.Count ? texcoords[ti] : Vector2.Zero;
                        var n = ni>=0 && ni<normals.Count ? normals[ni] : new Vector3(0,0,1);
                        model.Positions.Add(p);
                        model.TexCoords.Add(uv);
                        model.Normals.Add(n);
                        idx = indexCounter++;
                        vertices[key]=idx;
                    }
                    faceVerts.Add(idx);
                }
                for(int i=1;i<faceVerts.Count-1;i++)
                {
                    model.Indices.Add(faceVerts[0]);
                    model.Indices.Add(faceVerts[i+1]);
                    model.Indices.Add(faceVerts[i]);
                }
            }
        }
        // If no normals, generate flat normals per triangle
        if (normals.Count==0 && model.Indices.Count>=3)
        {
            var newNormals = new List<Vector3>(new Vector3[model.Positions.Count]);
            for(int i=0;i<model.Indices.Count;i+=3)
            {
                uint i0=model.Indices[i], i1=model.Indices[i+1], i2=model.Indices[i+2];
                var p0=model.Positions[(int)i0]; var p1=model.Positions[(int)i1]; var p2=model.Positions[(int)i2];
                var n = Vector3.Normalize(Vector3.Cross(p1-p0, p2-p0));
                newNormals[(int)i0]=n; newNormals[(int)i1]=n; newNormals[(int)i2]=n;
            }
            model.Normals.Clear();
            model.Normals.AddRange(newNormals);
        }
        return model;
    }
}
