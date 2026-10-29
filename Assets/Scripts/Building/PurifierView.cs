using UnityEngine;

namespace RaftGame.Building
{
    public sealed class PurifierView : MonoBehaviour
    {
        public Transform CleanWater;
        public GameObject Steam;
        public void Present(bool working, float progress)
        {
            if (Steam != null) Steam.SetActive(working);
            if (CleanWater != null) CleanWater.localScale = new Vector3(1, working ? Mathf.Lerp(0.15f, 1f, progress) : 0.3f, 1);
        }
    }
}
