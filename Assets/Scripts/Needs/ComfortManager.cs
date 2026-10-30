using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Ocean;
namespace RaftGame.Needs
{
    public sealed class ComfortManager : MonoBehaviour
    {
        public static readonly FacilityKind[] Kinds = { FacilityKind.Plant, FacilityKind.Rug, FacilityKind.DecorLamp };
        private ComfortConfig configuration;
        private BuildManager build;
        private DayNightManager day;
        private readonly Dictionary<FacilityKind, int> counts = new Dictionary<FacilityKind, int>();
        private readonly List<DecorationView> lamps = new List<DecorationView>();
        private int layout = -1;
        private float rawScore;
        public bool IsInitialized { get; private set; }
        public float Score { get; private set; }
        public float RestMultiplier => IsInitialized ? 1 + Score / 100 * configuration.MaximumRestBonus : 1;
        public int Count { get; private set; }
        public event Action Changed;
        public bool Initialize(ComfortConfig config, BuildManager building, DayNightManager daylight)
        {
            if (IsInitialized) return true;
            if (config == null || config.FirstContribution <= 0 || config.RepeatFactor < 0 || config.RepeatFactor >= 1 ||
                config.MaximumRestBonus < 0 || config.MaximumRestBonus > 1 || building == null || !building.IsInitialized) return false;
            configuration = config; build = building; day = daylight; IsInitialized = true; build.Changed += Refresh; Refresh(); return true;
        }
        public int CountOf(FacilityKind kind) => counts.TryGetValue(kind, out var value) ? value : 0;
        public float Contribution(FacilityKind kind) => Points(CountOf(kind));
        public float NextContribution(FacilityKind kind) => IsInitialized && FacilityRules.IsDecoration(kind) ? configuration.FirstContribution * Mathf.Pow(configuration.RepeatFactor, CountOf(kind)) : 0;
        public float RemovalContribution(FacilityKind kind)
        {
            float marginal = Points(CountOf(kind)) - Points(Mathf.Max(0, CountOf(kind) - 1));
            return Score - Mathf.Clamp(rawScore - marginal, 0, 100);
        }
        private float Points(int count) => !IsInitialized || count <= 0 ? 0 : configuration.FirstContribution * (1 - Mathf.Pow(configuration.RepeatFactor, count)) / (1 - configuration.RepeatFactor);
        private void Refresh()
        {
            if (!IsInitialized || layout == build.LayoutVersion) return;
            layout = build.LayoutVersion; counts.Clear(); lamps.Clear(); Count = 0; float score = 0;
            foreach (var cell in build.Grills.Values) if (cell.State == DeckState.Complete && cell.IsDecoration)
            {
                counts[cell.Kind] = CountOf(cell.Kind) + 1; Count++;
                if (cell.DecorationView != null && cell.DecorationView.Lamp != null) lamps.Add(cell.DecorationView);
            }
            foreach (var kind in Kinds) score += Contribution(kind);
            rawScore = score; Score = Mathf.Clamp(score, 0, 100); Tick(); Changed?.Invoke();
        }
        public void Tick()
        {
            if (!IsInitialized) return;
            float night = day != null && day.IsInitialized ? 1 - day.Daylight : 0;
            foreach (var lamp in lamps) if (lamp != null) lamp.Present(night, configuration.LampIntensity);
        }
        public void Shutdown()
        {
            if (build != null) build.Changed -= Refresh;
            foreach (var lamp in lamps) if (lamp != null) lamp.Present(0, 0);
            lamps.Clear(); counts.Clear(); IsInitialized = false; Changed = null;
        }
    }
}
