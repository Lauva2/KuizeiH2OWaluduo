using UnityEngine;
using UnityEngine.UI;

namespace RaftGame.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DayPhaseIcon : MaskableGraphic
    {
        public DayPhaseIcon() { useLegacyMeshGeneration = false; }
        private bool night;
        public void Present(bool isNight) { if (night == isNight) return; night = isNight; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = GetPixelAdjustedRect();
            Color tint = night ? new Color(.30f, .48f, .64f) : new Color(.86f, .55f, .21f);
            Vector2 center = rect.center; float radius = Mathf.Min(rect.width, rect.height) * .27f;
            if (night)
            {
                // Crescent strips share their endpoints; no painted cutout or background dependency.
                for (int segment = 0; segment < 24; segment++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * 1.5f, segment / 24f);
                    float b = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * 1.5f, (segment + 1) / 24f);
                    Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    Quad(mesh, center + p * radius, center + q * radius, center + new Vector2(q.x * .52f + .48f, q.y) * radius, center + new Vector2(p.x * .52f + .48f, p.y) * radius, tint);
                }
                Vector2 star = center + new Vector2(radius * 1.02f, radius * .9f);
                Quad(mesh, star + Vector2.up * 3, star + Vector2.right * 2, star + Vector2.down * 3, star + Vector2.left * 2, tint);
            }
            else
            {
                for (int segment = 0; segment < 24; segment++)
                {
                    float a = segment * Mathf.PI * 2 / 24, b = (segment + 1) * Mathf.PI * 2 / 24;
                    Quad(mesh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, center, tint);
                }
                for (int ray = 0; ray < 8; ray++)
                {
                    float a = ray * Mathf.PI * .25f; Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-direction.y, direction.x) * 1.2f;
                    Quad(mesh, center + direction * radius * 1.25f - side, center + direction * radius * 1.65f - side,
                        center + direction * radius * 1.65f + side, center + direction * radius * 1.25f + side, tint);
                }
            }
        }
        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            foreach (Vector2 point in new[] { a, b, c, d }) mesh.AddVert(point, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
