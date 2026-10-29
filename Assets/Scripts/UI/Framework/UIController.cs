namespace RaftGame.UI
{
    public abstract class UIController
    {
        public UIView View { get; private set; }
        public UIModel Model { get; private set; }
        public bool IsOpen { get; private set; }

        internal void Initialize(UIView view, UIModel model)
        {
            View = view;
            Model = model;
            OnInitialize();
        }

        internal void Open()
        {
            if (IsOpen) { OnRefresh(); return; }
            IsOpen = true;
            View.SetVisible(true);
            OnOpen();
        }

        internal void Close()
        {
            if (!IsOpen) return;
            OnClose();
            IsOpen = false;
            View.SetVisible(false);
        }

        internal void Dispose()
        {
            Close();
            OnDispose();
            Model?.Dispose();
        }

        protected virtual void OnInitialize() { }
        protected abstract void OnOpen();
        protected virtual void OnRefresh() { }
        protected abstract void OnClose();
        protected virtual void OnDispose() { }
    }
}
