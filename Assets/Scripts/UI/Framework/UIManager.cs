using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace RaftGame.UI
{
    /// <summary>Owns panel registration, Addressables handles, MVC and close/cache lifecycle.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private RectTransform[] layers;
        [SerializeField] private Transform inactiveRoot;
        private readonly Dictionary<string, Registration> registrations = new Dictionary<string, Registration>();
        private readonly Dictionary<string, Panel> instances = new Dictionary<string, Panel>();
        private Font font;
        public bool IsInitialized { get; private set; }
        public int InstanceCount => instances.Count;

        private sealed class Registration
        {
            public Type ViewType;
            public Func<UIController> Controller;
            public Func<UIModel> Model;
        }

        private sealed class Panel
        {
            public string Address;
            public Registration Registration;
            public AsyncOperationHandle<GameObject> Handle;
            public bool HasHandle;
            public bool WantOpen;
            public UIView View;
            public UIController Controller;
            public Task<UIView> Loading;
        }

        public bool Initialize()
        {
            if (IsInitialized) return true;
            if (layers == null || layers.Length != 4 || inactiveRoot == null)
            { Debug.LogError("UIManager requires four layer roots and an inactive staging root.", this); return false; }
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) return false;
                layers[i].SetSiblingIndex(i);
            }
            inactiveRoot.gameObject.SetActive(false);
            // Use installed CJK fonts without redistributing a proprietary font file.
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC", "Arial" }, 20);
            IsInitialized = true;
            return true;
        }

        public void Register<TView, TModel>(string address, Func<UIController> controllerFactory, Func<TModel> modelFactory)
            where TView : UIView where TModel : UIModel
        {
            if (!IsInitialized) throw new InvalidOperationException("Initialize UIManager before registering panels.");
            if (string.IsNullOrWhiteSpace(address) || registrations.ContainsKey(address))
                throw new ArgumentException("A panel address must be nonempty and unique: " + address);
            registrations.Add(address, new Registration { ViewType = typeof(TView), Controller = controllerFactory, Model = () => modelFactory() });
        }

        public Task<UIView> OpenAsync(string address)
        {
            if (!IsInitialized) throw new InvalidOperationException("UIManager is not initialized.");
            if (!registrations.TryGetValue(address, out Registration registration))
                throw new ArgumentException("Panel is not registered: " + address);
            if (instances.TryGetValue(address, out Panel existing))
            {
                existing.WantOpen = true;
                if (existing.View != null)
                {
                    existing.View.transform.SetAsLastSibling();
                    existing.Controller.Open();
                    return Task.FromResult(existing.View);
                }
                return existing.Loading;
            }
            var entry = new Panel { Address = address, Registration = registration, WantOpen = true };
            instances.Add(address, entry);
            entry.Loading = LoadAsync(entry);
            return entry.Loading;
        }

        private async Task<UIView> LoadAsync(Panel entry)
        {
            try
            {
                entry.Handle = Addressables.LoadAssetAsync<GameObject>(entry.Address);
                entry.HasHandle = true;
                GameObject prefab = await entry.Handle.Task;
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded || prefab == null)
                    throw new InvalidOperationException("Unable to load UI prefab: " + entry.Address, entry.Handle.OperationException);
                // A close/destroy/shutdown can happen while AA is loading. Never resurrect that panel.
                if (!IsCurrent(entry)) { Release(entry); return null; }
                UIView prefabView = prefab.GetComponent<UIView>();
                if (prefabView == null || !entry.Registration.ViewType.IsInstanceOfType(prefabView))
                    throw new InvalidOperationException("UI prefab has the wrong UIView: " + entry.Address);
                if (!entry.Address.StartsWith("UI" + prefabView.Layer + "_", StringComparison.Ordinal))
                    throw new InvalidOperationException("UI name does not match its UIView layer: " + entry.Address);
                if (!entry.WantOpen && !prefabView.cache)
                { instances.Remove(entry.Address); Release(entry); return null; }

                GameObject instance = Instantiate(prefab, inactiveRoot, false);
                instance.name = entry.Address;
                entry.View = instance.GetComponent<UIView>();
                entry.View.SetVisible(false);
                entry.View.transform.SetParent(layers[(int)entry.View.Layer], false);
                var rect = (RectTransform)entry.View.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                foreach (Text text in instance.GetComponentsInChildren<Text>(true)) text.font = font;
                entry.Controller = entry.Registration.Controller();
                entry.Controller.Initialize(entry.View, entry.Registration.Model());
                if (entry.WantOpen) entry.Controller.Open();
                return entry.View;
            }
            catch
            {
                if (IsCurrent(entry)) instances.Remove(entry.Address);
                Release(entry);
                throw;
            }
        }

        private bool IsCurrent(Panel entry) => IsInitialized &&
            instances.TryGetValue(entry.Address, out Panel current) && ReferenceEquals(current, entry);

        public UIView GetView(string address) => instances.TryGetValue(address, out Panel entry) ? entry.View : null;
        public bool IsOpen(string address) => instances.TryGetValue(address, out Panel entry) && entry.Controller != null && entry.Controller.IsOpen;

        public void Close(string address)
        {
            if (!instances.TryGetValue(address, out Panel entry)) return;
            entry.WantOpen = false;
            if (entry.View == null) return;
            entry.Controller.Close();
            if (!entry.View.cache) DestroyPanel(address);
        }

        public void DestroyPanel(string address)
        {
            if (!instances.TryGetValue(address, out Panel entry)) return;
            instances.Remove(address);
            entry.WantOpen = false;
            // A pending load owns its handle until its completion continuation can release it.
            if (entry.View != null) Release(entry);
        }

        private void Release(Panel entry)
        {
            entry.Controller?.Dispose();
            entry.Controller = null;
            if (entry.View != null)
            {
                entry.View.SetVisible(false);
                Destroy(entry.View.gameObject);
                entry.View = null;
            }
            if (entry.HasHandle)
            {
                if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
                entry.HasHandle = false;
            }
        }

        public void Shutdown()
        {
            IsInitialized = false;
            foreach (Panel entry in instances.Values)
                if (entry.View != null) Release(entry);
            instances.Clear();
            registrations.Clear();
            if (font != null) Destroy(font);
            font = null;
        }
    }
}
