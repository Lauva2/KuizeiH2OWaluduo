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
                if (productionManager != null && !productionManager.Initialize(configuration.Production, buildManager, resourceManager))
                { Debug.LogError("Invalid production configuration.", this); IsInitialized = false; enabled = false; return; }
                if (needsManager != null && !needsManager.Initialize(configuration.Needs, resourceManager))
                { Debug.LogError("Invalid needs configuration.", this); IsInitialized = false; enabled = false; return; }
                if (waterManager != null && !waterManager.Initialize(configuration.Water, buildManager, resourceManager))
                { Debug.LogError("Invalid water configuration.", this); IsInitialized = false; enabled = false; return; }
                if (crewManager != null && !crewManager.Initialize(configuration.Crew, buildManager, cameraManager.SceneCamera, salvageManager, fishingManager, productionManager, needsManager, waterManager))
                { IsInitialized = false; enabled = false; return; }
                if (crewManager != null && resourceManager != null && salvageManager != null && fishingManager != null && productionManager != null && needsManager != null)
                {
                    saveManager = gameObject.AddComponent<SaveManager>();
                    saveManager.Initialize(buildManager, crewManager, resourceManager, salvageManager, fishingManager, productionManager, waterManager, needsManager, timeManager, oceanManager, cameraManager, Loot, dayNightManager, oceanLifeManager);
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
            buildPanel?.ProcessInput();
            timeManager.ProcessInput();
            if (saveManager != null) saveManager.Tick(Time.unscaledDeltaTime);
            float simulationDeltaTime = timeManager.IsPaused ? 0 : Time.deltaTime;
            timeManager.Tick(simulationDeltaTime);
            // Camera input remains responsive when simulation time is paused.
            cameraManager.Tick(Time.unscaledDeltaTime);
            oceanManager.Tick(simulationDeltaTime);
            if (dayNightManager != null) dayNightManager.Tick();
            if (oceanLifeManager != null) oceanLifeManager.Tick();
            if (buildManager != null) buildManager.Tick();
            if (salvageManager != null) salvageManager.Tick(simulationDeltaTime);
            if (fishingManager != null) fishingManager.ProcessInput();
            if (needsManager != null) needsManager.Tick(simulationDeltaTime);
            if (crewManager != null) crewManager.Tick(simulationDeltaTime);
        }

        private void OnDestroy()
        {
            if (saveManager != null) saveManager.Shutdown();
            if (uiManager != null) uiManager.Shutdown();
            if (crewManager != null) crewManager.Shutdown();
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
        private void OnApplicationQuit() { if (IsInitialized && saveManager != null && !Application.isBatchMode) saveManager.Save(true); }
    }
}
