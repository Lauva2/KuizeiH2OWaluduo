using UnityEngine;

namespace RaftGame.Ocean
{
    [CreateAssetMenu(menuName = "Raft Game/Ocean Config")]
    public sealed class OceanConfig : ScriptableObject
    {
        [Header("Surface")]
        public Material Material;
        public float SeaLevel;
        [Min(100f)] public float SurfaceSize = 320f;
        [Range(32, 240)] public int GridResolution = 192;
        [Min(1f)] public float FollowStep = 16f;

        [Header("Palette")]
        public Color DeepColor = new Color(0.025f, 0.16f, 0.34f);
        public Color ShallowColor = new Color(0.12f, 0.70f, 0.66f);
        public Color FoamColor = new Color(0.82f, 0.96f, 0.91f);
        public Color ReflectionColor = new Color(0.48f, 0.80f, 0.83f);

        [Header("Sea regions (fixed world X/Z coordinates)")]
        public Vector2 ShallowCenter = new Vector2(-28f, 12f);
        public Vector2 ShallowRadii = new Vector2(29f, 40f);
        [Min(0.1f)] public float DepthTransitionWidth = 14f;
        [Min(0f)] public float BoundaryVariation = 4f;
        [Min(0.1f)] public float ShallowDepth = 3f;
        [Min(1f)] public float DeepDepth = 45f;

        [Header("Waves: direction X/Z, wavelength, amplitude (metres)")]
        public Vector4 WaveA = new Vector4(1f, 0.35f, 12f, 0.14f);
        public Vector4 WaveB = new Vector4(-0.5f, 1f, 6.5f, 0.075f);
        public Vector4 WaveC = new Vector4(0.2f, 1f, 3.1f, 0.03f);
        [Range(0f, 2f)] public float WaveSpeed = 0.45f;

        [Header("Surface details")]
        [Range(0f, 1f)] public float RippleStrength = 0.32f;
        [Range(0.1f, 2f)] public float RippleScale = 0.8f;
        [Range(0f, 1f)] public float CrestFoam = 0.22f;
        [Range(0.05f, 2f)] public float ContactFoamWidth = 0.5f;
        [Range(0f, 1f)] public float ContactFoamStrength = 0.75f;
        [Range(0f, 1f)] public float SunGlint = 0.32f;

        public float MaximumWaveHeight => Mathf.Abs(WaveA.w) + Mathf.Abs(WaveB.w) + Mathf.Abs(WaveC.w);

        private void OnValidate()
        {
            SurfaceSize = Mathf.Max(100f, SurfaceSize);
            GridResolution = Mathf.Clamp(GridResolution, 32, 240);
            FollowStep = Mathf.Clamp(FollowStep, 1f, SurfaceSize * 0.1f);
            ShallowRadii = new Vector2(Mathf.Max(1f, ShallowRadii.x), Mathf.Max(1f, ShallowRadii.y));
            DepthTransitionWidth = Mathf.Max(0.1f, DepthTransitionWidth);
            BoundaryVariation = Mathf.Max(0f, BoundaryVariation);
            ShallowDepth = Mathf.Max(0.1f, ShallowDepth);
            DeepDepth = Mathf.Max(ShallowDepth + 1f, DeepDepth);
            WaveA = SanitizeWave(WaveA);
            WaveB = SanitizeWave(WaveB);
            WaveC = SanitizeWave(WaveC);
        }

        private static Vector4 SanitizeWave(Vector4 wave)
        {
            if (wave.x * wave.x + wave.y * wave.y < 0.0001f) wave.x = 1f;
            wave.z = Mathf.Max(0.5f, wave.z);
            wave.w = Mathf.Clamp(wave.w, 0f, 1f);
            return wave;
        }
    }
}
