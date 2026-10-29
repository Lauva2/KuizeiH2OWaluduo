using UnityEngine;

namespace RaftGame.Ocean
{
    /// <summary>Owns water configuration, animation and CPU height queries for future floating objects.</summary>
    [DisallowMultipleComponent]
    public sealed class OceanManager : MonoBehaviour
    {
        [SerializeField] private MeshRenderer surfaceRenderer;
        private OceanConfig configuration;
        private Transform followTarget;
        private MaterialPropertyBlock properties;
        private float elapsedTime;
        private float daylight = 1, twilight;
        public Color LitDeepColor => Color.Lerp(Color.Lerp(new Color(.018f, .075f, .16f), configuration.DeepColor, daylight), new Color(.11f, .22f, .31f), twilight * .3f);
        public Color LitShallowColor => Color.Lerp(Color.Lerp(new Color(.06f, .24f, .30f), configuration.ShallowColor, daylight), new Color(.30f, .46f, .43f), twilight * .3f);
        public void SetLighting(float day, float dusk)
        { daylight = Mathf.Clamp01(day); twilight = Mathf.Clamp01(dusk); if (IsInitialized) WriteProperties(); }
        private Color LitReflectionColor => Color.Lerp(Color.Lerp(new Color(.14f, .34f, .48f), configuration.ReflectionColor, daylight), new Color(.82f, .56f, .38f), twilight * .45f);
        private Color LitFoamColor => Color.Lerp(new Color(.30f, .51f, .60f), configuration.FoamColor, daylight);
        public void WriteSubmergedProperties(MaterialPropertyBlock target, Vector3 position)
        {
            target.SetColor("_WaterTint", Color.Lerp(LitShallowColor, LitDeepColor, GetDeepWaterBlend(position)));
            target.SetColor("_ReflectionColor", LitReflectionColor); target.SetColor("_FoamColor", LitFoamColor);
            target.SetFloat("_FishDepth", Mathf.Max(0, GetSurfaceHeight(position) - position.y)); target.SetFloat("_OceanTime", elapsedTime);
            target.SetVector("_SurfaceDetails", new Vector4(configuration.WaveA.x, configuration.WaveA.y, configuration.RippleScale, configuration.RippleStrength));
        }

        private static readonly int OceanTime = Shader.PropertyToID("_OceanTime");
        private static readonly int WaveA = Shader.PropertyToID("_WaveA");
        private static readonly int WaveB = Shader.PropertyToID("_WaveB");
        private static readonly int WaveC = Shader.PropertyToID("_WaveC");
        private static readonly int WaveSpeed = Shader.PropertyToID("_WaveSpeed");
        private static readonly int DeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int ShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int FoamColor = Shader.PropertyToID("_FoamColor");
        private static readonly int ReflectionColor = Shader.PropertyToID("_ReflectionColor");
        private static readonly int RippleStrength = Shader.PropertyToID("_RippleStrength");
        private static readonly int RippleScale = Shader.PropertyToID("_RippleScale");
        private static readonly int CrestFoam = Shader.PropertyToID("_CrestFoam");
        private static readonly int ContactWidth = Shader.PropertyToID("_ContactFoamWidth");
        private static readonly int ContactStrength = Shader.PropertyToID("_ContactFoamStrength");
        private static readonly int SunGlint = Shader.PropertyToID("_SunGlint");
        private static readonly int ReflectionStrength = Shader.PropertyToID("_ReflectionStrength");
        private static readonly int ShallowArea = Shader.PropertyToID("_ShallowArea");
        private static readonly int DepthTransition = Shader.PropertyToID("_DepthTransition");

        public bool IsInitialized { get; private set; }
        public float ElapsedTime => elapsedTime;
        public Vector2 CurrentDirection => Rotate(configuration.CurrentDirection.sqrMagnitude > .001f ? configuration.CurrentDirection.normalized : Vector2.down,
            Mathf.Sin(elapsedTime / Mathf.Max(20, configuration.CurrentTurnPeriod) * Mathf.PI * 2) * configuration.CurrentTurnDegrees);
        public Vector2 WindDirection => Rotate(configuration.WindDirection.sqrMagnitude > .001f ? configuration.WindDirection.normalized : Vector2.right,
            Mathf.Sin(elapsedTime / Mathf.Max(30, configuration.CurrentTurnPeriod * 1.7f) * Mathf.PI * 2 + .8f) * 55);
        public Vector2 GetDriftDirection(Vector3 position, float windage)
        {
            Vector2 direction = CurrentDirection + WindDirection * (configuration.WindInfluence * Mathf.Clamp01(windage));
            float eddy = (Mathf.PerlinNoise(position.x * .035f + 43, position.z * .035f + elapsedTime * .001f) - .5f) * 24;
            return Rotate(direction.normalized, eddy);
        }
        private static Vector2 Rotate(Vector2 direction, float degrees)
        { float angle = degrees * Mathf.Deg2Rad; return new Vector2(direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle), direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle)); }
        public void RestoreTime(float time) { elapsedTime = time; Tick(0); }

        public bool Initialize(OceanConfig config, Transform target)
        {
            if (config == null || config.Material == null || surfaceRenderer == null ||
                surfaceRenderer.GetComponent<MeshFilter>()?.sharedMesh == null)
            {
                Debug.LogError("OceanManager requires an OceanConfig, material and surface mesh.", this);
                return false;
            }

            configuration = config;
            followTarget = target;
            properties = new MaterialPropertyBlock();
            elapsedTime = 0f;
            surfaceRenderer.sharedMaterial = config.Material;
            IsInitialized = true;
            Tick(0f);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized) return;
            elapsedTime += Mathf.Max(0f, deltaTime);
            Vector3 center = surfaceRenderer.transform.position;
            if (followTarget != null)
            {
                // Snap by whole mesh cells so recentering does not change the sampled geometry.
                float cellSize = configuration.SurfaceSize / configuration.GridResolution;
                float step = Mathf.Max(1f, Mathf.Round(configuration.FollowStep / cellSize)) * cellSize;
                center.x = Mathf.Round(followTarget.position.x / step) * step;
                center.z = Mathf.Round(followTarget.position.z / step) * step;
            }
            center.y = configuration.SeaLevel;
            surfaceRenderer.transform.position = center;
            WriteProperties();
        }

        private void WriteProperties()
        {
            properties.SetFloat(OceanTime, elapsedTime);
            properties.SetVector(WaveA, configuration.WaveA);
            properties.SetVector(WaveB, configuration.WaveB);
            properties.SetVector(WaveC, configuration.WaveC);
            properties.SetFloat(WaveSpeed, configuration.WaveSpeed);
            properties.SetColor(DeepColor, LitDeepColor);
            properties.SetColor(ShallowColor, LitShallowColor);
            properties.SetColor(FoamColor, LitFoamColor);
            properties.SetColor(ReflectionColor, LitReflectionColor);
            properties.SetFloat(RippleStrength, configuration.RippleStrength);
            properties.SetFloat(RippleScale, configuration.RippleScale);
            properties.SetFloat(CrestFoam, configuration.CrestFoam);
            properties.SetFloat(ContactWidth, configuration.ContactFoamWidth);
            properties.SetFloat(ContactStrength, configuration.ContactFoamStrength);
            properties.SetFloat(SunGlint, configuration.SunGlint * Mathf.Lerp(.18f, 1, daylight));
            properties.SetFloat(ReflectionStrength, configuration.ReflectionStrength);
            properties.SetVector(ShallowArea, new Vector4(configuration.ShallowCenter.x, configuration.ShallowCenter.y,
                configuration.ShallowRadii.x, configuration.ShallowRadii.y));
            properties.SetVector(DepthTransition, new Vector4(configuration.DepthTransitionWidth,
                configuration.BoundaryVariation, 0f, 0f));
            surfaceRenderer.SetPropertyBlock(properties);
        }

        /// <summary>World-fixed region; waves, sea level and camera recentering do not change it.</summary>
        public OceanRegion GetRegion(Vector3 worldPosition)
        {
            return GetDeepWaterBlend(worldPosition) < 0.5f ? OceanRegion.Shallow : OceanRegion.Deep;
        }

        public float GetDeepWaterBlend(Vector3 worldPosition)
        {
            return IsInitialized ? SampleDeepWaterBlend(configuration, worldPosition) : 1f;
        }

        /// <summary>Design depth in metres, measured below sea level; not a seabed mesh raycast.</summary>
        public float GetWaterDepth(Vector3 worldPosition)
        {
            return IsInitialized ? Mathf.Lerp(configuration.ShallowDepth, configuration.DeepDepth,
                SampleDeepWaterBlend(configuration, worldPosition)) : 0f;
        }

        // Keep this equation in sync with DeepWaterBlend in StylizedOcean.shader.
        public static float SampleDeepWaterBlend(OceanConfig config, Vector3 worldPosition)
        {
            float radiusX = Mathf.Max(1f, config.ShallowRadii.x);
            float radiusZ = Mathf.Max(1f, config.ShallowRadii.y);
            Vector2 offset = new Vector2((worldPosition.x - config.ShallowCenter.x) / radiusX,
                (worldPosition.z - config.ShallowCenter.y) / radiusZ);
            float edgeDistance = (offset.magnitude - 1f) * Mathf.Min(radiusX, radiusZ);
            float variation = (Mathf.Sin(worldPosition.x * 0.085f + worldPosition.z * 0.035f) +
                0.5f * Mathf.Sin(worldPosition.z * 0.13f - worldPosition.x * 0.04f)) * config.BoundaryVariation;
            float width = Mathf.Max(0.1f, config.DepthTransitionWidth);
            float t = Mathf.Clamp01((edgeDistance + variation) / width + 0.5f);
            return t * t * (3f - 2f * t);
        }

        public float GetSurfaceHeight(Vector3 worldPosition)
        {
            if (!IsInitialized) return surfaceRenderer != null ? surfaceRenderer.transform.position.y : transform.position.y;
            return SampleHeight(configuration, worldPosition, elapsedTime);
        }

        // Keep this equation in sync with EvaluateWave in StylizedOcean.shader.
        public static float SampleHeight(OceanConfig config, Vector3 worldPosition, float time)
        {
            return config.SeaLevel + EvaluateWave(config.WaveA, worldPosition, time, config.WaveSpeed) +
                EvaluateWave(config.WaveB, worldPosition, time, config.WaveSpeed) +
                EvaluateWave(config.WaveC, worldPosition, time, config.WaveSpeed);
        }

        private static float EvaluateWave(Vector4 wave, Vector3 position, float time, float speed)
        {
            Vector2 direction = new Vector2(wave.x, wave.y).normalized;
            float frequency = 2f * Mathf.PI / Mathf.Max(0.5f, wave.z);
            float phase = frequency * (direction.x * position.x + direction.y * position.z) -
                time * speed * Mathf.Sqrt(9.81f * frequency);
            return wave.w * Mathf.Sin(phase);
        }

        public void Shutdown()
        {
            if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(null);
            IsInitialized = false;
            properties = null;
            followTarget = null;
        }
    }
}
