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
                if (salvageManager != null && !salvageManager.Initialize(configuration.Salvage, buildManager, oceanManager, resourceManager, cameraManager.SceneCamera))
                { Debug.LogError("Invalid salvage configuration.", this); IsInitialized = false; enabled = false; return; }
                if (fishingManager != null && !fishingManager.Initialize(configuration.Fishing, buildManager, resourceManager, oceanManager))
                { Debug.LogError("Invalid fishing configuration.", this); IsInitialized = false; enabled = false; return; }
                if (productionManager != null && !productionManager.Initialize(configuration.Production, buildManager, resourceManager))
                { Debug.LogError("Invalid production configuration.", this); IsInitialized = false; enabled = false; return; }
                if (needsManager != null && !needsManager.Initialize(configuration.Needs, resourceManager))
                { Debug.LogError("Invalid needs configuration.", this); IsInitialized = false; enabled = false; return; }
                if (crewManager != null && !crewManager.Initialize(configuration.Crew, buildManager, cameraManager.SceneCamera, salvageManager, fishingManager, productionManager, needsManager))
                { IsInitialized = false; enabled = false; return; }
                uiManager.Register<UICommon_Build, BuildPanelModel>(UICommon_Build.Address,
                    () => new BuildPanelController(buildManager, crewManager, resourceManager, salvageManager, fishingManager, productionManager, needsManager), () => new BuildPanelModel());
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
            // Camera input remains responsive when simulation time is paused.
            cameraManager.Tick(Time.unscaledDeltaTime);
            oceanManager.Tick(Time.deltaTime);
            if (buildManager != null) buildManager.Tick();
            if (salvageManager != null) salvageManager.Tick(Time.deltaTime);
            if (fishingManager != null) fishingManager.ProcessInput();
            if (needsManager != null) needsManager.Tick(Time.deltaTime);
            if (crewManager != null) crewManager.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (uiManager != null) uiManager.Shutdown();
            if (crewManager != null) crewManager.Shutdown();
            if (needsManager != null) needsManager.Shutdown();
            if (productionManager != null) productionManager.Shutdown();
            if (fishingManager != null) fishingManager.Shutdown();
            if (salvageManager != null) salvageManager.Shutdown();
            if (buildManager != null) buildManager.Shutdown();
            if (resourceManager != null) resourceManager.Shutdown();
            if (oceanManager != null) oceanManager.Shutdown();
            IsInitialized = false;
        }
    }
}
