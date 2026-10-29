using UnityEngine;

namespace RaftGame.Ocean
{
    [CreateAssetMenu(menuName = "Raft Game/Day Night Config")]
    public sealed class DayNightConfig : ScriptableObject
    {
        [Min(60)] public float DayDuration = 600;
        [Range(0, 24)] public float StartHour = 8;
        [Min(0)] public float DayLightIntensity = 1.05f;
        [Min(0)] public float MoonLightIntensity = .42f;
        public Color DayLight = new Color(1, .95f, .85f);
        public Color MoonLight = new Color(.53f, .68f, 1);
        public Color SunsetLight = new Color(1, .69f, .43f);
        [Min(0)] public float LanternIntensity = 1.15f;
    }
}
