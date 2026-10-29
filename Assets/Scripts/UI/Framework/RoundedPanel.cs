using UnityEngine;
using UnityEngine.UI;

namespace RaftGame.UI
{
    /// <summary>Small rounded UI panels without a texture or an additional material.</summary>
    public sealed class RoundedPanel : Image
    {
        [SerializeField, Min(0f)] private float radius = 8f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            mesh.AddVert(rect.center, color, Vector2.zero);
            const int segments = 6;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                    corner < 2 ? rect.yMax - r : rect.yMin + r);
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, color, Vector2.zero);
                }
            }
            int count = 4 * (segments + 1);
            for (int vertex = 1; vertex <= count; vertex++) mesh.AddTriangle(0, vertex, vertex % count + 1);
        }
    }
}
