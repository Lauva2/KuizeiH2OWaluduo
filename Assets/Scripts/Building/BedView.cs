using UnityEngine;
namespace RaftGame.Building
{
    public sealed class BedView : MonoBehaviour
    {
        public Transform SleepAnchor;
        public GameObject RestMarker;
        public void Present(bool occupied, Camera camera = null)
        {
            if (RestMarker == null) return;
            RestMarker.SetActive(occupied);
            if (occupied && camera != null) RestMarker.transform.rotation = camera.transform.rotation;
        }
    }
}
