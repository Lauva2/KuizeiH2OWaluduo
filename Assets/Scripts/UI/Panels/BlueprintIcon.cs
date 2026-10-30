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
            else if (Tool == BuildTool.PlanterBlueprint)
            {
                Quad(mesh, bounds, .10f, .15f, .80f, .37f, wood); Quad(mesh, bounds, .14f, .50f, .72f, .11f, new Color(.37f, .27f, .15f));
                foreach (float x in new[] { .29f, .63f }) { Quad(mesh, bounds, x, .59f, .045f, .22f, dark); Quad(mesh, bounds, x - .13f, .72f, .15f, .10f, new Color(.33f, .60f, .34f)); Quad(mesh, bounds, x + .02f, .78f, .17f, .10f, new Color(.49f, .68f, .31f)); }
                Quad(mesh, bounds, .06f, .47f, .88f, .07f, cream);
            }
            else if (Tool == BuildTool.PlantBlueprint)
            {
                Quad(mesh, bounds, .32f, .12f, .38f, .34f, new Color(.74f, .39f, .24f)); Quad(mesh, bounds, .27f, .40f, .48f, .09f, wood);
                Quad(mesh, bounds, .48f, .46f, .05f, .36f, dark);
                Quad(mesh, bounds, .20f, .62f, .30f, .18f, new Color(.32f, .62f, .38f)); Quad(mesh, bounds, .53f, .72f, .29f, .16f, new Color(.46f, .68f, .34f));
                Quad(mesh, bounds, .53f, .52f, .25f, .14f, new Color(.32f, .62f, .38f));
            }
            else if (Tool == BuildTool.RugBlueprint)
            {
                Quad(mesh, bounds, .10f, .18f, .80f, .64f, new Color(.75f, .40f, .28f));
                Quad(mesh, bounds, .17f, .25f, .66f, .50f, cream); Quad(mesh, bounds, .22f, .31f, .56f, .38f, new Color(.34f, .55f, .52f));
                for (int i = 0; i < 5; i++) { Quad(mesh, bounds, .17f + i * .15f, .10f, .035f, .08f, cream); Quad(mesh, bounds, .17f + i * .15f, .82f, .035f, .08f, cream); }
            }
            else if (Tool == BuildTool.DecorLampBlueprint)
            {
                Quad(mesh, bounds, .27f, .11f, .46f, .08f, wood); Quad(mesh, bounds, .47f, .17f, .06f, .50f, wood);
                Quad(mesh, bounds, .20f, .53f, .60f, .30f, cream); Quad(mesh, bounds, .26f, .83f, .48f, .06f, wood);
                Quad(mesh, bounds, .42f, .54f, .16f, .22f, new Color(1, .76f, .31f));
            }
            else if (Tool == BuildTool.BedBlueprint)
            {
                Quad(mesh, bounds, .10f, .12f, .07f, .65f, wood); Quad(mesh, bounds, .83f, .12f, .07f, .65f, wood);
                Quad(mesh, bounds, .10f, .57f, .80f, .23f, wood); Quad(mesh, bounds, .16f, .35f, .68f, .29f, cream);
                Quad(mesh, bounds, .19f, .54f, .62f, .13f, new Color(.98f, .94f, .79f));
                Quad(mesh, bounds, .16f, .28f, .68f, .24f, new Color(.43f, .61f, .65f));
                Quad(mesh, bounds, .10f, .23f, .80f, .07f, wood);
            }
            else if (Tool == BuildTool.CollectionNetBlueprint)
            {
                Quad(mesh, bounds, .13f, .17f, .74f, .61f, new Color(.28f, .49f, .45f));
                for (int index = 0; index <= 5; index++)
                { Quad(mesh, bounds, .16f + index * .13f, .2f, .018f, .55f, cream); Quad(mesh, bounds, .16f, .2f + index * .105f, .66f, .018f, cream); }
                Quad(mesh, bounds, .08f, .75f, .84f, .09f, wood); Quad(mesh, bounds, .08f, .13f, .07f, .62f, wood); Quad(mesh, bounds, .85f, .13f, .07f, .62f, wood);
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
