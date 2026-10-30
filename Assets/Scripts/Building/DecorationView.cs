using UnityEngine;
namespace RaftGame.Building
{
    public sealed class DecorationView : MonoBehaviour
    {
        public Light Lamp;
        public Renderer Glow;
        private MaterialPropertyBlock properties;
        public void Present(float night, float intensity)
        {
            if (Lamp == null) return;
            Lamp.intensity = Mathf.Clamp01(night) * intensity; Lamp.enabled = Lamp.intensity > .01f;
            if (Glow == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", Color.Lerp(new Color(.43f, .45f, .34f), new Color(1, .82f, .40f), night));
            properties.SetColor("_EmissionColor", new Color(1, .70f, .25f) * (night * 2)); Glow.SetPropertyBlock(properties);
        }
    }
}
