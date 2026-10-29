using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RaftGame.UI;
using RaftGame.Resources;
using RaftGame.Building;

namespace RaftGame.Editor
{
    /// <summary>Authors the management HUD as a regular Addressable prefab.</summary>
    public static class ManagementHudBuilder
    {
        private static readonly Color Ink = new Color(0.16f, 0.28f, 0.25f);
        private static readonly Color Muted = new Color(0.30f, 0.41f, 0.36f);
        private static readonly Color Paper = new Color(0.98f, 0.96f, 0.89f, 0.98f);
        private static readonly Color Sea = new Color(0.18f, 0.49f, 0.40f);
        private static readonly Color White = new Color(1f, 0.98f, 0.92f);

        [MenuItem("Tools/Raft Game/Refresh Management HUD")]
        public static void Rebuild()
        {
            var root = new GameObject(UICommon_Build.Address, typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var view = root.AddComponent<UICommon_Build>();
            view.cache = true;
            var serialized = new SerializedObject(view);
            serialized.FindProperty("layer").enumValueIndex = (int)UILayer.Common;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                RectTransform header = Rect("Header", root.transform, new Vector2(24, -24), new Vector2(1552, 112));
                root.AddComponent<ManagementHudLayout>().Header = header;
                RectTransform title = Card("Camp Card", header, new Vector2(0, 1), Vector2.zero, new Vector2(256, 112));
                Label("木筏营地", title, new Vector2(60, -21), new Vector2(178, 35), 26, Ink, true);
                view.SaveButton = ActionButton("保存 F5", title, new Vector2(22, -72), new Vector2(100, 26));
                view.LoadButton = ActionButton("读档 F9", title, new Vector2(134, -72), new Vector2(100, 26));
                for (int plank = 0; plank < 3; plank++)
                {
                    Image wood = Block("Raft emblem", title, new Vector2(23 + plank * 9, -28), new Vector2(6, 23), new Color(0.69f, 0.48f, 0.27f));
                    wood.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
                }
                Block("Water emblem", title, new Vector2(20, -55), new Vector2(32, 2), Sea);

                RectTransform supplies = Card("Resource Card", header, new Vector2(0, 1), new Vector2(272, 0), new Vector2(800, 112));
                RectTransform tooltip = Card("Resource Tooltip", root.transform, new Vector2(0, 1), Vector2.zero, new Vector2(302, 104));
                Text tooltipText = Label("", tooltip, new Vector2(16, -10), new Vector2(272, 84), 14, Ink);
                view.ResourceTiles = new ResourceTile[5];
                Text[] counts = new Text[5];
                for (int index = 0; index < 5; index++)
                {
                    var tileRect = Rect(((ResourceType)index).ToString(), supplies, new Vector2(index * 160, 0), new Vector2(160, 112));
                    tileRect.gameObject.AddComponent<Image>().color = Color.clear;
                    var tile = tileRect.gameObject.AddComponent<ResourceTile>(); view.ResourceTiles[index] = tile;
                    tile.Resource = (ResourceType)index; tile.TooltipPanel = tooltip; tile.TooltipText = tooltipText;
                    if (index > 0) Block("Divider", tileRect, new Vector2(0, -18), new Vector2(1, 76), new Color(0.81f, 0.83f, 0.76f));
                    var icon = Rect("Icon", tileRect, new Vector2(16, -16), new Vector2(45, 43)).gameObject.AddComponent<ResourceIcon>();
                    icon.Resource = (ResourceType)index; icon.raycastTarget = false;
                    counts[index] = Label("0", tileRect, new Vector2(72, -17), new Vector2(77, 40), 26, Ink, true);
                    var track = Block("Stock Track", tileRect, new Vector2(19, -62), new Vector2(122, 5), new Color(0.84f, 0.86f, 0.78f));
                    tile.AvailableFill = Fill(track.transform, "Available", index == 4 ? new Color(0.22f, 0.56f, 0.67f) : Sea);
                    tile.ReservedFill = Fill(track.transform, "Reserved", new Color(0.79f, 0.55f, 0.26f));
                    if (index < 3) Label((index == 0 ? "木材" : index == 1 ? "废料" : "鲜鱼") + " · 可用占比", tileRect, new Vector2(19, -77), new Vector2(122, 20), 12, Muted);
                    else
                    {
                        Button minus = SmallButton("−", tileRect, new Vector2(19, -77));
                        Button plus = SmallButton("+", tileRect, new Vector2(109, -77));
                        Text target = Label("目标 4", tileRect, new Vector2(54, -77), new Vector2(52, 26), 12, Muted);
                        target.alignment = TextAnchor.MiddleCenter;
                        if (index == 3) { view.TargetMinusButton = minus; view.TargetPlusButton = plus; view.FoodTargetText = target; }
                        else { view.WaterTargetMinusButton = minus; view.WaterTargetPlusButton = plus; view.WaterTargetText = target; }
                    }
                }
                view.WoodText = counts[0]; view.ScrapText = counts[1]; view.FishText = counts[2]; view.CookedFishText = counts[3]; view.WaterText = counts[4];

                RectTransform life = Card("Deck Counts", header, new Vector2(0, 1), new Vector2(1088, 0), new Vector2(464, 112));
                view.CompletedText = Label("已建甲板  9", life, new Vector2(22, -16), new Vector2(128, 24), 16, Ink, true);
                view.BlueprintText = Label("待施工  0", life, new Vector2(156, -16), new Vector2(112, 24), 15, Muted);
                view.GrillText = Label("烤架 0 · 净水 0", life, new Vector2(282, -16), new Vector2(164, 24), 15, Muted);
                view.NeedsText = Label("饱腹 70%     水分 80%     效率 100%", life, new Vector2(22, -51), new Vector2(418, 22), 15, Ink);
                view.SatietyFill = Fill(Block("Satiety Track", life, new Vector2(22, -86), new Vector2(200, 6), new Color(0.84f, 0.86f, 0.78f)).transform, "Satiety Fill", Sea);
                view.HydrationFill = Fill(Block("Hydration Track", life, new Vector2(240, -86), new Vector2(200, 6), new Color(0.84f, 0.86f, 0.78f)).transform, "Hydration Fill", new Color(0.22f, 0.56f, 0.67f));

                RectTransform toolbar = Card("Construction Toolbar", root.transform, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(900, 136));
                view.ModeText = Label("自由规划", toolbar, new Vector2(22, -12), new Vector2(175, 26), 18, Ink, true);
                view.CostText = Label("甲板：木材 4 + 废料 1", toolbar, new Vector2(204, -15), new Vector2(280, 21), 14, Muted);
                view.CrewText = Label("船员：待命", toolbar, new Vector2(540, -15), new Vector2(336, 21), 13, Ink);
                view.CrewText.alignment = TextAnchor.MiddleRight;
                view.BuildButton = ToolButton("建造图册", "B", toolbar, 22, 276);
                view.CancelButton = ToolButton("撤销蓝图", "X", toolbar, 312, 276);
                view.FishingButton = ToolButton("空闲钓鱼 · 开", "F", toolbar, 602, 276);
                view.FishingButtonText = view.FishingButton.GetComponentInChildren<Text>();
                Block("Toolbar separator", toolbar, new Vector2(22, -105), new Vector2(856, 1), new Color(0.85f, 0.85f, 0.78f));
                view.StatusText = Label("点击漂浮物标记打捞 · 再次点击取消", toolbar, new Vector2(22, -109), new Vector2(856, 20), 14, Muted);

                RectTransform hints = Card("Camera Instructions", root.transform, Vector2.zero, new Vector2(24, 24), new Vector2(286, 58));
                root.GetComponent<ManagementHudLayout>().CameraHints = hints;
                hints.GetComponent<Image>().color = new Color(0.10f, 0.26f, 0.26f, 0.82f);
                Label("WASD 移动   ·   滚轮缩放\nQ / E 旋转   ·   Home 复位", hints, new Vector2(16, -9), new Vector2(258, 42), 13, White);
                RectTransform time = Card("Time Controls", root.transform, new Vector2(1, 0), new Vector2(-24, 24), new Vector2(286, 116));
                root.GetComponent<ManagementHudLayout>().TimeControls = time;
                view.TimeText = Label("漂流中 · 1×", time, new Vector2(14, -8), new Vector2(260, 22), 14, Ink);
                view.PauseButton = ActionButton("暂停", time, new Vector2(14, -40), new Vector2(76, 34));
                view.PauseButtonText = view.PauseButton.GetComponentInChildren<Text>();
                view.SpeedOneButton = ActionButton("1×", time, new Vector2(98, -40), new Vector2(50, 34));
                view.SpeedTwoButton = ActionButton("2×", time, new Vector2(158, -40), new Vector2(50, 34));
                view.SpeedFourButton = ActionButton("4×", time, new Vector2(218, -40), new Vector2(50, 34));
                view.SaveStatusText = Label("F5 保存 · F9 继续", time, new Vector2(14, -84), new Vector2(260, 22), 12, Muted);
                var weather = Card("Current and Wind", root.transform, new Vector2(0, 1), new Vector2(24, -154), new Vector2(236, 48));
                Label("海流", weather, new Vector2(14, -10), new Vector2(50, 28), 14, Ink);
                view.CurrentArrow = Arrow(weather, new Vector2(84, -24), Sea);
                Label("风向", weather, new Vector2(124, -10), new Vector2(50, 28), 14, Ink);
                view.WindArrow = Arrow(weather, new Vector2(194, -24), new Color(.65f, .40f, .21f));
                var dayClock = Card("Day and Night", root.transform, new Vector2(1, 1), new Vector2(-24, -154), new Vector2(284, 68));
                view.DayIcon = Rect("Sun and Moon", dayClock, new Vector2(10, -11), new Vector2(44, 44)).gameObject.AddComponent<DayPhaseIcon>(); view.DayIcon.raycastTarget = false;
                view.DayClockText = Label("第 1 天 · 08:00", dayClock, new Vector2(64, -8), new Vector2(208, 28), 19, Ink, true);
                view.DayPhaseText = Label("白昼", dayClock, new Vector2(64, -39), new Vector2(50, 20), 13, Muted);
                var dayTrack = Block("Day Progress", dayClock, new Vector2(116, -47), new Vector2(152, 5), new Color(.79f, .82f, .74f));
                view.DayProgressFill = Fill(dayTrack.transform, "Passed Time", new Color(.82f, .55f, .25f));
                var notice = Card("Latest Discovery", root.transform, new Vector2(.5f, 1), new Vector2(0, -154), new Vector2(600, 48));
                view.LootNotice = notice.gameObject; view.LootText = Label("", notice, new Vector2(20, -10), new Vector2(560, 28), 17, Ink, true); notice.gameObject.SetActive(false);
                BuildCatalog(root.transform, view);
                tooltip.SetAsLastSibling(); tooltip.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/UI/UICommon_Build.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("MANAGEMENT_HUD_OK");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Fill(Transform parent, string name, Color color)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }

        private static Button ToolButton(string title, string shortcut, Transform parent, float x, float width = 162)
        {
            RectTransform rect = Rect(title, parent, new Vector2(x, -48), new Vector2(width, 44));
            var image = rect.gameObject.AddComponent<RoundedPanel>(); image.color = Sea;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.pressedColor = new Color(0.78f, 0.84f, 0.8f); button.colors = colors;
            Label(title, rect, new Vector2(16, -1), new Vector2(width - 52, 42), 16, White);
            RectTransform key = Rect("Shortcut", rect, new Vector2(width - 33, -10), new Vector2(25, 24));
            var chip = key.gameObject.AddComponent<RoundedPanel>(); chip.color = new Color(1, 1, 1, 0.14f); chip.raycastTarget = false;
            Text keyText = Label(shortcut, key, Vector2.zero, new Vector2(25, 24), 12, White, true); keyText.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static RectTransform Arrow(Transform parent, Vector2 position, Color color)
        {
            var rect = Rect("Direction", parent, position, new Vector2(32, 32)); rect.pivot = new Vector2(.5f, .5f);
            var shaft = Block("Shaft", rect, new Vector2(2, -14), new Vector2(23, 4), color);
            var top = Block("Tip", rect, new Vector2(21, -7), new Vector2(13, 4), color); top.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var bottom = Block("Tip", rect, new Vector2(18, -21), new Vector2(13, 4), color); bottom.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            return rect;
        }
        private static void BuildCatalog(Transform root, UICommon_Build view)
        {
            var overlay = Rect("Build Catalog Overlay", root, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.gameObject.AddComponent<Image>().color = new Color(.03f, .14f, .15f, .48f);
            view.CatalogOverlay = overlay.gameObject;
            var window = Card("Build Catalog", overlay, new Vector2(.5f, .5f), Vector2.zero, new Vector2(920, 452));
            view.CatalogWindow = window; root.GetComponent<ManagementHudLayout>().CatalogWindow = window;
            Label("建造图册", window, new Vector2(24, -18), new Vector2(240, 42), 28, Ink, true);
            Label("在海上发现图纸，把木筏慢慢变成家", window, new Vector2(240, -30), new Vector2(570, 22), 14, Muted);
            view.CatalogCloseButton = ActionButton("×", window, new Vector2(856, -24), new Vector2(40, 36));
            view.BasicsCategoryButton = ActionButton("基础建造", window, new Vector2(24, -108), new Vector2(156, 48));
            view.ProductionCategoryButton = ActionButton("生产设施", window, new Vector2(24, -168), new Vector2(156, 48));
            view.LivingCategoryButton = ActionButton("生活设施", window, new Vector2(24, -228), new Vector2(156, 48));
            Label("B / Esc 关闭", window, new Vector2(24, -360), new Vector2(156, 24), 13, Muted);
            view.CatalogHeading = Label("基础建造 · 1 张图纸", window, new Vector2(212, -66), new Vector2(640, 25), 16, Ink, true);
            view.DeckRecipeButton = Recipe("木筏甲板", BuildTool.DeckBlueprint, "扩展营地\n连接更多空间", window, 212, out view.DeckRecipeCost, out view.DeckScrapCost);
            view.GrillButton = Recipe("简易烤架", BuildTool.GrillBlueprint, "鲜鱼与木材\n烤出温暖一餐", window, 212, out view.GrillRecipeCost, out view.GrillScrapCost);
            view.WaterButton = Recipe("海水净化器", BuildTool.PurifierBlueprint, "木材作燃料\n储备干净淡水", window, 516, out view.PurifierRecipeCost, out view.PurifierScrapCost);
            view.CatalogEmptyText = Label("尚无已解锁的生活设施图纸", window, new Vector2(212, -168), new Vector2(656, 70), 20, Muted);
            view.CatalogHint = Label("未知图纸藏在漂流补给箱中", window, new Vector2(24, -412), new Vector2(872, 24), 14, Muted);
            overlay.gameObject.SetActive(false);
        }
        private static Button Recipe(string name, BuildTool tool, string description, Transform parent, float x, out Text cost, out Text scrapCost)
        {
            var rect = Card(name, parent, new Vector2(0, 1), new Vector2(x, -108), new Vector2(284, 280));
            rect.GetComponent<Image>().color = new Color(.91f, .92f, .85f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            Label(name, rect, new Vector2(20, -14), new Vector2(244, 34), 22, Ink, true);
            var icon = Rect("Blueprint Illustration", rect, new Vector2(20, -66), new Vector2(110, 88)).gameObject.AddComponent<BlueprintIcon>(); icon.Tool = tool; icon.raycastTarget = false;
            Label(description, rect, new Vector2(146, -75), new Vector2(122, 66), 14, Muted);
            Label("建造用料", rect, new Vector2(20, -166), new Vector2(244, 20), 13, Muted);
            foreach (int index in new[] { 0, 1 })
            { var resource = Rect("Cost icon", rect, new Vector2(20 + index * 125, -194), new Vector2(24, 26)).gameObject.AddComponent<ResourceIcon>(); resource.Resource = index == 0 ? ResourceType.Wood : ResourceType.Scrap; resource.raycastTarget = false; }
            cost = Label("4", rect, new Vector2(53, -194), new Vector2(60, 26), 20, Ink, true);
            scrapCost = Label("1", rect, new Vector2(178, -194), new Vector2(60, 26), 20, Ink, true);
            var footer = Block("Choose", rect, new Vector2(20, -239), new Vector2(244, 29), Sea);
            var label = Label("选择建造  →", footer.transform, Vector2.zero, new Vector2(244, 29), 14, White); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Button SmallButton(string title, Transform parent, Vector2 position)
        {
            RectTransform rect = Rect(title, parent, position, new Vector2(32, 26));
            Image image = rect.gameObject.AddComponent<RoundedPanel>(); image.color = new Color(0.23f, 0.36f, 0.32f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Text text = Label(title, rect, Vector2.zero, new Vector2(32, 26), 18, White); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Button ActionButton(string title, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(title, parent, position, size);
            Image image = rect.gameObject.AddComponent<RoundedPanel>(); image.color = Sea;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Text text = Label(title, rect, Vector2.zero, size, 14, White); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static RectTransform Card(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, position, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.gameObject.AddComponent<RoundedPanel>().color = Paper;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0.04f, 0.18f, 0.16f, 0.18f); shadow.effectDistance = new Vector2(0, -3);
            return rect;
        }

        private static Image Block(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color, bool bold = false)
        {
            Text text = Rect(value, parent, position, size).gameObject.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = fontSize; text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
    }
}
