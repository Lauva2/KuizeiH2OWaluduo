using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Core;

namespace RaftGame.Ocean
{
    /// <summary>Derived from the saved simulation clock; never advances a second, competing timer.</summary>
    public sealed class DayNightManager : MonoBehaviour
    {
        [SerializeField] private Light mainLight;
        [SerializeField] private Light lantern;
        [SerializeField] private Renderer lanternGlass;
        [SerializeField] private Renderer lanternCore;
        private DayNightConfig configuration;
        private TimeManager clock;
        private OceanManager ocean;
        private Camera sceneCamera;
        private AmbientMode originalMode;
        private Color originalSky, originalEquator, originalGround, originalBackground, originalLight;
        private float originalIntensity;
        private Quaternion originalRotation;
        private MaterialPropertyBlock glassProperties;
        public bool IsInitialized { get; private set; }
        public float DayDuration => configuration.DayDuration;
        private double TotalDays => configuration.StartHour / 24.0 + clock.ElapsedSeconds / configuration.DayDuration;
        public int Day => (int)System.Math.Min(int.MaxValue, System.Math.Floor(TotalDays) + 1);
        public float Hour => (float)((TotalDays - System.Math.Floor(TotalDays)) * 24);
        public float Daylight { get; private set; }
        public float Twilight { get; private set; }
        public bool IsNight => Hour < 6 || Hour >= 19;
        public string Phase => Hour < 5 || Hour >= 19 ? "夜晚" : Hour < 7 ? "清晨" : Hour < 17 ? "白昼" : "黄昏";
        public Light MainLight => mainLight;
        public Light Lantern => lantern;
        public bool Initialize(DayNightConfig config, TimeManager time, OceanManager sea, Camera camera)
        {
            if (IsInitialized) return true;
            if (config == null || config.DayDuration < 60 || float.IsNaN(config.DayDuration) || float.IsInfinity(config.DayDuration) || time == null || !time.IsInitialized || sea == null || !sea.IsInitialized || camera == null || mainLight == null) return false;
            configuration = config; clock = time; ocean = sea; sceneCamera = camera;
            originalMode = RenderSettings.ambientMode; originalSky = RenderSettings.ambientSkyColor;
            originalEquator = RenderSettings.ambientEquatorColor; originalGround = RenderSettings.ambientGroundColor;
            originalBackground = camera.backgroundColor; originalLight = mainLight.color;
            originalIntensity = mainLight.intensity; originalRotation = mainLight.transform.rotation;
            glassProperties = new MaterialPropertyBlock(); IsInitialized = true; Tick(); return true;
        }
        public void Tick()
        {
            if (!IsInitialized) return;
            float elevation = Mathf.Sin((Hour - 6) / 24 * Mathf.PI * 2);
            Daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.12f, .28f, elevation));
            Twilight = Mathf.Sin(Daylight * Mathf.PI);
            mainLight.color = Color.Lerp(configuration.MoonLight, Color.Lerp(configuration.DayLight, configuration.SunsetLight, Twilight * .9f), Daylight);
            mainLight.intensity = Mathf.Lerp(configuration.MoonLightIntensity, configuration.DayLightIntensity, Daylight);
            mainLight.transform.rotation = Quaternion.Euler(elevation > 0 ? 15 + elevation * 40 : 38, 210 + (Hour - 12) * 7, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.22f, .31f, .46f), new Color(.73f, .85f, .88f), Daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.14f, .22f, .34f), new Color(.53f, .70f, .71f), Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.08f, .13f, .21f), new Color(.30f, .40f, .39f), Daylight);
            ocean.SetLighting(Daylight, Twilight);
            sceneCamera.backgroundColor = ocean.LitDeepColor;
            if (lantern != null)
            {
                lantern.intensity = configuration.LanternIntensity * (1 - Daylight);
                lantern.enabled = lantern.intensity > .01f;
            }
            if (lanternGlass != null)
            {
                glassProperties.SetColor("_EmissionColor", new Color(1, .78f, .35f) * (1 - Daylight) * 2.3f);
                glassProperties.SetColor("_Color", Color.Lerp(new Color(1, .86f, .48f), new Color(.34f, .40f, .32f), Daylight));
                lanternGlass.SetPropertyBlock(glassProperties);
                if (lanternCore != null) lanternCore.SetPropertyBlock(glassProperties);
            }
        }
        public void Shutdown()
        {
            if (!IsInitialized) return;
            RenderSettings.ambientMode = originalMode; RenderSettings.ambientSkyColor = originalSky;
            RenderSettings.ambientEquatorColor = originalEquator; RenderSettings.ambientGroundColor = originalGround;
            if (sceneCamera != null) sceneCamera.backgroundColor = originalBackground;
            if (mainLight != null) { mainLight.color = originalLight; mainLight.intensity = originalIntensity; mainLight.transform.rotation = originalRotation; }
            if (lantern != null) lantern.enabled = false;
            if (lanternGlass != null) lanternGlass.SetPropertyBlock(null);
            if (lanternCore != null) lanternCore.SetPropertyBlock(null);
            IsInitialized = false;
        }
    }
}
