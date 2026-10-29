using System.Collections.Generic;
using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Ocean
{
    /// <summary>Small schools of decorative, submerged fish. Shared meshes, no colliders or inventory rewards.</summary>
    public sealed class OceanLifeManager : MonoBehaviour
    {
        private sealed class Fish
        {
            public OceanFishView View;
            public Vector2 Center, Offset;
            public float Phase, Radius, Speed, Scale;
            public Color Color;
        }
        private readonly List<Fish> fish = new List<Fish>();
        private OceanLifeConfig configuration;
        private OceanManager ocean;
        private DayNightManager daylight;
        public bool IsInitialized { get; private set; }
        public int Count => fish.Count;
        public OceanFishView GetFish(int index) => fish[index].View;
        public bool Initialize(OceanLifeConfig config, OceanManager sea, DayNightManager lighting)
        {
            if (IsInitialized) return true;
            if (config == null || config.FishPrefab == null || config.FishPrefab.GetComponent<OceanFishView>() == null || sea == null || !sea.IsInitialized) return false;
            configuration = config; ocean = sea; daylight = lighting;
            int state = config.Seed == 0 ? 1 : config.Seed;
            int schools = Mathf.Clamp(config.Schools, 1, 8), count = Mathf.Clamp(config.FishPerSchool, 2, 8);
            for (int school = 0; school < schools; school++)
            {
                float bearing = school * Mathf.PI * 2 / schools + SeaRandom.Range(ref state, -.3f, .3f);
                float radius = SeaRandom.Range(ref state, config.MinimumRadius, Mathf.Max(config.MinimumRadius, config.MaximumRadius));
                Vector2 center = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing)) * radius;
                float phase = SeaRandom.Range(ref state, 0, Mathf.PI * 2);
                Color tint = school % 3 == 0 ? config.SilverFish : school % 3 == 1 ? config.CoralFish : config.BlueFish;
                float speed = SeaRandom.Range(ref state, .8f, 1.2f) * config.SwimSpeed;
                GameObject prefab = school % 3 == 1 ? config.CoralFishPrefab : school % 3 == 2 ? config.BlueFishPrefab : config.FishPrefab;
                if (prefab == null || prefab.GetComponent<OceanFishView>() == null) prefab = config.FishPrefab;
                for (int member = 0; member < count; member++)
                {
                    var view = Instantiate(prefab, transform).GetComponent<OceanFishView>();
                    view.name = "School " + (school + 1) + " Fish " + (member + 1);
                    fish.Add(new Fish { View = view, Center = center, Offset = new Vector2((member % 2 - .5f) * .68f, member / 2 * .85f),
                        Phase = phase, Radius = radius, Speed = speed, Scale = SeaRandom.Range(ref state, .82f, 1.16f), Color = tint });
                }
            }
            IsInitialized = true; Tick(); return true;
        }
        public void Tick()
        {
            if (!IsInitialized) return;
            float time = ocean.ElapsedTime;
            foreach (var item in fish)
            {
                float angle = item.Phase + time * item.Speed / 3.6f;
                Vector2 swim = new Vector2(Mathf.Cos(angle) * 2.8f, Mathf.Sin(angle) * 1.9f);
                Vector2 forward = new Vector2(-Mathf.Sin(angle) * 2.8f, Mathf.Cos(angle) * 1.9f).normalized;
                Vector2 side = new Vector2(forward.y, -forward.x);
                Vector2 position = item.Center + swim + side * item.Offset.x - forward * item.Offset.y;
                var world = new Vector3(position.x, 0, position.y);
                world.y = ocean.GetSurfaceHeight(world) - configuration.SwimDepth - .08f * Mathf.Sin(time * .8f + item.Offset.y);
                item.View.transform.SetPositionAndRotation(world, Quaternion.LookRotation(new Vector3(forward.x, 0, forward.y)));
                item.View.transform.localScale = Vector3.one * item.Scale;
                item.View.Present(time, item.Phase + item.Offset.y * 2, item.Color, ocean, daylight != null ? daylight.Daylight : 1);
            }
        }
        public void Shutdown()
        {
            foreach (var item in fish) if (item.View != null) Destroy(item.View.gameObject);
            fish.Clear(); IsInitialized = false;
        }
    }
}
