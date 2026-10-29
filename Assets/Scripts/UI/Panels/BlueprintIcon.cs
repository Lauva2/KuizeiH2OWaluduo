using UnityEngine;
using UnityEngine.UI;
using RaftGame.Building;
namespace RaftGame.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlueprintIcon : MaskableGraphic
    {
        public BuildTool Tool;
        public BlueprintIcon() { useLegacyMeshGeneration = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect bounds = GetPixelAdjustedRect();
            Color wood = new Color(.70f, .45f, .24f), dark = new Color(.22f, .36f, .33f), steel = new Color(.48f, .64f, .60f), cream = new Color(.91f, .81f, .56f);
            if (Tool == BuildTool.DeckBlueprint)
            {
                for (int row = 0; row < 4; row++) Quad(mesh, bounds, .08f, .14f + row * .18f, .84f, .15f, row % 2 == 0 ? wood : new Color(.80f, .55f, .29f));
                foreach (float x in new[] { .2f, .77f }) for (int row = 0; row < 4; row++) Quad(mesh, bounds, x, .2f + row * .18f, .025f, .025f, dark);
            }
            else if (Tool == BuildTool.GrillBlueprint)
            {
                Quad(mesh, bounds, .2f, .1f, .07f, .47f, dark); Quad(mesh, bounds, .75f, .1f, .07f, .47f, dark);
                Quad(mesh, bounds, .11f, .47f, .80f, .28f, dark); Quad(mesh, bounds, .15f, .55f, .72f, .15f, new Color(.63f, .36f, .18f));
                for (int i = 0; i < 7; i++) Quad(mesh, bounds, .17f + i * .10f, .53f, .025f, .24f, steel);
                Quad(mesh, bounds, .01f, .54f, .12f, .07f, wood);
            }
            else
            {
                Quad(mesh, bounds, .08f, .1f, .84f, .11f, wood); Quad(mesh, bounds, .15f, .2f, .045f, .67f, wood); Quad(mesh, bounds, .83f, .2f, .045f, .67f, wood);
                Quad(mesh, bounds, .22f, .23f, .32f, .36f, dark); Quad(mesh, bounds, .20f, .58f, .36f, .07f, steel);
                Quad(mesh, bounds, .63f, .24f, .20f, .16f, cream); Quad(mesh, bounds, .65f, .36f, .16f, .035f, new Color(.2f, .6f, .75f));
                Quad(mesh, bounds, .09f, .78f, .83f, .12f, cream); Quad(mesh, bounds, .2f, .87f, .61f, .04f, cream);
            }
        }
        private static void Quad(VertexHelper mesh, Rect bounds, float x, float y, float w, float h, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(bounds.xMin + x * bounds.width, bounds.yMin + y * bounds.height), color, Vector2.zero);
            mesh.AddVert(new Vector2(bounds.xMin + x * bounds.width, bounds.yMin + (y + h) * bounds.height), color, Vector2.zero);
            mesh.AddVert(new Vector2(bounds.xMin + (x + w) * bounds.width, bounds.yMin + (y + h) * bounds.height), color, Vector2.zero);
            mesh.AddVert(new Vector2(bounds.xMin + (x + w) * bounds.width, bounds.yMin + y * bounds.height), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
