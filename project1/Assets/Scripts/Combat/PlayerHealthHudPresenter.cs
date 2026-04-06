using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class PlayerHealthHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private PlayerHealth _playerHealth;

        [SerializeField]
        private GameOverHandler _gameOverHandler;

        [Header("Health Bar")]
        [SerializeField]
        private Vector2 _barPosition = new Vector2(16f, 16f);

        [SerializeField]
        private Vector2 _barSize = new Vector2(220f, 18f);

        [SerializeField]
        private Color _barBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField]
        private Color _barFillColor = new Color(0.85f, 0.2f, 0.2f, 0.95f);

        [SerializeField]
        private Color _barLowHealthColor = new Color(1f, 0.15f, 0.05f, 0.95f);

        [SerializeField, Range(0f, 1f)]
        private float _lowHealthThreshold = 0.3f;

        [Header("Game Over")]
        [SerializeField]
        private int _gameOverFontSize = 36;

        [SerializeField]
        private Color _gameOverOverlayColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Label")]
        [SerializeField]
        private int _fontSize = 16;

        private GUIStyle _labelStyle;
        private GUIStyle _gameOverStyle;
        private Texture2D _pixel;

        private void Awake()
        {
            if (_playerHealth == null)
            {
                _playerHealth = FindPlayerHealth();
            }

            if (_gameOverHandler == null)
            {
                _gameOverHandler = FindGameOverHandler();
            }
        }

        private void OnDestroy()
        {
            if (_pixel != null)
            {
                Destroy(_pixel);
                _pixel = null;
            }
        }

        private void OnGUI()
        {
            EnsureGuiResources();

            if (_playerHealth != null)
            {
                DrawHealthBar();
            }

            if (_gameOverHandler != null && _gameOverHandler.IsGameOver)
            {
                DrawGameOverOverlay();
            }
        }

        private void DrawHealthBar()
        {
            Rect backgroundRect = new Rect(_barPosition.x, _barPosition.y, _barSize.x, _barSize.y);
            DrawRect(backgroundRect, _barBackgroundColor);

            float normalized = _playerHealth.HealthNormalized;
            float fillWidth = backgroundRect.width * normalized;
            if (fillWidth > 0f)
            {
                Color fillColor = normalized <= _lowHealthThreshold ? _barLowHealthColor : _barFillColor;
                DrawRect(new Rect(backgroundRect.x, backgroundRect.y, fillWidth, backgroundRect.height), fillColor);
            }

            string label = $"HP {Mathf.CeilToInt(_playerHealth.CurrentHealth)}/{Mathf.CeilToInt(_playerHealth.MaxHealth)}";
            GUI.Label(
                new Rect(backgroundRect.x, backgroundRect.y + backgroundRect.height + 2f, backgroundRect.width + 80f, 20f),
                label,
                _labelStyle);
        }

        private void DrawGameOverOverlay()
        {
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), _gameOverOverlayColor);

            float labelWidth = 300f;
            float labelHeight = 50f;
            Rect labelRect = new Rect(
                (Screen.width - labelWidth) * 0.5f,
                (Screen.height - labelHeight) * 0.4f,
                labelWidth,
                labelHeight);

            GUI.Label(labelRect, "GAME OVER", _gameOverStyle);
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

            if (_gameOverStyle == null)
            {
                _gameOverStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _gameOverFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _gameOverStyle.normal.textColor = Color.white;
            }
        }

        private static PlayerHealth FindPlayerHealth()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<PlayerHealth>();
#else
            return FindObjectOfType<PlayerHealth>();
#endif
        }

        private static GameOverHandler FindGameOverHandler()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<GameOverHandler>();
#else
            return FindObjectOfType<GameOverHandler>();
#endif
        }
    }
}
