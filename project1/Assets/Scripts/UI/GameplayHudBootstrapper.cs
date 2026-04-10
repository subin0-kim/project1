using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mukseon.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class GameplayHudBootstrapper : MonoBehaviour
    {
        private const string RootObjectName = "GameplayHudRuntime";

        private PlayerHealth _playerHealth;
        private GangshinController _gangshinController;
        private PlayerLevelSystem _playerLevelSystem;
        private WaveCombatDirector _waveCombatDirector;

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private VisualElement _root;

        private VisualElement _healthRoot;
        private VisualElement _healthFill;
        private Label _healthLabel;

        private VisualElement _gangshinRoot;
        private VisualElement _gangshinFill;
        private Label _gangshinStateLabel;
        private Label _gangshinGaugeLabel;

        private VisualElement _experienceRoot;
        private VisualElement _experienceFill;
        private Label _experienceLabel;

        private VisualElement _waveRoot;
        private Label _waveLabel;
        private Label _remainingLabel;

        private VisualElement _overlay;
        private VisualElement _levelUpPanel;
        private Label _levelUpTitleLabel;
        private readonly List<Button> _choiceButtons = new List<Button>(3);

        private bool _uiBuilt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHudBootstrapper()
        {
            GameplayHudBootstrapper existing = FindBootstrapper();
            if (existing != null)
            {
                existing.HandleSceneLoaded();
                return;
            }

            var root = new GameObject(RootObjectName);
            DontDestroyOnLoad(root);
            root.AddComponent<GameplayHudBootstrapper>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureUi();
            HandleSceneLoaded();
        }

        private void OnEnable()
        {
            TryResolveSources();
        }

        private void Update()
        {
            if (_playerHealth == null || _gangshinController == null || _playerLevelSystem == null || _waveCombatDirector == null)
            {
                TryResolveSources();
            }

            RefreshView();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeAll();

            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
            }
        }

        private static GameplayHudBootstrapper FindBootstrapper()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<GameplayHudBootstrapper>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<GameplayHudBootstrapper>();
#endif
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneLoaded();
        }

        private void HandleSceneLoaded()
        {
            UnsubscribeAll();
            _playerHealth = null;
            _gangshinController = null;
            _playerLevelSystem = null;
            _waveCombatDirector = null;

            EnsureUi();
            TryResolveSources();
            RefreshView();
        }

        private void EnsureUi()
        {
            if (_uiBuilt)
            {
                return;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 500;
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");

            _uiDocument = gameObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            _root = _uiDocument.rootVisualElement;
            _root.style.flexGrow = 1f;
            _root.style.backgroundColor = Color.clear;

            _overlay = CreateBox(_root, "overlay");
            _overlay.style.left = 0f;
            _overlay.style.top = 0f;
            _overlay.style.right = 0f;
            _overlay.style.bottom = 0f;
            _overlay.style.display = DisplayStyle.None;

            CreateHealthHud();
            CreateBottomHud();
            CreateWaveHud();
            CreateLevelUpPanel();

            _uiBuilt = true;
        }

        private void TryResolveSources()
        {
            bool changed = false;

            if (_playerHealth == null)
            {
                _playerHealth = FindSceneObject<PlayerHealth>();
                if (_playerHealth != null)
                {
                    _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
                    DisableLegacyPresenter<PlayerHealthHudPresenter>();
                    changed = true;
                }
            }

            if (_gangshinController == null)
            {
                _gangshinController = FindSceneObject<GangshinController>();
                if (_gangshinController != null)
                {
                    _gangshinController.OnGaugeChanged += HandleGangshinGaugeChanged;
                    _gangshinController.OnStateChanged += HandleGangshinStateChanged;
                    DisableLegacyPresenter<GangshinHudPresenter>();
                    changed = true;
                }
            }

            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = FindSceneObject<PlayerLevelSystem>();
                if (_playerLevelSystem != null)
                {
                    _playerLevelSystem.OnExperienceChanged += HandleExperienceChanged;
                    _playerLevelSystem.OnLevelSelectionOpened += HandleLevelSelectionOpened;
                    _playerLevelSystem.OnLevelSelectionClosed += HandleLevelSelectionClosed;
                    DisableLegacyPresenter<LevelUpPanelPresenter>();
                    changed = true;
                }
            }

            if (_waveCombatDirector == null)
            {
                _waveCombatDirector = FindSceneObject<WaveCombatDirector>();
                if (_waveCombatDirector != null)
                {
                    _waveCombatDirector.OnWaveStarted += HandleWaveStarted;
                    _waveCombatDirector.OnWaveEnded += HandleWaveEnded;
                    _waveCombatDirector.OnRemainingEnemyCountChanged += HandleRemainingChanged;
                    _waveCombatDirector.OnAllWavesCompleted += HandleAllWavesCompleted;
                    DisableLegacyPresenter<WaveHudPresenter>();
                    changed = true;
                }
            }

            if (changed)
            {
                RefreshView();
            }
        }

        private void UnsubscribeAll()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            }

            if (_gangshinController != null)
            {
                _gangshinController.OnGaugeChanged -= HandleGangshinGaugeChanged;
                _gangshinController.OnStateChanged -= HandleGangshinStateChanged;
            }

            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnExperienceChanged -= HandleExperienceChanged;
                _playerLevelSystem.OnLevelSelectionOpened -= HandleLevelSelectionOpened;
                _playerLevelSystem.OnLevelSelectionClosed -= HandleLevelSelectionClosed;
            }

            if (_waveCombatDirector != null)
            {
                _waveCombatDirector.OnWaveStarted -= HandleWaveStarted;
                _waveCombatDirector.OnWaveEnded -= HandleWaveEnded;
                _waveCombatDirector.OnRemainingEnemyCountChanged -= HandleRemainingChanged;
                _waveCombatDirector.OnAllWavesCompleted -= HandleAllWavesCompleted;
            }
        }

        private void RefreshView()
        {
            RefreshHealthHud();
            RefreshGangshinHud();
            RefreshExperienceHud();
            RefreshWaveHud();
            RefreshLevelUpPanel();
        }

        private void RefreshHealthHud()
        {
            bool isVisible = _playerHealth != null;
            _healthRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isVisible)
            {
                return;
            }

            SetFillWidth(_healthFill, _playerHealth.HealthNormalized);
            _healthFill.style.backgroundColor = _playerHealth.HealthNormalized <= 0.3f
                ? new Color(0.95f, 0.24f, 0.2f)
                : new Color(0.82f, 0.22f, 0.22f);
            _healthLabel.text = $"HP {Mathf.CeilToInt(_playerHealth.CurrentHealth)}/{Mathf.CeilToInt(_playerHealth.MaxHealth)}";
        }

        private void RefreshGangshinHud()
        {
            bool isVisible = _gangshinController != null;
            _gangshinRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isVisible)
            {
                _overlay.style.display = DisplayStyle.None;
                return;
            }

            SetFillWidth(_gangshinFill, _gangshinController.GaugeNormalized);
            _gangshinFill.style.backgroundColor = _gangshinController.IsReady
                ? new Color(1f, 0.47f, 0.22f)
                : new Color(0.96f, 0.82f, 0.28f);

            switch (_gangshinController.CurrentState)
            {
                case GangshinState.Ready:
                    _gangshinStateLabel.text = "Gangshin ready";
                    _overlay.style.display = DisplayStyle.None;
                    break;
                case GangshinState.Active:
                    _gangshinStateLabel.text = $"Gangshin active {Mathf.CeilToInt(_gangshinController.RemainingActiveTime)}s";
                    _overlay.style.display = DisplayStyle.Flex;
                    _overlay.style.backgroundColor = new Color(0.8f, 0.15f, 0.15f, 0.12f);
                    break;
                case GangshinState.Cooldown:
                    _gangshinStateLabel.text = $"Gangshin cooldown {Mathf.CeilToInt(_gangshinController.RemainingCooldownTime)}s";
                    _overlay.style.display = DisplayStyle.Flex;
                    _overlay.style.backgroundColor = new Color(0.18f, 0.36f, 0.8f, 0.08f);
                    break;
                default:
                    _gangshinStateLabel.text = "Gathering spirit";
                    _overlay.style.display = DisplayStyle.None;
                    break;
            }

            _gangshinGaugeLabel.text =
                $"Gangshin {Mathf.RoundToInt(_gangshinController.CurrentGauge)}/{Mathf.RoundToInt(_gangshinController.MaxGauge)}";
        }

        private void RefreshExperienceHud()
        {
            bool isVisible = _playerLevelSystem != null;
            _experienceRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isVisible)
            {
                return;
            }

            float threshold = Mathf.Max(1f, _playerLevelSystem.CurrentThreshold);
            SetFillWidth(_experienceFill, _playerLevelSystem.CurrentExperience / threshold);
            _experienceLabel.text =
                $"Lv {_playerLevelSystem.CurrentLevel}  EXP {_playerLevelSystem.CurrentExperience:0.##}/{threshold:0.##}";
        }

        private void RefreshWaveHud()
        {
            bool isVisible = _waveCombatDirector != null;
            _waveRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isVisible)
            {
                return;
            }

            if (!_waveCombatDirector.IsRunning)
            {
                _waveLabel.text = "Wave 0";
                _remainingLabel.text = "Remaining: 0";
                return;
            }

            _waveLabel.text = $"Wave {_waveCombatDirector.CurrentWaveNumber}";
            _remainingLabel.text = $"Remaining: {_waveCombatDirector.RemainingEnemyCount}";
        }

        private void RefreshLevelUpPanel()
        {
            bool isVisible = _playerLevelSystem != null && _playerLevelSystem.IsSelectionOpen && _playerLevelSystem.CurrentChoices.Count > 0;
            _levelUpPanel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isVisible)
            {
                return;
            }

            _levelUpTitleLabel.text = $"Level {_playerLevelSystem.CurrentLevel} - choose a skill";

            IReadOnlyList<SkillData> choices = _playerLevelSystem.CurrentChoices;
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Button button = _choiceButtons[i];
                bool hasChoice = i < choices.Count;
                button.style.display = hasChoice ? DisplayStyle.Flex : DisplayStyle.None;
                if (!hasChoice)
                {
                    continue;
                }

                SkillData choice = choices[i];
                int nextLevel = _playerLevelSystem.GetSkillLevel(choice.SkillId) + 1;
                button.text = $"{choice.DisplayName} Lv.{nextLevel}\n{choice.Description}";
            }
        }

        private void HandlePlayerHealthChanged(float current, float max) => RefreshHealthHud();
        private void HandleGangshinGaugeChanged(float current, float max) => RefreshGangshinHud();
        private void HandleGangshinStateChanged(GangshinState state) => RefreshGangshinHud();
        private void HandleExperienceChanged(int level, float current, float threshold) => RefreshExperienceHud();
        private void HandleLevelSelectionOpened(int level, IReadOnlyList<SkillData> choices) => RefreshLevelUpPanel();
        private void HandleLevelSelectionClosed(int level) => RefreshLevelUpPanel();
        private void HandleWaveStarted(int waveNumber, WaveDefinition wave) => RefreshWaveHud();
        private void HandleWaveEnded(int waveNumber, WaveEndReason endReason) => RefreshWaveHud();
        private void HandleRemainingChanged(int waveNumber, int remainingEnemyCount) => RefreshWaveHud();

        private void HandleAllWavesCompleted()
        {
            _waveLabel.text = "All Waves Cleared";
            _remainingLabel.text = "Remaining: 0";
        }

        private void CreateHealthHud()
        {
            _healthRoot = CreatePanel(_root, 16f, 16f, 240f, 52f);
            CreateBar(_healthRoot, out _healthFill, out _healthLabel, false);
        }

        private void CreateBottomHud()
        {
            _gangshinRoot = CreatePanel(_root, 790f, 912f, 340f, 70f);
            _gangshinStateLabel = CreateLabel(_gangshinRoot, 0f, 4f, 340f, 18f, 18, TextAnchor.MiddleCenter);
            CreateBar(_gangshinRoot, out _gangshinFill, out _gangshinGaugeLabel, true);

            _experienceRoot = CreatePanel(_root, 760f, 972f, 400f, 52f);
            CreateBar(_experienceRoot, out _experienceFill, out _experienceLabel, true);
        }

        private void CreateWaveHud()
        {
            _waveRoot = CreatePanel(_root, 790f, 16f, 340f, 54f);
            _waveLabel = CreateLabel(_waveRoot, 0f, 4f, 340f, 22f, 22, TextAnchor.MiddleCenter);
            _remainingLabel = CreateLabel(_waveRoot, 0f, 28f, 340f, 18f, 16, TextAnchor.MiddleCenter);
        }

        private void CreateLevelUpPanel()
        {
            _levelUpPanel = CreatePanel(_root, 680f, 360f, 560f, 360f);
            _levelUpPanel.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.94f);
            _levelUpTitleLabel = CreateLabel(_levelUpPanel, 24f, 24f, 512f, 28f, 24, TextAnchor.MiddleCenter);

            _choiceButtons.Clear();
            for (int i = 0; i < 3; i++)
            {
                int choiceIndex = i;
                Button button = new Button(() =>
                {
                    if (_playerLevelSystem != null)
                    {
                        _playerLevelSystem.ApplyChoice(choiceIndex);
                    }
                });

                button.style.position = Position.Absolute;
                button.style.left = 24f;
                button.style.top = 72f + (84f * i);
                button.style.width = 512f;
                button.style.height = 68f;
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.fontSize = 18f;
                button.style.backgroundColor = new Color(0.14f, 0.16f, 0.24f, 0.96f);
                button.style.color = Color.white;
                _levelUpPanel.Add(button);
                _choiceButtons.Add(button);
            }

            _levelUpPanel.style.display = DisplayStyle.None;
        }

        private void CreateBar(VisualElement parent, out VisualElement fill, out Label label, bool centered)
        {
            var barRoot = new VisualElement();
            barRoot.style.position = Position.Absolute;
            barRoot.style.left = 0f;
            barRoot.style.right = 0f;
            barRoot.style.bottom = 8f;
            barRoot.style.height = 20f;
            barRoot.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            parent.Add(barRoot);

            fill = new VisualElement();
            fill.style.height = Length.Percent(100f);
            fill.style.width = Length.Percent(100f);
            fill.style.backgroundColor = new Color(0.82f, 0.22f, 0.22f);
            barRoot.Add(fill);

            label = new Label();
            label.style.position = Position.Absolute;
            label.style.left = 8f;
            label.style.right = 8f;
            label.style.top = 0f;
            label.style.bottom = 0f;
            label.style.color = Color.white;
            label.style.fontSize = 16f;
            label.style.unityTextAlign = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.Normal;
            barRoot.Add(label);
        }

        private static VisualElement CreatePanel(VisualElement parent, float left, float top, float width, float height)
        {
            var panel = CreateBox(parent, "panel");
            panel.style.left = left;
            panel.style.top = top;
            panel.style.width = width;
            panel.style.height = height;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            return panel;
        }

        private static VisualElement CreateBox(VisualElement parent, string name)
        {
            var element = new VisualElement
            {
                name = name
            };
            element.style.position = Position.Absolute;
            parent.Add(element);
            return element;
        }

        private static Label CreateLabel(
            VisualElement parent,
            float left,
            float top,
            float width,
            float height,
            int fontSize,
            TextAnchor alignment)
        {
            var label = new Label();
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            label.style.width = width;
            label.style.height = height;
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = alignment;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        private static void SetFillWidth(VisualElement fill, float normalized)
        {
            if (fill == null)
            {
                return;
            }

            fill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
        }

        private static void DisableLegacyPresenter<T>() where T : Behaviour
        {
            T presenter = FindSceneObject<T>();
            if (presenter != null && presenter.enabled)
            {
                presenter.enabled = false;
            }
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}
