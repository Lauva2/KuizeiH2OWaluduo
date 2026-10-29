using UnityEngine;

namespace RaftGame.Ocean
{
    public sealed class OceanFishView : MonoBehaviour
    {
        [SerializeField] private Transform tail;
        [SerializeField] private Renderer[] parts;
        private MaterialPropertyBlock properties;
        public Transform Tail => tail;
        public void Present(float time, float phase, Color color, OceanManager sea, float daylight)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_FishColor", color);
            sea.WriteSubmergedProperties(properties, transform.position);
            properties.SetFloat("_Daylight", daylight);
            foreach (var part in parts) if (part != null) part.SetPropertyBlock(properties);
            if (tail != null) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 7 + phase) * 24, 0);
        }
    }
}
