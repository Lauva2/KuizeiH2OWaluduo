using UnityEngine;
using UnityEngine.UI;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;
using RaftGame.Core;

namespace RaftGame.UI
{
    public sealed class BuildPanelController : UIController
    {
        private readonly BuildManager manager;
        private UICommon_Build view;
        private BuildPanelModel model;
        private readonly CrewManager crew;
        private readonly ResourceManager resources;
        private readonly SalvageManager salvage;
        private readonly FishingManager fishing;
        private readonly ProductionManager production;
        private readonly NeedsManager needs;
        private readonly GameManager game;
        private int category;
        private float noticeUntil;
        private RaftGame.Ocean.OceanManager ocean;
        public BuildPanelController(BuildManager manager, CrewManager crew = null, ResourceManager resources = null, SalvageManager salvage = null,
            FishingManager fishing = null, ProductionManager production = null, NeedsManager needs = null, GameManager game = null)
        { this.manager = manager; this.crew = crew; this.resources = resources; this.salvage = salvage; this.fishing = fishing; this.production = production; this.needs = needs; this.game = game; }

        protected override void OnInitialize()
        {
            view = (UICommon_Build)View;
            model = (BuildPanelModel)Model;
            ocean = UnityEngine.Object.FindObjectOfType<RaftGame.Ocean.OceanManager>();
            // WASD/arrows and Space belong to camera/time controls, not automatic UI navigation.
            foreach (Button button in view.GetComponentsInChildren<Button>(true))
                button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        protected override void OnOpen()
        {
            view.BuildButton.onClick.AddListener(ToggleBuild);
            view.CancelButton.onClick.AddListener(ToggleCancel);
            if (view.FishingButton != null) view.FishingButton.onClick.AddListener(ToggleFishing);
            if (view.GrillButton != null) view.GrillButton.onClick.AddListener(ToggleGrill);
            if (view.TargetMinusButton != null) view.TargetMinusButton.onClick.AddListener(LowerTarget);
            if (view.TargetPlusButton != null) view.TargetPlusButton.onClick.AddListener(RaiseTarget);
            manager.Changed += Refresh;
            if (crew != null) crew.Changed += Refresh;
            if (resources != null) resources.Changed += Refresh;
            if (salvage != null) salvage.Changed += Refresh;
            if (fishing != null) fishing.Changed += Refresh;
            if (production != null) production.Changed += Refresh;
            if (needs != null) needs.Changed += Refresh;
            model.Changed += Render;
            if (view.PauseButton != null) view.PauseButton.onClick.AddListener(TogglePause);
            if (view.SpeedOneButton != null) view.SpeedOneButton.onClick.AddListener(SpeedOne);
            if (view.SpeedTwoButton != null) view.SpeedTwoButton.onClick.AddListener(SpeedTwo);
            if (view.SpeedFourButton != null) view.SpeedFourButton.onClick.AddListener(SpeedFour);
            if (game != null && game.TimeControl != null) game.TimeControl.Changed += Refresh;
            BindAdditional(true);
            Refresh();
        }

        protected override void OnRefresh() => Refresh();
        private void Refresh() { if (view != null) model.Refresh(manager, crew, resources, salvage, fishing, production, needs, game != null ? game.Water : null); }
        private void ToggleWater() => SelectBlueprint(BuildTool.PurifierBlueprint);
        private void SelectNet() => SelectBlueprint(BuildTool.CollectionNetBlueprint);
        private void SelectBed() => SelectBlueprint(BuildTool.BedBlueprint);
        private void SelectPlanter() => SelectBlueprint(BuildTool.PlanterBlueprint);
        private void SelectPlant() => SelectBlueprint(BuildTool.PlantBlueprint);
        private void SelectRug() => SelectBlueprint(BuildTool.RugBlueprint);
        private void SelectDecorLamp() => SelectBlueprint(BuildTool.DecorLampBlueprint);
        private void OpenComfort() { if (game != null) game.OpenComfortPanel(); }
        private void SelectDeck() => SelectBlueprint(BuildTool.DeckBlueprint);
        private void SelectBlueprint(BuildTool tool) { manager.CatalogOpen = false; manager.SetTool(tool); Refresh(); }
        private void CloseCatalog() { manager.CatalogOpen = false; Refresh(); }
        private void Basics() { category = 0; Refresh(); }
        private void Production() { category = 1; Refresh(); }
        private void Living() { category = 2; Refresh(); }
        private void OnLootChanged() { noticeUntil = Time.unscaledTime + 7; Refresh(); }
        public void ProcessInput()
        {
            if (!IsOpen) return;
            RenderClock();
            if (game != null && game.WorldPanelOpen) return;
            if (Input.GetKeyDown(KeyCode.B)) ToggleBuild();
            if (manager.CatalogOpen && Input.GetKeyDown(KeyCode.Escape)) CloseCatalog();
            if (view.LootNotice != null && Time.unscaledTime > noticeUntil) view.LootNotice.SetActive(false);
            if (game != null && view.CurrentArrow != null)
            {
                if (ocean != null)
                {
                    view.CurrentArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(ocean.CurrentDirection.y, ocean.CurrentDirection.x) * Mathf.Rad2Deg);
                    view.WindArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(ocean.WindDirection.y, ocean.WindDirection.x) * Mathf.Rad2Deg);
                }
            }
        }
        private void LowerWater() { if (game != null && game.Water != null) game.Water.SetTarget(game.Water.Target - 1); }
        private void RaiseWater() { if (game != null && game.Water != null) game.Water.SetTarget(game.Water.Target + 1); }
        private void Save() { if (game != null) game.Saves.Save(); }
        private void Load() { if (game != null) game.Saves.Load(); }
        private void BindAdditional(bool bind)
        {
            Bind(view.WaterButton, ToggleWater, bind); Bind(view.WaterTargetMinusButton, LowerWater, bind); Bind(view.WaterTargetPlusButton, RaiseWater, bind);
            Bind(view.NetButton, SelectNet, bind);
            Bind(view.BedButton, SelectBed, bind);
            Bind(view.PlanterButton, SelectPlanter, bind);
            Bind(view.PlantButton, SelectPlant, bind); Bind(view.RugButton, SelectRug, bind); Bind(view.DecorLampButton, SelectDecorLamp, bind); Bind(view.ComfortButton, OpenComfort, bind);
            Bind(view.CrewButton, OpenCrew, bind);
            if (crew != null && crew.Recruitment != null) { if (bind) crew.Recruitment.Changed += Refresh; else crew.Recruitment.Changed -= Refresh; }
            Bind(view.SaveButton, Save, bind); Bind(view.LoadButton, Load, bind);
            Bind(view.DeckRecipeButton, SelectDeck, bind); Bind(view.CatalogCloseButton, CloseCatalog, bind);
            Bind(view.BasicsCategoryButton, Basics, bind); Bind(view.ProductionCategoryButton, Production, bind); Bind(view.LivingCategoryButton, Living, bind);
            if (game == null) return;
            if (game.Comfort != null) { if (bind) game.Comfort.Changed += Refresh; else game.Comfort.Changed -= Refresh; }
            if (game.Farming != null) { if (bind) game.Farming.Changed += Refresh; else game.Farming.Changed -= Refresh; }
            if (bind) { if (game.Water != null) game.Water.Changed += Refresh; if (game.Saves != null) game.Saves.Changed += Refresh; if (game.Loot != null) game.Loot.Changed += OnLootChanged; }
            else { if (game.Water != null) game.Water.Changed -= Refresh; if (game.Saves != null) game.Saves.Changed -= Refresh; if (game.Loot != null) game.Loot.Changed -= OnLootChanged; }
        }
        private static void Bind(Button button, UnityEngine.Events.UnityAction callback, bool bind)
        { if (button == null) return; if (bind) button.onClick.AddListener(callback); else button.onClick.RemoveListener(callback); }
        private void OpenCrew() { if (game != null) game.OpenCrewPanel(); }
        private void ToggleGrill() => SelectBlueprint(BuildTool.GrillBlueprint);
        private void LowerTarget() { if (production != null) production.SetTarget(production.Target - 1); }
        private void RaiseTarget() { if (production != null) production.SetTarget(production.Target + 1); }
        private void ToggleBuild()
        {
            bool open = !manager.CatalogOpen;
            manager.SetTool(BuildTool.None); manager.CatalogOpen = open;
            if (view.ResourceTiles != null) foreach (var tile in view.ResourceTiles) tile.OnPointerExit(null);
            Refresh();
        }
        private void ToggleCancel() => manager.SetTool(manager.Tool == BuildTool.CancelBlueprint ? BuildTool.None : BuildTool.CancelBlueprint);
        private void ToggleFishing() { if (fishing != null) fishing.SetAutomaticFishing(!fishing.AutomaticFishing); }
        private void TogglePause() { if (game != null) game.TimeControl.TogglePause(); }
        private void SpeedOne() { if (game != null) game.TimeControl.SetSpeed(1); }
        private void SpeedTwo() { if (game != null) game.TimeControl.SetSpeed(2); }
        private void SpeedFour() { if (game != null) game.TimeControl.SetSpeed(4); }

        private void Render()
        {
            if (view == null) return;
            if (view.ComfortButton != null)
            {
                view.ComfortButton.gameObject.SetActive(game != null && game.Comfort != null);
                if (game != null && game.Comfort != null)
                { view.ComfortText.text = $"营地舒适 {game.Comfort.Score:0.#}"; view.ComfortDetailText.text = $"休息恢复 +{(game.Comfort.RestMultiplier - 1) * 100:0.#}% · V 查看装饰"; }
            }
            RenderClock();
            RenderCatalog();
            if (view.CrewButtonText != null) view.CrewButtonText.text = $"船员 {(crew != null ? crew.Count : 0)}/2";
            if (game != null && game.TimeControl != null)
            {
                var clock = game.TimeControl;
                if (view.TimeText != null) view.TimeText.text = clock.IsPaused ? "已暂停 · 仍可规划" : $"漂流中 · {clock.Speed}×";
                if (view.PauseButtonText != null) view.PauseButtonText.text = clock.IsPaused ? "继续" : "暂停";
                Button[] speeds = { view.SpeedOneButton, view.SpeedTwoButton, view.SpeedFourButton };
                int[] values = { 1, 2, 4 };
                for (int index = 0; index < speeds.Length; index++) if (speeds[index] != null)
                    speeds[index].image.color = !clock.IsPaused && clock.Speed == values[index] ? new Color(0.12f, 0.46f, 0.39f) : new Color(0.23f, 0.36f, 0.32f);
                if (view.PauseButton != null) view.PauseButton.image.color = clock.IsPaused ? new Color(0.66f, 0.37f, 0.22f) : new Color(0.23f, 0.36f, 0.32f);
            }
            view.CompletedText.text = $"已建甲板  {model.Completed}";
            view.BlueprintText.text = $"待施工  {model.Blueprints}";
            view.StatusText.text = model.Message;
            if (view.CrewText != null) view.CrewText.text = model.CrewStatus;
            if (view.WoodText != null) view.WoodText.text = model.WoodStatus;
            if (view.ScrapText != null) view.ScrapText.text = model.ScrapStatus;
            if (view.CostText != null) view.CostText.text = model.CostStatus;
            if (view.FishText != null) view.FishText.text = model.FishStatus;
            if (view.CookedFishText != null) view.CookedFishText.text = model.CookedFishStatus;
            if (resources != null && view.PotatoText != null) view.PotatoText.text = resources.GetTotal(ResourceType.Potato).ToString();
            if (resources != null && view.BakedPotatoText != null) view.BakedPotatoText.text = resources.GetTotal(ResourceType.BakedPotato).ToString();
            if (view.CookedFoodText != null) view.CookedFoodText.text = production != null ? $"熟食合计 {production.FoodCount} / 目标 {production.Target}" : "熟食储备 —";
            if (view.WaterText != null) view.WaterText.text = model.WaterStatus;
            if (view.FoodTargetText != null) view.FoodTargetText.text = "目标 " + model.FoodTarget;
            if (view.WaterTargetText != null) view.WaterTargetText.text = "目标 " + model.WaterTarget;
            if (view.ResourceTiles != null && resources != null) foreach (var tile in view.ResourceTiles)
                tile.Present(resources, tile.Resource == ResourceType.CookedFish || tile.Resource == ResourceType.BakedPotato ? model.FoodTarget : tile.Resource == ResourceType.Water ? model.WaterTarget : -1);
            if (view.HydrationFill != null)
            {
                view.HydrationFill.anchorMax = new Vector2(Mathf.Clamp01(model.Hydration / 100f), 1);
                view.HydrationFill.GetComponent<Image>().color = model.Hydration < 25 ? new Color(0.78f, 0.43f, 0.23f) : new Color(0.22f, 0.56f, 0.67f);
            }
            if (view.SaveStatusText != null) view.SaveStatusText.text = game != null && game.Saves != null ? game.Saves.Status : "F5 保存 · F9 继续";
            if (view.LoadButton != null) view.LoadButton.interactable = game != null && game.Saves != null && game.Saves.HasSave;
            if (view.WaterButton != null) view.WaterButton.interactable = manager.PurifierUnlocked;
            if (view.WaterTargetMinusButton != null) view.WaterTargetMinusButton.interactable = game != null && game.Water != null && model.WaterTarget > 0;
            if (view.WaterTargetPlusButton != null) view.WaterTargetPlusButton.interactable = game != null && game.Water != null && model.WaterTarget < 20;
            if (view.NeedsText != null) view.NeedsText.text = model.NeedsStatus;
            if (view.EnergyText != null) view.EnergyText.text = model.EnergyStatus;
            if (view.EnergyFill != null)
            {
                view.EnergyFill.anchorMax = new Vector2(Mathf.Clamp01(model.Energy / 100), 1);
                view.EnergyFill.GetComponent<Image>().color = model.Energy < 25 ? new Color(.78f, .43f, .23f) : new Color(.59f, .48f, .70f);
            }
            if (view.SatietyFill != null)
            {
                view.SatietyFill.anchorMax = new Vector2(UnityEngine.Mathf.Clamp01(model.Satiety / 100f), 1f);
                view.SatietyFill.GetComponent<Image>().color = model.Satiety < 25f ? new Color(0.78f, 0.43f, 0.23f) : new Color(0.24f, 0.58f, 0.47f);
            }
            if (view.GrillText != null) view.GrillText.text = $"烤架 {model.Grills} · 净水 {model.Purifiers} · 网 {model.Nets}";
            if (view.TargetMinusButton != null) view.TargetMinusButton.interactable = production != null && model.FoodTarget > 0;
            if (view.TargetPlusButton != null) view.TargetPlusButton.interactable = production != null && model.FoodTarget < 20;
            if (view.FishingButton != null)
            {
                view.FishingButton.interactable = fishing != null;
                view.FishingButton.image.color = model.FishingEnabled ? new Color(0.12f, 0.50f, 0.44f) : new Color(0.22f, 0.34f, 0.32f);
            }
            if (view.FishingButtonText != null) view.FishingButtonText.text = model.FishingEnabled ? "空闲钓鱼 · 开" : "空闲钓鱼 · 关";
            view.ModeText.text = model.Tool == BuildTool.None ? "自由规划" :
                model.Tool == BuildTool.DeckBlueprint ? "扩建甲板" : model.Tool == BuildTool.BedBlueprint ? "床铺 · R 旋转" : model.Tool == BuildTool.CollectionNetBlueprint ? "收集网 · R 旋转" : model.Tool == BuildTool.GrillBlueprint ? "烤架 · R 旋转" : model.Tool == BuildTool.PurifierBlueprint ? "净水器 · R 旋转" : "撤销蓝图";
            if (FacilityRules.TryKind(model.Tool, out var selectedKind) && FacilityRules.IsDecoration(selectedKind)) view.ModeText.text = FacilityRules.Name(selectedKind) + " · R 旋转";
            if (model.Tool == BuildTool.PlanterBlueprint) view.ModeText.text = "种植箱 · R 旋转";
            view.BuildButton.image.color = model.Tool == BuildTool.DeckBlueprint ? new Color(0.12f, 0.46f, 0.39f) : new Color(0.23f, 0.36f, 0.32f);
            view.CancelButton.image.color = model.Tool == BuildTool.CancelBlueprint ? new Color(0.66f, 0.37f, 0.22f) : new Color(0.38f, 0.43f, 0.38f);
        }
        private void RenderCatalog()
        {
            float height = category == 2 ? 600 : 452;
            view.GetComponent<ManagementHudLayout>()?.ResizeCatalog(height);
            if (view.CatalogHint != null) view.CatalogHint.rectTransform.anchoredPosition = new Vector2(24, -height + 40);
            if (view.CatalogCloseHint != null) view.CatalogCloseHint.rectTransform.anchoredPosition = new Vector2(24, -height + 92);
            if (view.CatalogOverlay != null) view.CatalogOverlay.SetActive(manager.CatalogOpen);
            if (view.DeckRecipeButton != null) view.DeckRecipeButton.gameObject.SetActive(category == 0);
            if (view.GrillButton != null) view.GrillButton.gameObject.SetActive(category == 1);
            if (view.WaterButton != null) view.WaterButton.gameObject.SetActive(category == 1 && manager.PurifierUnlocked);
            if (view.NetButton != null)
            {
                view.NetButton.gameObject.SetActive(category == 1); view.NetButton.interactable = manager.HasFacilityAssets(FacilityKind.CollectionNet);
                view.NetButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(manager.PurifierUnlocked ? 660 : 436, -108);
            }
            if (view.BedButton != null) { view.BedButton.gameObject.SetActive(category == 2); view.BedButton.interactable = manager.HasFacilityAssets(FacilityKind.Bed); }
            if (view.PlanterButton != null) { view.PlanterButton.gameObject.SetActive(category == 2); view.PlanterButton.interactable = manager.HasFacilityAssets(FacilityKind.Planter); }
            if (view.FarmingCatalogHint != null) view.FarmingCatalogHint.SetActive(category == 2);
            if (view.PlantButton != null) view.PlantButton.gameObject.SetActive(category == 2);
            if (view.RugButton != null) view.RugButton.gameObject.SetActive(category == 2);
            if (view.DecorLampButton != null) view.DecorLampButton.gameObject.SetActive(category == 2);
            if (view.CatalogEmptyText != null) view.CatalogEmptyText.gameObject.SetActive(false);
            if (view.CatalogHeading != null) view.CatalogHeading.text = category == 0 ? "基础建造 · 1 张图纸" : category == 1 ? $"生产设施 · {(manager.PurifierUnlocked ? 3 : 2)} 张图纸" : "生活设施 · 5 张图纸";
            if (view.CatalogHint != null) view.CatalogHint.text = category == 2 ? "装饰保持通行 · 多种搭配提升舒适度，加快床上精力恢复" : manager.PurifierUnlocked ? "已发现净水器图纸 · 选择卡片后，在甲板上规划建造" : "未知图纸藏在漂流补给箱中 · 前期可从补给箱或垂钓获得瓶装淡水";
            SetRecipeCost(view.DeckRecipeCost, view.DeckScrapCost, manager.DeckCost); SetRecipeCost(view.GrillRecipeCost, view.GrillScrapCost, manager.GrillCost); SetRecipeCost(view.PurifierRecipeCost, view.PurifierScrapCost, manager.PurifierCost);
            SetRecipeCost(view.NetRecipeCost, view.NetScrapCost, manager.NetCost);
            SetRecipeCost(view.BedRecipeCost, view.BedScrapCost, manager.BedCost);
            SetRecipeCost(view.PlanterRecipeCost, view.PlanterScrapCost, manager.FacilityCost(FacilityKind.Planter));
            SetRecipeCost(view.PlantRecipeCost, view.PlantScrapCost, manager.FacilityCost(FacilityKind.Plant));
            SetRecipeCost(view.RugRecipeCost, view.RugScrapCost, manager.FacilityCost(FacilityKind.Rug));
            SetRecipeCost(view.DecorLampRecipeCost, view.DecorLampScrapCost, manager.FacilityCost(FacilityKind.DecorLamp));
            Button[] tabs = { view.BasicsCategoryButton, view.ProductionCategoryButton, view.LivingCategoryButton };
            for (int index = 0; index < tabs.Length; index++) if (tabs[index] != null) tabs[index].image.color = category == index ? new Color(.12f, .46f, .39f) : new Color(.23f, .36f, .32f);
            if (view.LootNotice != null && game != null && game.Loot != null)
            { view.LootNotice.SetActive(!string.IsNullOrEmpty(game.Loot.Message) && Time.unscaledTime <= noticeUntil); view.LootText.text = game.Loot.Message; }
        }
        private void RenderClock()
        {
            if (game == null || game.DayNight == null || !game.DayNight.IsInitialized || view.DayClockText == null) return;
            var day = game.DayNight;
            int minute = Mathf.FloorToInt(day.Hour * 60);
            string text = $"第 {day.Day} 天 · {minute / 60:00}:{minute % 60:00}";
            if (view.DayClockText.text != text) view.DayClockText.text = text;
            if (view.DayPhaseText != null && view.DayPhaseText.text != day.Phase) view.DayPhaseText.text = day.Phase;
            if (view.DayIcon != null) view.DayIcon.Present(day.IsNight);
            if (view.DayProgressFill != null) view.DayProgressFill.anchorMax = new Vector2(day.Hour / 24, 1);
        }
        private static void SetRecipeCost(Text text, Text scrap, ResourceCost cost) { if (text != null) text.text = cost.Wood.ToString(); if (scrap != null) scrap.text = cost.Scrap.ToString(); }

        protected override void OnClose()
        {
            manager.CatalogOpen = false;
            view.BuildButton.onClick.RemoveListener(ToggleBuild);
            view.CancelButton.onClick.RemoveListener(ToggleCancel);
            if (view.FishingButton != null) view.FishingButton.onClick.RemoveListener(ToggleFishing);
            if (view.GrillButton != null) view.GrillButton.onClick.RemoveListener(ToggleGrill);
            if (view.TargetMinusButton != null) view.TargetMinusButton.onClick.RemoveListener(LowerTarget);
            if (view.TargetPlusButton != null) view.TargetPlusButton.onClick.RemoveListener(RaiseTarget);
            manager.Changed -= Refresh;
            if (crew != null) crew.Changed -= Refresh;
            if (resources != null) resources.Changed -= Refresh;
            if (salvage != null) salvage.Changed -= Refresh;
            if (fishing != null) fishing.Changed -= Refresh;
            if (production != null) production.Changed -= Refresh;
            if (needs != null) needs.Changed -= Refresh;
            model.Changed -= Render;
            if (view.PauseButton != null) view.PauseButton.onClick.RemoveListener(TogglePause);
            if (view.SpeedOneButton != null) view.SpeedOneButton.onClick.RemoveListener(SpeedOne);
            if (view.SpeedTwoButton != null) view.SpeedTwoButton.onClick.RemoveListener(SpeedTwo);
            if (view.SpeedFourButton != null) view.SpeedFourButton.onClick.RemoveListener(SpeedFour);
            if (game != null && game.TimeControl != null) game.TimeControl.Changed -= Refresh;
            BindAdditional(false);
        }
    }
}
