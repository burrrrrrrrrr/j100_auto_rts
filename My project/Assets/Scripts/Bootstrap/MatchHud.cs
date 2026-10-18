using MultiplayerRts.Client;
using MultiplayerRts.Net;
using MultiplayerRts.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerRts.Bootstrap
{
    /// <summary>
    /// Zero-setup runtime HUD: builds its own Canvas + Text elements in code (no manual UI
    /// authoring) and each frame reads the GameClient mirror's current view to show, per
    /// player, resource total and base health as integers (via the pure NumericDisplayFormatter),
    /// plus a center banner with the match result when it ends. Assign the mirror via
    /// <see cref="Bind"/>.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        private GameClientStateMirror _mirror;

        private Text _leftText;
        private Text _rightText;
        private Text _bannerText;

        public void Bind(GameClientStateMirror mirror)
        {
            _mirror = mirror;
        }

        private void Start()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("HUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _leftText   = MakeText(canvasGo.transform, "LeftHUD",  new Vector2(0f, 1f),   new Vector2(20f, -20f),  TextAnchor.UpperLeft);
            _rightText  = MakeText(canvasGo.transform, "RightHUD", new Vector2(1f, 1f),   new Vector2(-220f, -20f), TextAnchor.UpperRight);
            _bannerText = MakeText(canvasGo.transform, "Banner",   new Vector2(0.5f, 0.5f), new Vector2(-200f, 0f),  TextAnchor.MiddleCenter);
            _bannerText.fontSize = 36;
            _bannerText.text = string.Empty;
        }

        private Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 120f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) { text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void Update()
        {
            if (_mirror == null || _leftText == null) { return; }

            ClientRenderView view = _mirror.View;
            if (view == null) { return; }

            // Lowest id = left player, highest = right (demo convention matching ShapeRenderer).
            PlayerSnapshot? left = null, right = null;
            uint minId = uint.MaxValue, maxId = 0;
            foreach (PlayerSnapshot p in view.Players)
            {
                if (p.Id.Value <= minId) { minId = p.Id.Value; left = p; }
                if (p.Id.Value >= maxId) { maxId = p.Id.Value; right = p; }
            }

            _leftText.text  = left.HasValue  ? Format("P1 (blue)", left.Value)  : "P1: --";
            _rightText.text = right.HasValue ? Format("P2 (red)",  right.Value) : "P2: --";

            _bannerText.text = BannerFor(view);
        }

        private static string Format(string label, PlayerSnapshot p)
        {
            string res = NumericDisplayFormatter.FormatResourceTotal(p.ResourceTotal);
            string hp  = NumericDisplayFormatter.FormatBaseHealth(p.BaseHealth, p.BaseMaxHealth);
            return $"{label}\nResources: {res}\nBase HP: {hp}";
        }

        private static string BannerFor(ClientRenderView view)
        {
            if (view.Phase == MatchPhase.Paused) { return "PAUSED (reconnect window)"; }
            if (!view.Outcome.HasValue) { return string.Empty; }

            MatchOutcomeSnapshot o = view.Outcome.Value;
            switch (o.Kind)
            {
                case MatchOutcomeKind.Winner:
                    return o.Winner.HasValue ? $"WINNER: Player {o.Winner.Value.Value}" : "WINNER";
                case MatchOutcomeKind.Draw:
                    return "DRAW";
                case MatchOutcomeKind.NoContest:
                    return "NO CONTEST";
                default:
                    return "MATCH ENDED";
            }
        }
    }
}

