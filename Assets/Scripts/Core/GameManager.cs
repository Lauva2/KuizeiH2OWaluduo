using UnityEngine;
using RaftGame.Ocean;
using RaftGame.Building;
using RaftGame.UI;
using System;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.Core
{
    /// <summary>Owns configuration and the explicit initialization/tick order of game systems.</summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameConfig configuration;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private OceanManager oceanManager;
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private CrewManager crewManager;
        [SerializeField] private ResourceManager resourceManager;
        [SerializeField] private SalvageManager salvageManager;
        [SerializeField] private FishingManager fishingManager;
        [SerializeField] private ProductionManager productionManager;
        [SerializeField] private NeedsManager needsManager;
        [SerializeField] private TimeManager timeManager;
        public TimeManager TimeControl => timeManager;
        [SerializeField] private WaterManager waterManager;
        public WaterManager Water => waterManager;
        private SaveManager saveManager;
        public SaveManager Saves => saveManager;
        public LootManager Loot { get; private set; }
        private BuildPanelController buildPanel;
        [SerializeField] private FacilityManager facilityManager;
        public FacilityManager Facilities => facilityManager;
        [SerializeField] private CollectionNetManager collectionNetManager;
        public CollectionNetManager CollectionNets => collectionNetManager;
        [SerializeField] private RescueManager rescueManager;
        public RescueManager Recruitment => rescueManager;
        public CrewManager Crew => crewManager;
        public NeedsManager Needs => needsManager;
        [SerializeField] private RestManager restManager;
        public RestManager Rest => restManager;
        public bool CrewPanelOpen { get; private set; }
        [SerializeField] private ComfortManager comfortManager;
        public ComfortManager Comfort => comfortManager;
        [SerializeField] private RaftGame.Farming.FarmingManager farmingManager;
        public RaftGame.Farming.FarmingManager Farming => farmingManager;
        public bool ComfortPanelOpen { get; private set; }
        public bool WorldPanelOpen => CrewPanelOpen || ComfortPanelOpen;
        [SerializeField] private DayNightManager dayNightManager;
        public DayNightManager DayNight => dayNightManager;
        private OceanLifeManager oceanLifeManager;
        public OceanLifeManager OceanLife => oceanLifeManager;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (configuration == null || cameraManager == null || oceanManager == null)
            {
                Debug.LogError("GameManager requires a GameConfig, CameraManager and OceanManager.", this);
                enabled = false;
                return;
            }

            if (!cameraManager.Initialize(configuration) ||
                !oceanManager.Initialize(configuration.Ocean, cameraManager.Focus))
            {
                enabled = false;
                return;
            }

            IsInitialized = true;
            if (timeManager == null) timeManager = gameObject.AddComponent<TimeManager>();
            timeManager.Initialize();
            if (configuration.DayNight != null && (dayNightManager == null || !dayNightManager.Initialize(configuration.DayNight, timeManager, oceanManager, cameraManager.SceneCamera)))
            { Debug.LogError("Invalid day/night configuration or scene lighting.", this); IsInitialized = false; enabled = false; return; }
            if (configuration.OceanLife != null)
            {
                oceanLifeManager = gameObject.AddComponent<OceanLifeManager>();
                if (!oceanLifeManager.Initialize(configuration.OceanLife, oceanManager, dayNightManager))
                { Debug.LogError("Invalid ocean life configuration.", this); IsInitialized = false; enabled = false; return; }
            }
            if (buildManager != null)
            {
                if (resourceManager != null && !resourceManager.Initialize(configuration.Resources))
                { Debug.LogError("Invalid resource configuration.", this); IsInitialized = false; enabled = false; return; }
                if (!buildManager.Initialize(configuration.Building, cameraManager.SceneCamera, resourceManager) || uiManager == null || !uiManager.Initialize())
                {
                    Debug.LogError("Building requires BuildConfig and an initialized UIManager.", this);
                    IsInitialized = false;
                    enabled = false;
                    return;
                }
                Loot = gameObject.AddComponent<LootManager>();
                if (!Loot.Initialize(configuration.Loot, resourceManager, buildManager))
                { Debug.LogError("Invalid loot configuration.", this); IsInitialized = false; enabled = false; return; }
                if (salvageManager != null && !salvageManager.Initialize(configuration.Salvage, buildManager, oceanManager, resourceManager, cameraManager.SceneCamera, Loot))
                { Debug.LogError("Invalid salvage configuration.", this); IsInitialized = false; enabled = false; return; }
                if (fishingManager != null && !fishingManager.Initialize(configuration.Fishing, buildManager, resourceManager, oceanManager, Loot))
                { Debug.LogError("Invalid fishing configuration.", this); IsInitialized = false; enabled = false; return; }
                if (farmingManager != null && !farmingManager.Initialize(configuration.Farming, buildManager, resourceManager, needsManager))
                { Debug.LogError("Invalid farming configuration.", this); IsInitialized = false; enabled = false; return; }
                if (productionManager != null && !productionManager.Initialize(configuration.Production, buildManager, resourceManager, farmingManager))
                { Debug.LogError("Invalid production configuration.", this); IsInitialized = false; enabled = false; return; }
                if (needsManager != null && !needsManager.Initialize(configuration.Needs, resourceManager))
                { Debug.LogError("Invalid needs configuration.", this); IsInitialized = false; enabled = false; return; }
                if (waterManager != null && !waterManager.Initialize(configuration.Water, buildManager, resourceManager))
                { Debug.LogError("Invalid water configuration.", this); IsInitialized = false; enabled = false; return; }
                if (collectionNetManager != null && !collectionNetManager.Initialize(configuration.CollectionNets, buildManager, salvageManager, resourceManager, Loot))
                { Debug.LogError("Invalid collection net configuration.", this); IsInitialized = false; enabled = false; return; }
                if (crewManager != null && !crewManager.Initialize(configuration.Crew, buildManager, cameraManager.SceneCamera, salvageManager, fishingManager, productionManager, needsManager, waterManager, collectionNetManager, farmingManager))
                { IsInitialized = false; enabled = false; return; }
                if (rescueManager != null && !rescueManager.Initialize(configuration.Rescue, crewManager, buildManager, oceanManager, cameraManager.SceneCamera))
                { Debug.LogError("Invalid rescue configuration.", this); IsInitialized = false; enabled = false; return; }
                if (comfortManager != null && !comfortManager.Initialize(configuration.Comfort, buildManager, dayNightManager))
                { Debug.LogError("Invalid comfort configuration.", this); IsInitialized = false; enabled = false; return; }
                if (restManager != null && !restManager.Initialize(configuration.Rest, buildManager, crewManager, comfortManager))
                { Debug.LogError("Invalid rest configuration.", this); IsInitialized = false; enabled = false; return; }
                if (crewManager != null) uiManager.Register<UIPop_Crew, CrewPanelModel>(UIPop_Crew.Address,
                    () => new CrewPanelController(this, crewManager), () => new CrewPanelModel());
                if (comfortManager != null) uiManager.Register<UIPop_Comfort, ComfortPanelModel>(UIPop_Comfort.Address,
                    () => new ComfortPanelController(this, comfortManager), () => new ComfortPanelModel());
                if (facilityManager != null)
                {
                    var outlineMaterial = configuration.Building.BlueprintPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
                    if (!facilityManager.Initialize(buildManager, cameraManager.SceneCamera, outlineMaterial))
                    { Debug.LogError("Invalid facility interaction setup.", this); IsInitialized = false; enabled = false; return; }
                    uiManager.Register<UICommon_Facility, FacilityPanelModel>(UICommon_Facility.Address,
                        () => new FacilityPanelController(facilityManager, crewManager, productionManager, waterManager, collectionNetManager, comfortManager, farmingManager), () => new FacilityPanelModel());
                    facilityManager.SelectionChanged += SyncFacilityPanel;
                }
                if (crewManager != null && resourceManager != null && salvageManager != null && fishingManager != null && productionManager != null && needsManager != null)
                {
                    saveManager = gameObject.AddComponent<SaveManager>();
                    saveManager.Initialize(buildManager, crewManager, resourceManager, salvageManager, fishingManager, productionManager, waterManager, needsManager, timeManager, oceanManager, cameraManager, Loot, dayNightManager, oceanLifeManager, rescueManager, comfortManager);
                }
                uiManager.Register<UICommon_Build, BuildPanelModel>(UICommon_Build.Address,
                    () => buildPanel = new BuildPanelController(buildManager, crewManager, resourceManager, salvageManager, fishingManager, productionManager, needsManager, this), () => new BuildPanelModel());
            }
        }

        private async void Start()
        {
            if (!IsInitialized || uiManager == null) return;
            try { await uiManager.OpenAsync(UICommon_Build.Address); }
            catch (Exception exception) { if (this != null) Debug.LogException(exception, this); }
        }

        private void LateUpdate()
        {
            if (!IsInitialized) return;
            if (Input.GetKeyDown(KeyCode.C)) { if (CrewPanelOpen) CloseCrewPanel(); else OpenCrewPanel(); }
            if (Input.GetKeyDown(KeyCode.V)) { if (ComfortPanelOpen) CloseComfortPanel(); else OpenComfortPanel(); }
            if (CrewPanelOpen && Input.GetKeyDown(KeyCode.Escape)) CloseCrewPanel();
            if (ComfortPanelOpen && Input.GetKeyDown(KeyCode.Escape)) CloseComfortPanel();
            buildPanel?.ProcessInput();
            timeManager.ProcessInput();
            if (saveManager != null) saveManager.Tick(Time.unscaledDeltaTime);
            float simulationDeltaTime = timeManager.IsPaused ? 0 : Time.deltaTime;
            timeManager.Tick(simulationDeltaTime);
            // Camera input remains responsive when simulation time is paused.
            cameraManager.Tick(Time.unscaledDeltaTime);
            oceanManager.Tick(simulationDeltaTime);
            if (dayNightManager != null) dayNightManager.Tick();
            if (comfortManager != null) comfortManager.Tick();
            if (oceanLifeManager != null) oceanLifeManager.Tick();
            if (facilityManager != null) facilityManager.Tick();
            if (buildManager != null) buildManager.Tick();
            if (rescueManager != null) rescueManager.Tick(simulationDeltaTime);
            if (salvageManager != null) salvageManager.Tick(simulationDeltaTime);
            if (fishingManager != null) fishingManager.ProcessInput();
            if (needsManager != null) needsManager.Tick(simulationDeltaTime);
            if (farmingManager != null) farmingManager.Tick(simulationDeltaTime);
            if (crewManager != null) crewManager.Tick(simulationDeltaTime);
        }

        private void OnDestroy()
        {
            if (saveManager != null) saveManager.Shutdown();
            CloseCrewPanel();
            CloseComfortPanel();
            if (facilityManager != null) { facilityManager.SelectionChanged -= SyncFacilityPanel; facilityManager.Shutdown(); }
            if (uiManager != null) uiManager.Shutdown();
            if (crewManager != null) crewManager.Shutdown();
            if (farmingManager != null) farmingManager.Shutdown();
            if (restManager != null) restManager.Shutdown();
            if (comfortManager != null) comfortManager.Shutdown();
            if (rescueManager != null) rescueManager.Shutdown();
            if (collectionNetManager != null) collectionNetManager.Shutdown();
            if (needsManager != null) needsManager.Shutdown();
            if (productionManager != null) productionManager.Shutdown();
            if (waterManager != null) waterManager.Shutdown();
            if (fishingManager != null) fishingManager.Shutdown();
            if (salvageManager != null) salvageManager.Shutdown();
            if (buildManager != null) buildManager.Shutdown();
            if (Loot != null) Loot.Shutdown();
            if (resourceManager != null) resourceManager.Shutdown();
            if (oceanLifeManager != null) oceanLifeManager.Shutdown();
            if (dayNightManager != null) dayNightManager.Shutdown();
            if (oceanManager != null) oceanManager.Shutdown();
            if (timeManager != null) timeManager.Shutdown();
            IsInitialized = false;
        }
        public async void OpenCrewPanel()
        {
            if (!IsInitialized || crewManager == null || uiManager == null) return;
            CrewPanelOpen = true; CloseComfortPanel(); buildManager.WorldModalOpen = true; buildManager.CatalogOpen = false; buildManager.SetTool(BuildTool.None); facilityManager?.ClearSelection();
            try { await uiManager.OpenAsync(UIPop_Crew.Address); }
            catch (Exception exception) { if (this != null) { NotifyCrewPanelClosed(); Debug.LogException(exception, this); } }
        }
        public void NotifyCrewPanelClosed() { CrewPanelOpen = false; if (buildManager != null) buildManager.WorldModalOpen = WorldPanelOpen; }
        public void CloseCrewPanel() { NotifyCrewPanelClosed(); if (uiManager != null) uiManager.Close(UIPop_Crew.Address); }
        public async void OpenComfortPanel()
        {
            if (!IsInitialized || comfortManager == null || uiManager == null) return;
            ComfortPanelOpen = true; CloseCrewPanel(); buildManager.WorldModalOpen = true; buildManager.CatalogOpen = false; buildManager.SetTool(BuildTool.None); facilityManager?.ClearSelection();
            try { await uiManager.OpenAsync(UIPop_Comfort.Address); }
            catch (Exception exception) { if (this != null) { NotifyComfortPanelClosed(); Debug.LogException(exception, this); } }
        }
        public void NotifyComfortPanelClosed() { ComfortPanelOpen = false; if (buildManager != null) buildManager.WorldModalOpen = WorldPanelOpen; }
        public void CloseComfortPanel() { NotifyComfortPanelClosed(); if (uiManager != null) uiManager.Close(UIPop_Comfort.Address); }
        private async void SyncFacilityPanel()
        {
            if (facilityManager.Selected == null) { uiManager.Close(UICommon_Facility.Address); return; }
            try { await uiManager.OpenAsync(UICommon_Facility.Address); }
            catch (Exception exception) { if (this != null) Debug.LogException(exception, this); }
        }
        private void OnApplicationQuit() { if (IsInitialized && saveManager != null && !Application.isBatchMode) saveManager.Save(true); }
    }
}
