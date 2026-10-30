using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RaftGame.Building
{
    /// <summary>Selection and relocation interaction. BuildManager owns all committed layout changes.</summary>
    public sealed class FacilityManager : MonoBehaviour
    {
        private BuildManager building;
        private Camera sceneCamera;
        private LineRenderer footprint, workCell;
        private GameObject ghost;
        private MaterialPropertyBlock tint;
        private bool clearing;
        public bool IsInitialized { get; private set; }
        public GrillCell Selected { get; private set; }
        public bool IsMoving => Selected != null && Selected.IsBeingMoved;
        public bool ConfirmingDemolition { get; private set; }
        public int Rotation { get; private set; }
        public string Message { get; private set; } = "免费搬移，拆除全额返还建材";
        public event Action Changed;
        public event Action SelectionChanged;

        public bool Initialize(BuildManager build, Camera camera, Material outlineMaterial)
        {
            if (IsInitialized) return true;
            if (build == null || !build.IsInitialized || camera == null || outlineMaterial == null) return false;
            building = build; sceneCamera = camera; tint = new MaterialPropertyBlock();
            footprint = Outline("Selected Facility", outlineMaterial, new Color(1f, .78f, .28f, .96f));
            workCell = Outline("Facility Work Cell", outlineMaterial, new Color(.25f, .92f, .72f, .8f));
            building.Changed += OnLayoutChanged; building.LayoutRestoring += ClearSelection;
            IsInitialized = true; return true;
        }

        public void Tick()
        {
            if (!IsInitialized) return;
            building.FacilityInputBlocked = false;
            if (building.WorldModalOpen) { ClearSelection(); building.FacilityInputBlocked = true; return; }
            if (building.CatalogOpen || building.Tool != BuildTool.None || Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.X))
            { ClearSelection(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                if (IsMoving) CancelMove(); else ClearSelection();
                building.FacilityInputBlocked = true; return;
            }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (IsMoving)
            {
                building.FacilityInputBlocked = true;
                if (Input.GetKeyDown(KeyCode.R)) Rotate();
                bool visible = !overUI && !Input.GetMouseButton(2) && building.TryPointerCell(out _);
                if (ghost != null) ghost.SetActive(visible);
                if (visible && building.TryPointerCell(out Vector2Int target))
                {
                    Preview(target);
                    if (Input.GetMouseButtonDown(0)) ConfirmMove(target);
                }
                return;
            }
            if (overUI || Input.GetMouseButton(2) || !Input.GetMouseButtonDown(0)) return;
            if (!TrySelectAt(sceneCamera.ScreenPointToRay(Input.mousePosition))) ClearSelection();
        }

        public bool TrySelectAt(Ray ray)
        {
            if (!IsInitialized || IsMoving || building.CatalogOpen || building.Tool != BuildTool.None || !building.TryPickFacility(ray, out GrillCell facility)) return false;
            if (!Select(facility)) return false;
            building.FacilityInputBlocked = true; return true;
        }

        public bool Select(GrillCell cell)
        {
            if (!IsInitialized || !building.IsCurrentFacility(cell) || building.CatalogOpen || building.Tool != BuildTool.None) return false;
            if (IsMoving) CancelMove();
            Selected = cell; ConfirmingDemolition = false; Rotation = cell.Rotation;
            Message = "免费搬移，拆除全额返还建材";
            ShowSelection(); SelectionChanged?.Invoke(); Changed?.Invoke(); return true;
        }

        public bool BeginMove()
        {
            if (!IsInitialized || Selected == null || IsMoving) return false;
            Rotation = Selected.Rotation; ConfirmingDemolition = false;
            if (!building.TryBeginFacilityMove(Selected)) return false;
            ghost = building.CreateFacilityPreview(Selected.Kind, transform);
            ghost.name = "Facility Move Preview"; ghost.SetActive(false);
            Message = "选择新位置 · R 旋转 · 右键取消";
            building.FacilityInputBlocked = true; Changed?.Invoke(); return true;
        }

        public void Rotate() { if (!IsMoving) return; Rotation = (Rotation + 1) % 4; Changed?.Invoke(); }

        public bool Preview(Vector2Int target)
        {
            if (!IsMoving || ghost == null) return false;
            bool valid = building.CanMoveFacility(Selected, target, Rotation, out string reason);
            ghost.SetActive(true); ghost.transform.position = building.CellToWorld(target) + Vector3.up * .035f;
            ghost.transform.rotation = building.FacilityRotation(Rotation);
            tint.SetColor("_Color", valid ? new Color(.15f, .91f, .70f, .45f) : new Color(1f, .28f, .20f, .45f));
            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(tint);
            string message = valid ? "左键确认搬移 · R 旋转 · 右键取消" : reason;
            if (Message != message) { Message = message; Changed?.Invoke(); }
            return valid;
        }

        public bool ConfirmMove(Vector2Int target)
        {
            if (!IsMoving) return false;
            if (!building.TryMoveFacility(Selected, target, Rotation, out string reason)) { Message = reason; Changed?.Invoke(); return false; }
            DestroyGhost(); Message = "搬移完成，生产将重新调度";
            ShowSelection(); Changed?.Invoke(); return true;
        }

        public void CancelMove()
        {
            if (!IsInitialized || !IsMoving) return;
            building.CancelFacilityMove(Selected); DestroyGhost(); Rotation = Selected.Rotation;
            Message = "已取消，设施仍在原位"; Changed?.Invoke();
        }

        public void RequestDemolition()
        {
            if (Selected == null || IsMoving) return;
            ConfirmingDemolition = true; Message = "确认拆除？甲板保留，建材全部返还"; Changed?.Invoke();
        }
        public void CancelDemolition() { ConfirmingDemolition = false; Message = "免费搬移，拆除全额返还建材"; Changed?.Invoke(); }
        public bool ConfirmDemolition()
        {
            if (!ConfirmingDemolition || Selected == null || IsMoving) return false;
            if (!building.TryDemolishFacility(Selected, out string reason)) { Message = reason; Changed?.Invoke(); return false; }
            ClearSelection(); return true;
        }

        public void ClearSelection()
        {
            if (clearing || Selected == null) return;
            clearing = true;
            try
            {
                if (IsMoving) building.CancelFacilityMove(Selected);
                DestroyGhost(); Selected = null; ConfirmingDemolition = false;
                footprint.gameObject.SetActive(false); workCell.gameObject.SetActive(false);
                SelectionChanged?.Invoke(); Changed?.Invoke();
            }
            finally { clearing = false; }
        }

        private void OnLayoutChanged()
        {
            if (Selected == null || clearing) return;
            if (!building.IsCurrentFacility(Selected)) ClearSelection();
            else { if (!IsMoving) DestroyGhost(); ShowSelection(); Changed?.Invoke(); }
        }
        private LineRenderer Outline(string name, Material material, Color color)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(transform, false);
            line.useWorldSpace = false; line.sharedMaterial = material; line.loop = true; line.positionCount = 4; line.widthMultiplier = .055f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", color); line.SetPropertyBlock(properties);
            line.gameObject.SetActive(false); return line;
        }
        private void ShowSelection()
        {
            if (Selected == null) return;
            SetOutline(footprint, Selected.Coordinate, .94f); SetOutline(workCell, Selected.WorkCell, .62f);
            if (Selected.IsDecoration) workCell.gameObject.SetActive(false);
        }
        private void SetOutline(LineRenderer line, Vector2Int coordinate, float radius)
        {
            float scale = building.TileSize / 2f; radius *= scale;
            line.transform.position = building.CellToWorld(coordinate) + Vector3.up * .07f;
            line.transform.rotation = building.FacilityRotation(0);
            line.SetPositions(new[] { new Vector3(-radius, 0, -radius), new Vector3(-radius, 0, radius), new Vector3(radius, 0, radius), new Vector3(radius, 0, -radius) });
            line.gameObject.SetActive(true);
        }
        private void DestroyGhost() { if (ghost != null) { ghost.SetActive(false); Destroy(ghost); ghost = null; } }
        public void Shutdown()
        {
            if (!IsInitialized) return;
            ClearSelection(); building.Changed -= OnLayoutChanged; building.LayoutRestoring -= ClearSelection; building.FacilityInputBlocked = false;
            Destroy(footprint.gameObject); Destroy(workCell.gameObject); IsInitialized = false; Changed = null; SelectionChanged = null;
        }
    }
}
