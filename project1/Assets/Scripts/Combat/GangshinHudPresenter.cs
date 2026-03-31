using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class GangshinHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private GangshinController _gangshinController;

        [Header("Gauge")]
        [SerializeField]
        private Vector2 _gaugePosition = new Vector2(16f, 72f);

        [SerializeField]
        private Vector2 _gaugeSize = new Vector2(260f, 20f);

        [SerializeField]
        private Color _gaugeBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField]
        private Color _gaugeFillColor = new Color(0.95f, 0.8f, 0.2f, 0.95f);

        [SerializeField]
        private Color _readyFillColor = new Color(1f, 0.35f, 0.2f, 0.95f);

        [Header("Overlay")]
        [SerializeField]
        private Color _activeOverlayColor = new Color(0.85f, 0.2f, 0.2f, 0.14f);

        [SerializeField]
        private Color _cooldownOverlayColor = new Color(0.2f, 0.35f, 0.7f, 0.08f);

        [SerializeField]
        private int _fontSize = 18;

        private GUIStyle _labelStyle;
        private Texture2D _pixel;

        private void Awake()
        {
            if (_gangshinController == null)
            {
                _gangshinController = FindGangshinControllerInScene();
            }
        }

        private void OnDestroy()
        {
            if (_pixel == null)
            {
                return;
            }

            Destroy(_pixel);
            _pixel = null;
        }

        private void OnGUI()
        {
            if (_gangshinController == null)
            {
                return;
            }

            EnsureGuiResources();
            DrawOverlay();
            DrawGauge();
        }

        private void DrawOverlay()
        {
            Color overlayColor = Color.clear;
            switch (_gangshinController.CurrentState)
            {
                case GangshinState.Active:
                    overlayColor = _activeOverlayColor;
                    break;
                case GangshinState.Cooldown:
                    overlayColor = _cooldownOverlayColor;
                    break;
            }

            if (overlayColor.a <= 0f)
            {
                return;
            }

            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), overlayColor);
        }

        private void DrawGauge()
        {
            Rect backgroundRect = new Rect(_gaugePosition.x, _gaugePosition.y, _gaugeSize.x, _gaugeSize.y);
            DrawRect(backgroundRect, _gaugeBackgroundColor);

            float fillWidth = backgroundRect.width * _gangshinController.GaugeNormalized;
            if (fillWidth > 0f)
            {
                Color fillColor = _gangshinController.IsReady ? _readyFillColor : _gaugeFillColor;
                DrawRect(new Rect(backgroundRect.x, backgroundRect.y, fillWidth, backgroundRect.height), fillColor);
            }

            string stateLabel = BuildStateLabel();
            GUI.Label(
                new Rect(backgroundRect.x, backgroundRect.y - 24f, backgroundRect.width + 120f, 22f),
                stateLabel,
                _labelStyle);

            GUI.Label(
                new Rect(backgroundRect.x, backgroundRect.y + backgroundRect.height + 4f, backgroundRect.width + 120f, 22f),
                $"Gangshin {Mathf.RoundToInt(_gangshinController.CurrentGauge)}/{Mathf.RoundToInt(_gangshinController.MaxGauge)}",
                _labelStyle);
        }

        private string BuildStateLabel()
        {
            switch (_gangshinController.CurrentState)
            {
                case GangshinState.Ready:
                    return "Gangshin ready - hold or double tap";
                case GangshinState.Active:
                    return $"Gangshin active {Mathf.CeilToInt(_gangshinController.RemainingActiveTime)}s";
                case GangshinState.Cooldown:
                    return $"Gangshin cooldown {Mathf.CeilToInt(_gangshinController.RemainingCooldownTime)}s";
                default:
                    return "Gathering spirit";
            }
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }

        private void EnsureGuiResources()
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(1, 1);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _fontSize
                };
                _labelStyle.normal.textColor = Color.white;
            }
            else
            {
                _labelStyle.fontSize = _fontSize;
            }
        }

        private static GangshinController FindGangshinControllerInScene()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<GangshinController>();
#else
            return FindObjectOfType<GangshinController>();
#endif
        }
    }
}
