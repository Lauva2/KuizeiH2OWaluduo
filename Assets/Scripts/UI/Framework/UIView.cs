using UnityEngine;

namespace RaftGame.UI
{
    public abstract class UIView : MonoBehaviour
    {
        [SerializeField] private UILayer layer = UILayer.Common;
        public bool cache = true;
        public UILayer Layer => layer;
        public virtual void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
