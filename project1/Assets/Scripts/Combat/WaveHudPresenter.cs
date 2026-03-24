using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class WaveHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private WaveCombatDirector _waveCombatDirector;

        [Header("Format")]
        [SerializeField]
        private string _waveLabelFormat = "Wave {0}";

        [SerializeField]
        private string _remainingLabelFormat = "Remaining: {0}";

        [SerializeField]
        private string _completedLabel = "All Waves Cleared";

        [Header("OnGUI")]
        [SerializeField]
        private bool _renderWithOnGui = true;

        [SerializeField]
        private Vector2 _screenOffset = new Vector2(16f, 16f);

        [SerializeField, Min(10)]
        private int _fontSize = 20;

        [SerializeField]
        private Color _fontColor = Color.white;

        private string _waveLabel;
        private string _remainingLabel;
        private GUIStyle _guiStyle;

        private void Awake()
        {
            if (_waveCombatDirector == null)
            {
                _waveCombatDirector = FindWaveDirectorInScene();
            }

            RefreshLabels(0, 0, false);
        }

        private void OnEnable()
        {
            if (_waveCombatDirector == null)
            {
                return;
            }

            _waveCombatDirector.OnWaveStarted += HandleWaveStarted;
            _waveCombatDirector.OnWaveEnded += HandleWaveEnded;
            _waveCombatDirector.OnRemainingEnemyCountChanged += HandleRemainingEnemyCountChanged;
            _waveCombatDirector.OnAllWavesCompleted += HandleAllWavesCompleted;

            RefreshLabels(
                _waveCombatDirector.CurrentWaveNumber,
                _waveCombatDirector.RemainingEnemyCount,
                _waveCombatDirector.IsRunning);
        }

        private void OnDisable()
        {
            if (_waveCombatDirector == null)
            {
                return;
            }

            _waveCombatDirector.OnWaveStarted -= HandleWaveStarted;
            _waveCombatDirector.OnWaveEnded -= HandleWaveEnded;
            _waveCombatDirector.OnRemainingEnemyCountChanged -= HandleRemainingEnemyCountChanged;
            _waveCombatDirector.OnAllWavesCompleted -= HandleAllWavesCompleted;
        }

        private void OnGUI()
        {
            if (!_renderWithOnGui)
            {
                return;
            }

            EnsureGuiStyle();

            Rect waveRect = new Rect(_screenOffset.x, _screenOffset.y, 420f, 28f);
            Rect remainingRect = new Rect(_screenOffset.x, _screenOffset.y + 28f, 420f, 28f);

            GUI.Label(waveRect, _waveLabel, _guiStyle);
            GUI.Label(remainingRect, _remainingLabel, _guiStyle);
        }

        private void HandleWaveStarted(int waveNumber, WaveDefinition wave)
        {
            SetWaveLabel(waveNumber, true);
            SetRemainingLabel(_waveCombatDirector.RemainingEnemyCount);
        }

        private void HandleWaveEnded(int waveNumber, WaveEndReason endReason)
        {
            SetWaveLabel(waveNumber, true);
            SetRemainingLabel(_waveCombatDirector.RemainingEnemyCount);
        }

        private void HandleRemainingEnemyCountChanged(int waveNumber, int remainingEnemyCount)
        {
            SetWaveLabel(waveNumber, true);
            SetRemainingLabel(remainingEnemyCount);
        }

        private void HandleAllWavesCompleted()
        {
            _waveLabel = _completedLabel;
            SetRemainingLabel(0);
        }

        private void RefreshLabels(int waveNumber, int remainingEnemyCount, bool isRunning)
        {
            SetWaveLabel(waveNumber, isRunning);
            SetRemainingLabel(remainingEnemyCount);
        }

        private void SetWaveLabel(int waveNumber, bool isRunning)
        {
            if (!isRunning || waveNumber <= 0)
            {
                _waveLabel = string.Format(_waveLabelFormat, 0);
                return;
            }

            _waveLabel = string.Format(_waveLabelFormat, waveNumber);
        }

        private void SetRemainingLabel(int remainingEnemyCount)
        {
            _remainingLabel = string.Format(_remainingLabelFormat, Mathf.Max(0, remainingEnemyCount));
        }

        private void EnsureGuiStyle()
        {
            if (_guiStyle != null)
            {
                _guiStyle.fontSize = _fontSize;
                _guiStyle.normal.textColor = _fontColor;
                return;
            }

            _guiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize
            };
            _guiStyle.normal.textColor = _fontColor;
        }

        private static WaveCombatDirector FindWaveDirectorInScene()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<WaveCombatDirector>();
#else
            return FindObjectOfType<WaveCombatDirector>();
#endif
        }
    }
}
