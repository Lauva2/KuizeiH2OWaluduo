using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RaftGame.Core
{
    public sealed class TimeManager : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }
        public bool IsPaused { get; private set; }
        public int Speed { get; private set; } = 1;
        public double ElapsedSeconds { get; private set; }
        public event Action Changed;

        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true; Speed = 1; IsPaused = false; ElapsedSeconds = 0;
            Apply();
        }

        public bool SetSpeed(int speed)
        {
            if (!IsInitialized || (speed != 1 && speed != 2 && speed != 4)) return false;
            Speed = speed; IsPaused = false; Apply(); return true;
        }

        public void SetPaused(bool paused)
        {
            if (!IsInitialized || IsPaused == paused) return;
            IsPaused = paused; Apply();
        }

        public void TogglePause() => SetPaused(!IsPaused);
        private void Apply() { Time.timeScale = IsPaused ? 0 : Speed; Changed?.Invoke(); }
        public void Tick(float deltaTime) { if (IsInitialized) ElapsedSeconds += Mathf.Max(0, deltaTime); }

        public void ProcessInput()
        {
            if (!IsInitialized) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<InputField>() != null) return;
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(4);
        }

        public void Restore(int speed, bool paused, double seconds)
        {
            Speed = speed; IsPaused = paused; ElapsedSeconds = seconds; Apply();
        }

        public void Shutdown() { Time.timeScale = 1; IsInitialized = false; Changed = null; }
    }
}
