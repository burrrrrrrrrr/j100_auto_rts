using MultiplayerRts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MultiplayerRts.Bootstrap
{
    /// <summary>
    /// Bottom-center placement HUD (demo): "Place Spawner" / "Place Tower" buttons arm a
    /// placement mode; the next left-click on the map places that structure for the local
    /// player via MatchBootstrap.TryPlace (authoritative placement pipeline). A status line
    /// shows the last rejection reason. Builds its own Canvas in code.
    ///
    /// Uses the new Input System (the project has Input handling set to the Input System
    /// package): the EventSystem gets an InputSystemUIInputModule so uGUI buttons work, and
    /// map clicks are read from Mouse.current rather than the legacy UnityEngine.Input.
    /// </summary>
    public sealed class PlacementUI : MonoBehaviour
    {
        private MatchBootstrap _bootstrap;
        private Camera _camera;

        private enum PlaceMode { MeleeSpawner, RangedSpawner, Tower, Wall }
        private PlaceMode? _armed;
        private Text _statusText;
        private Text _armedText;

        private EventSystem _eventSystem;
        private readonly System.Collections.Generic.List<RectTransform> _buttonRects =
            new System.Collections.Generic.List<RectTransform>();

        public void Bind(MatchBootstrap bootstrap, Camera cam)
        {
            _bootstrap = bootstrap;
            _camera = cam;
        }

        private void Start()
        {
            EnsureEventSystem();
            BuildCanvas();
        }

        private void EnsureEventSystem()
        {
            _eventSystem = FindObjectOfType<EventSystem>();
            if (_eventSystem == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                _eventSystem = es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            else if (_eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                // Replace a legacy StandaloneInputModule (incompatible with Input System) if present.
                var legacy = _eventSystem.GetComponent<StandaloneInputModule>();
                if (legacy != null) { Destroy(legacy); }
                _eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("PlacementCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("PlacementPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 8f);
            prt.sizeDelta = new Vector2(560f, 36f);

            // Four buttons side by side within the strip; only these block map clicks.
            MakeButton(panel.transform, "Spawner", new Vector2(-204f, 0f), () => Arm(PlaceMode.MeleeSpawner));
            MakeButton(panel.transform, "Ranged",  new Vector2( -68f, 0f), () => Arm(PlaceMode.RangedSpawner));
            MakeButton(panel.transform, "Tower",   new Vector2(  68f, 0f), () => Arm(PlaceMode.Tower));
            MakeButton(panel.transform, "Wall",    new Vector2( 204f, 0f), () => Arm(PlaceMode.Wall));

            // Labels float ABOVE the strip and never block clicks (raycastTarget off).
            _armedText  = MakeLabel(panel.transform, new Vector2(0f, 54f), "Click a button, then click the map");
            _statusText = MakeLabel(panel.transform, new Vector2(0f, 32f), string.Empty);
        }

        private Button MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130f, 30f);
            rt.anchoredPosition = pos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            _buttonRects.Add(rt);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) { txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            txt.text = label; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white; txt.fontSize = 16;
            return btn;
        }

        private Text MakeLabel(Transform parent, Vector2 pos, string initial)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(440f, 24f);
            rt.anchoredPosition = pos;
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) { txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white; txt.fontSize = 14;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.raycastTarget = false; // labels never block map clicks
            txt.text = initial;
            return txt;
        }

        private void Arm(PlaceMode mode)
        {
            _armed = mode;
            if (_armedText != null) { _armedText.text = $"Placing: {Label(mode)}  (click the map; right-click/ESC to cancel)"; }
            if (_statusText != null) { _statusText.text = string.Empty; }
        }

        private static string Label(PlaceMode m)
        {
            switch (m)
            {
                case PlaceMode.MeleeSpawner: return "Melee Spawner";
                case PlaceMode.RangedSpawner: return "Ranged Spawner";
                case PlaceMode.Tower: return "Tower";
                case PlaceMode.Wall: return "Wall";
                default: return m.ToString();
            }
        }

        private void Cancel()
        {
            _armed = null;
            if (_armedText != null) { _armedText.text = "Click a button, then click the map"; }
        }

        private void Update()
        {
            if (!_armed.HasValue) { return; }

            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;

            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                       || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (cancel) { Cancel(); return; }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 screen = mouse.position.ReadValue();

                // Only treat a click as "on the UI" if it is actually inside one of the two
                // button rectangles. Everything else on screen is placeable map. This avoids
                // the Input System IsPointerOverGameObject over-reporting we hit earlier.
                if (IsOverAnyButton(screen)) { return; }
                if (TryGetGroundPoint(screen, out Vector3 world))
                {
                    ActionResult r;
                    switch (_armed.Value)
                    {
                        case PlaceMode.RangedSpawner:
                            r = _bootstrap.TryPlaceRangedSpawner(world);
                            break;
                        case PlaceMode.Tower:
                            r = _bootstrap.TryPlace(StructureType.Defensive, world);
                            break;
                        case PlaceMode.Wall:
                            r = _bootstrap.TryPlaceWall(world);
                            break;
                        default:
                            r = _bootstrap.TryPlace(StructureType.Spawner, world);
                            break;
                    }
                    ShowResult(r);
                }
                else
                {
                    SetStatus($"Could not map click to ground (camera={( _camera != null ? _camera.name : "null")})");
                    Debug.LogWarning("[PlacementUI] ground raycast failed");
                }
            }
        }

        private void SetStatus(string s) { if (_statusText != null) { _statusText.text = s; } }

        private bool IsOverAnyButton(Vector2 screenPos)
        {
            for (int i = 0; i < _buttonRects.Count; i++)
            {
                RectTransform rt = _buttonRects[i];
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetGroundPoint(Vector2 screenPos, out Vector3 world)
        {
            world = default;
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null) { return false; }
            Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
            {
                world = ray.GetPoint(dist);
                return true;
            }
            return false;
        }

        private void ShowResult(ActionResult r)
        {
            if (_statusText == null) { return; }
            if (r == null) { _statusText.text = "Placement failed."; return; }
            string what = _armed.HasValue ? Label(_armed.Value) : "structure";
            _statusText.text = r.Success
                ? $"Placed {what}.  (Resources: {_bootstrap.LocalResources()})"
                : $"Rejected: {r.Reason}";
        }
    }
}







