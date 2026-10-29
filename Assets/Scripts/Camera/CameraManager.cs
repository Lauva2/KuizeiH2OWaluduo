using UnityEngine;
using UnityEngine.EventSystems;

namespace RaftGame.Core
{
    /// <summary>Stable, ground-plane camera controls; driven exclusively by GameManager.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraManager : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Transform focus;
        private GameConfig configuration;
        private float yaw;
        private float size;
        private Vector3 previousPointer;

        public Transform Focus => focus;
        public Camera SceneCamera => sceneCamera;

        public bool Initialize(GameConfig config)
        {
            if (config == null || sceneCamera == null || focus == null)
            {
                Debug.LogError("CameraManager is missing its camera, focus or configuration.", this);
                return false;
            }

            configuration = config;
            yaw = config.CameraYaw;
            size = Mathf.Clamp(config.CameraSize, config.MinimumCameraSize, config.MaximumCameraSize);
            sceneCamera.orthographic = true;
            sceneCamera.depthTextureMode |= DepthTextureMode.Depth;
            ApplyPose();
            previousPointer = Input.mousePosition;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (configuration == null) return;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Quaternion rotation = Quaternion.Euler(configuration.CameraTilt, yaw, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            Vector2 movement = new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            movement = Vector2.ClampMagnitude(movement, 1f);
            focus.position += (right * movement.x + forward * movement.y) *
                (configuration.PanSpeed * size / configuration.CameraSize * deltaTime);

            if (!overUI)
            {
                size = Mathf.Clamp(size - Input.mouseScrollDelta.y * configuration.ZoomSpeed,
                    configuration.MinimumCameraSize, configuration.MaximumCameraSize);
                yaw += ((Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f)) *
                    configuration.RotateSpeed * deltaTime;

                if (Input.GetMouseButton(2) && !Input.GetMouseButtonDown(2))
                {
                    // Ray/plane dragging remains consistent at every zoom and aspect ratio.
                    Plane ground = new Plane(Vector3.up, focus.position);
                    Ray previous = sceneCamera.ScreenPointToRay(previousPointer);
                    Ray current = sceneCamera.ScreenPointToRay(Input.mousePosition);
                    if (ground.Raycast(previous, out float oldDistance) &&
                        ground.Raycast(current, out float newDistance))
                        focus.position += previous.GetPoint(oldDistance) - current.GetPoint(newDistance);
                }
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                focus.position = Vector3.zero;
                yaw = configuration.CameraYaw;
                size = configuration.CameraSize;
            }

            previousPointer = Input.mousePosition;
            ApplyPose();
        }

        private void ApplyPose()
        {
            Quaternion rotation = Quaternion.Euler(configuration.CameraTilt, yaw, 0f);
            sceneCamera.orthographicSize = size;
            sceneCamera.transform.SetPositionAndRotation(focus.position - rotation * Vector3.forward * 85f, rotation);
        }
    }
}
