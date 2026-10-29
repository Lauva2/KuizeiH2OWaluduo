using UnityEngine;
namespace RaftGame.Resources
{
    public static class SeaRandom
    {
        public static float Value(ref int state)
        {
            uint value = state == 0 ? 0x6D2B79F5u : unchecked((uint)state);
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            state = unchecked((int)value);
            return (value >> 8) / 16777216f;
        }
        public static float Range(ref int state, float min, float max) => Mathf.Lerp(min, max, Value(ref state));
        public static int Seed() => Random.Range(1, int.MaxValue);
    }
}
