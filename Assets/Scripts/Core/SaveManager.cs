using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.Core
{
    public sealed class SaveManager : MonoBehaviour
    {
        private BuildManager build;
        private CrewManager crew;
        private ResourceManager resources;
        private SalvageManager salvage;
        private FishingManager fishing;
        private ProductionManager food;
        private WaterManager water;
        private NeedsManager needs;
        private TimeManager clock;
        private Ocean.OceanManager ocean;
        private CameraManager cameraManager;
        private LootManager loot;
        private Ocean.DayNightManager daylight;
        private Ocean.OceanLifeManager oceanLife;
        private string directory;
        private float autoTimer;
        public bool IsInitialized { get; private set; }
        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "F5 保存 · F9 读档";
        public string ManualPath => Path.Combine(directory, "slot_01.json");
        public string AutoPath => Path.Combine(directory, "autosave.json");
        public bool HasSave => IsInitialized && (File.Exists(ManualPath) || File.Exists(AutoPath));
        public event Action Changed;

        public void Initialize(BuildManager b, CrewManager c, ResourceManager r, SalvageManager s, FishingManager f,
            ProductionManager p, WaterManager w, NeedsManager n, TimeManager t, Ocean.OceanManager o, CameraManager cam, LootManager rewards = null,
            Ocean.DayNightManager lighting = null, Ocean.OceanLifeManager fish = null)
        {
            build = b; crew = c; resources = r; salvage = s; fishing = f; food = p; water = w; needs = n; clock = t; ocean = o; cameraManager = cam;
            loot = rewards;
            daylight = lighting; oceanLife = fish;
            directory = Application.isBatchMode ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ValidationSaves")) : Path.Combine(Application.persistentDataPath, "Saves");
            IsInitialized = true; Changed?.Invoke();
        }

        public SaveData Capture()
        {
            var data = new SaveData { SavedUtc = DateTime.UtcNow.ToString("O"), Inventory = resources.CaptureInventory(),
                CrewX = crew.CurrentCell.x, CrewZ = crew.CurrentCell.y, Satiety = needs.Satiety, Hydration = needs.Hydration,
                FoodTarget = food.Target, WaterTarget = water != null ? water.Target : 0, Rods = fishing.RodCount, AutomaticFishing = fishing.AutomaticFishing,
                Speed = clock.Speed, Paused = clock.IsPaused, ElapsedSeconds = clock.ElapsedSeconds, OceanTime = ocean.ElapsedTime,
                CameraFocus = cameraManager.Focus.position, CameraYaw = cameraManager.Yaw, CameraSize = cameraManager.Size,
                Floating = salvage.CaptureItems(), SpawnTimer = salvage.SpawnTimer, PurifierUnlocked = build.PurifierUnlocked,
                Loot = loot != null ? loot.Capture() : new LootState { RandomState = 1 }, DriftRandomState = salvage.RandomState, DriftSequence = salvage.Sequence };
            foreach (var cell in build.Cells.Values) data.Decks.Add(new DeckSave { X = cell.Coordinate.x, Z = cell.Coordinate.y, Complete = cell.State == DeckState.Complete, Progress = cell.ConstructionProgress });
            foreach (var cell in build.Grills.Values) data.Facilities.Add(new FacilitySave { X = cell.Coordinate.x, Z = cell.Coordinate.y, Rotation = cell.Rotation, Kind = cell.Kind, Complete = cell.State == DeckState.Complete, Progress = cell.ConstructionProgress });
            return data;
        }

        public bool Save(bool automatic = false)
        {
            if (!IsInitialized || IsBusy) return false;
            IsBusy = true;
            try
            {
                var data = Capture();
                if (!Validate(data, out string reason)) throw new InvalidDataException(reason);
                Directory.CreateDirectory(directory);
                string path = automatic ? AutoPath : ManualPath, temporary = path + ".tmp";
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                Status = automatic ? "已自动保存" : "保存成功 · F9 可恢复";
                autoTimer = 0; return true;
            }
            catch (Exception exception) { Status = "保存失败，请检查存储空间"; Debug.LogWarning("Save failed: " + exception.Message); return false; }
            finally { IsBusy = false; Changed?.Invoke(); }
        }

        public bool Load(bool automatic = false)
        {
            if (!IsInitialized || IsBusy) return false;
            string path = automatic ? AutoPath : File.Exists(ManualPath) ? ManualPath : AutoPath;
            if (!File.Exists(path)) { Status = "尚无可读取的存档"; Changed?.Invoke(); return false; }
            IsBusy = true;
            try
            {
                if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Save exceeds size limit.");
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                Migrate(data);
                if (!Validate(data, out string reason)) throw new InvalidDataException(reason);
                // Validation finishes before any runtime state is changed. Active task claims are never serialized.
                clock.SetPaused(true); crew.CancelAssignments();
                build.RestoreUnlocks(data.PurifierUnlocked); build.RestoreLayout(data.Decks, data.Facilities); resources.RestoreInventory(data.Inventory);
                crew.RestorePosition(new Vector2Int(data.CrewX, data.CrewZ));
                needs.SetSatiety(data.Satiety); needs.SetHydration(data.Hydration);
                food.SetTarget(data.FoodTarget); water?.SetTarget(data.WaterTarget); fishing.RestoreEquipment(data.Rods, data.AutomaticFishing);
                ocean.RestoreTime(data.OceanTime); salvage.RestoreItems(data.Floating, data.SpawnTimer, data.DriftRandomState, data.DriftSequence); loot?.Restore(data.Loot);
                cameraManager.RestorePose(data.CameraFocus, data.CameraYaw, data.CameraSize);
                clock.Restore(data.Speed, data.Paused, data.ElapsedSeconds);
                daylight?.Tick(); oceanLife?.Tick();
                Status = automatic || path == AutoPath ? "已恢复自动存档" : "读档成功"; autoTimer = 0; return true;
            }
            catch (Exception exception) { Status = "读取失败：存档损坏或版本不兼容"; Debug.LogWarning("Load rejected: " + exception.Message); return false; }
            finally { IsBusy = false; Changed?.Invoke(); }
        }

        public bool Validate(SaveData data, out string reason)
        {
            reason = "Invalid save data.";
            if (data == null || data.Version != 2 || !data.Inventory.IsValid || data.Decks == null || data.Facilities == null || data.Floating == null ||
                data.DriftRandomState == 0 || data.DriftSequence < data.Floating.Count || data.Loot.RandomState == 0 || data.Loot.CatchesWithoutWater < 0 || data.Loot.CratesWithoutBlueprint < 0 ||
                data.Decks.Count < 1 || data.Decks.Count > 4096 || data.Facilities.Count > data.Decks.Count || data.Floating.Count > 50 ||
                !Range(data.Satiety, 0, 100) || !Range(data.Hydration, 0, 100) || !Range(data.OceanTime, 0, 100000000) ||
                !Range(data.CameraSize, 1, 1000) || !Range(data.CameraYaw, -1000000, 1000000) || !Finite(data.CameraFocus) ||
                !Range(data.SpawnTimer, -100000, 100000) || double.IsNaN(data.ElapsedSeconds) || double.IsInfinity(data.ElapsedSeconds) || data.ElapsedSeconds < 0 ||
                data.Rods < 0 || data.Rods > 1 || data.FoodTarget < 0 || data.FoodTarget > 20 || data.WaterTarget < 0 || data.WaterTarget > 20 ||
                (data.Speed != 1 && data.Speed != 2 && data.Speed != 4)) return false;
            var all = new HashSet<Vector2Int>(); var complete = new HashSet<Vector2Int>();
            foreach (var deck in data.Decks)
            {
                if (deck == null || Math.Abs((long)deck.X) > build.MaximumCoordinate || Math.Abs((long)deck.Z) > build.MaximumCoordinate ||
                    !Range(deck.Progress, 0, 1) || !all.Add(deck.Coordinate)) return false;
                if (deck.Complete) complete.Add(deck.Coordinate);
            }
            var footprints = new HashSet<Vector2Int>(); var blocked = new HashSet<Vector2Int>();
            foreach (var facility in data.Facilities)
            {
                if (facility == null || (facility.Kind != FacilityKind.Grill && facility.Kind != FacilityKind.Purifier) ||
                    !build.HasFacilityAssets(facility.Kind) || facility.Rotation < 0 || facility.Rotation > 3 || !Range(facility.Progress, 0, 1) ||
                    (facility.Kind == FacilityKind.Purifier && !data.PurifierUnlocked) ||
                    !complete.Contains(facility.Coordinate) || !footprints.Add(facility.Coordinate)) return false;
                if (facility.Complete) blocked.Add(facility.Coordinate);
            }
            foreach (var facility in data.Facilities)
            {
                Vector2Int work = facility.Coordinate + GrillCell.WorkDirection(facility.Rotation);
                if (!complete.Contains(work) || footprints.Contains(work)) return false;
            }
            Vector2Int crewCell = new Vector2Int(data.CrewX, data.CrewZ);
            if (!complete.Contains(crewCell) || blocked.Contains(crewCell) || !Connected(complete, footprints) || !Connected(all, footprints)) return false;
            foreach (var item in data.Floating)
                if (item == null || !salvage.Supports(item.Kind) || !Range(item.DriftMultiplier, .1f, 3) || !Range(item.Windage, 0, 1) || !Finite(item.Position) || !Range(item.Age, 0, 100000) || !Range(item.Phase, -100000, 100000)) return false;
            reason = null; return true;
        }
        private static void Migrate(SaveData data)
        {
            if (data == null || data.Version != 1) return;
            data.Version = 2;
            data.Loot = new LootState { RandomState = SeaRandom.Seed() };
            data.DriftRandomState = SeaRandom.Seed(); data.DriftSequence = data.Floating != null ? data.Floating.Count : 0;
            if (data.Facilities != null) foreach (var facility in data.Facilities) if (facility != null && facility.Kind == FacilityKind.Purifier) data.PurifierUnlocked = true;
            if (data.Floating != null) foreach (var item in data.Floating) if (item != null)
            { item.Kind = item.Type == ResourceType.Wood ? FloatingKind.Wood : item.Type == ResourceType.Scrap ? FloatingKind.Scrap : (FloatingKind)(-1); item.DriftMultiplier = 1; item.Windage = .3f; }
        }
        private static bool Range(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        private static bool Finite(Vector3 value) => Range(value.x, -1000000, 1000000) && Range(value.y, -1000000, 1000000) && Range(value.z, -1000000, 1000000);
        private static bool Connected(HashSet<Vector2Int> cells, HashSet<Vector2Int> excluded)
        {
            var remaining = new HashSet<Vector2Int>(cells); remaining.ExceptWith(excluded); if (remaining.Count == 0) return false;
            var queue = new Queue<Vector2Int>(); foreach (var first in remaining) { queue.Enqueue(first); remaining.Remove(first); break; }
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (var direction in DeckPathfinder.Directions) if (remaining.Remove(current + direction)) queue.Enqueue(current + direction);
            }
            return remaining.Count == 0;
        }
        public void Tick(float unscaledDeltaTime)
        {
            if (!IsInitialized) return;
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (Application.isBatchMode || clock.IsPaused) return;
            autoTimer += Mathf.Max(0, unscaledDeltaTime); if (autoTimer >= 60) Save(true);
        }
        public void Shutdown() { IsInitialized = false; Changed = null; }
    }
}
