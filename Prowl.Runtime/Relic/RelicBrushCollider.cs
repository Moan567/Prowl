// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Vector;

namespace Prowl.Runtime.Relic;

/// <summary>
/// Mesh collider for imported brush geometry. Identical collision to
/// <see cref="MeshCollider"/> but without the per-triangle wireframe gizmo:
/// a full map's collider wireframes would otherwise bury the scene view in
/// green lines. When selected, only a subtle bounds box is drawn.
/// </summary>
[AddComponentMenu("Relic/Brush/Brush Collider")]
public sealed class RelicBrushCollider : MeshCollider
{
    public override void DrawGizmos()
    {
        // Intentionally quiet: brush triangle wireframes cover the whole map.
    }

    public override void DrawGizmosSelected()
    {
        var m = Mesh.Res;
        if (m == null)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr.IsValid()) m = mr.Mesh.Res;
        }
        if (m == null) return;

        Debug.PushMatrix(GizmoMatrix);
        Debug.DrawWireCube(Center + m.bounds.Center, m.bounds.Size * 0.5f, Color.Yellow);
        Debug.PopMatrix();
    }
}
