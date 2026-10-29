using System;

namespace RaftGame.UI
{
    public abstract class UIModel : IDisposable
    {
        public event Action Changed;
        protected void NotifyChanged() => Changed?.Invoke();
        public virtual void Dispose() => Changed = null;
    }
}
