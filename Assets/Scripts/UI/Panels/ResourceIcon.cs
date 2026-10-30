using UnityEngine;
using UnityEngine.UI;
using RaftGame.Resources;
namespace RaftGame.UI
{
    /// <summary>Small, texture-free illustrations, readable at HUD scale.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResourceIcon : MaskableGraphic
    {
        public ResourceIcon() { useLegacyMeshGeneration = false; }
        public ResourceType Resource;
        private VertexHelper mesh;
        private Rect bounds;
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); mesh = target; bounds = GetPixelAdjustedRect();
            if (Resource == ResourceType.Wood)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = 0.16f + i * 0.23f;
                    Polygon(new Color(0.72f + i * 0.05f, 0.45f + i * 0.04f, 0.22f), new Vector2(x, .12f), new Vector2(x + .17f, .17f), new Vector2(x + .26f, .84f), new Vector2(x + .09f, .89f));
                    Circle(new Vector2(x + .13f, .55f), new Vector2(.025f, .07f), new Color(.48f, .30f, .16f));
                }
                Polygon(new Color(.9f, .77f, .51f), new Vector2(.15f, .35f), new Vector2(.83f, .42f), new Vector2(.84f, .49f), new Vector2(.16f, .42f));
            }
            else if (Resource == ResourceType.Scrap)
            {
                Polygon(new Color(.38f, .56f, .55f), new Vector2(.12f, .2f), new Vector2(.78f, .12f), new Vector2(.9f, .73f), new Vector2(.7f, .89f), new Vector2(.19f, .78f));
                Polygon(new Color(.67f, .78f, .7f), new Vector2(.27f, .30f), new Vector2(.67f, .27f), new Vector2(.77f, .64f), new Vector2(.33f, .7f));
                Circle(new Vector2(.28f, .67f), Vector2.one * .04f, new Color(.22f, .36f, .34f)); Circle(new Vector2(.72f, .34f), Vector2.one * .04f, new Color(.22f, .36f, .34f));
            }
            else if (Resource == ResourceType.Water)
            {
                Polygon(new Color(.18f, .58f, .77f), new Vector2(.5f, .96f), new Vector2(.16f, .46f), new Vector2(.15f, .27f), new Vector2(.28f, .1f), new Vector2(.5f, .05f), new Vector2(.72f, .1f), new Vector2(.85f, .27f), new Vector2(.84f, .46f));
                Circle(new Vector2(.35f, .35f), new Vector2(.07f, .14f), new Color(.68f, .9f, .94f));
            }
            else if (Resource == ResourceType.Potato || Resource == ResourceType.BakedPotato)
            {
                bool baked = Resource == ResourceType.BakedPotato;
                Circle(new Vector2(.5f, .45f), new Vector2(.40f, .30f), baked ? new Color(.79f, .46f, .23f) : new Color(.72f, .55f, .31f));
                if (baked) Circle(new Vector2(.51f, .48f), new Vector2(.29f, .18f), new Color(.95f, .82f, .51f));
                foreach (var point in new[] { new Vector2(.30f, .46f), new Vector2(.60f, .34f), new Vector2(.69f, .56f) }) Circle(point, Vector2.one * .035f, new Color(.42f, .29f, .15f));
                if (baked) foreach (float x in new[] { .38f, .63f }) Polygon(new Color(.70f, .77f, .68f), new Vector2(x, .79f), new Vector2(x + .035f, .79f), new Vector2(x + .08f, .96f), new Vector2(x + .045f, .96f));
            }
            else
            {
                Color body = Resource == ResourceType.CookedFish ? new Color(.85f, .49f, .20f) : new Color(.35f, .67f, .65f);
                Polygon(body, new Vector2(.12f, .48f), new Vector2(.02f, .75f), new Vector2(.03f, .23f));
                Circle(new Vector2(.52f, .5f), new Vector2(.38f, .24f), body);
                Polygon(body, new Vector2(.42f, .68f), new Vector2(.55f, .89f), new Vector2(.68f, .69f));
                Circle(new Vector2(.76f, .57f), Vector2.one * .035f, new Color(.13f, .24f, .25f));
                if (Resource == ResourceType.CookedFish) for (int i = 0; i < 3; i++)
                { float x = .36f + i * .12f; Polygon(new Color(.46f, .29f, .15f), new Vector2(x, .34f), new Vector2(x + .03f, .34f), new Vector2(x + .1f, .65f), new Vector2(x + .07f, .65f)); }
            }
        }
        private Vector2 Position(Vector2 point) => bounds.center + (point - Vector2.one * .5f) * Mathf.Min(bounds.width, bounds.height);
        private void Polygon(Color tint, params Vector2[] points)
        {
            int start = mesh.currentVertCount; foreach (var point in points) mesh.AddVert(Position(point), tint, Vector2.zero);
            for (int index = 1; index < points.Length - 1; index++) mesh.AddTriangle(start, start + index, start + index + 1);
        }
        private void Circle(Vector2 center, Vector2 radius, Color tint)
        {
            var points = new Vector2[20]; for (int i = 0; i < points.Length; i++) { float a = i * Mathf.PI * 2 / points.Length; points[i] = center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radius); } Polygon(tint, points);
        }
    }
}
