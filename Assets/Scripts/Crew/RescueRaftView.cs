using UnityEngine;
namespace RaftGame.Crew
{
    public sealed class RescueRaftView : MonoBehaviour
    {
        public GameObject Marker;
        public CrewView Person;
        public void Initialize(Camera camera)
        { Person.Initialize(camera); Person.ApplyCrewColor(2); SetMarked(false); }
        public void SetMarked(bool marked) { if (Marker != null) Marker.SetActive(marked); }
    }
}
