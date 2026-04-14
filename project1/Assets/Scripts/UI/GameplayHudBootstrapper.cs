using System;
using System.Collections.Generic;
using Mukseon.Core.Input;
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
        private sealed class FloatingText
        {
            public EnemyHealth Enemy;
            public Label Label;
            public float TimeLeft;
            public float OffsetY;
        }

        private readonly struct CardSlot
        {
            public readonly Button Button;
            public readonly VisualElement Icon;
            public readonly Label NameLabel;
            public readonly Label DescLabel;

            public CardSlot(Button button, VisualElement icon, Label nameLabel, Label descLabel)
                => (Button, Icon, NameLabel, DescLabel) = (button, icon, nameLabel, descLabel);
        }

        private static class Strings
        {
            public const string LevelUpTitle = "레벨 업! 스킬을 선택하세요";
        }

        private const string RootObjectName = "GameplayHudRuntime";

        private PlayerHealth _playerHealth;
        private GangshinController _gangshinController;
        private PlayerLevelSystem _playerLevelSystem;
        private WaveCombatDirector _waveCombatDirector;
        private EnemyHealth _bossEnemy;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _worldRoot;

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
        private VisualElement _bossRoot;
        private VisualElement _bossFill;
        private Label _bossLabel;
        private VisualElement _levelUpContainer;
        private VisualElement _levelUpPanel;
        private Label _levelUpTitle;
        private readonly List<CardSlot> _cardSlots = new List<CardSlot>(3);

        private sealed class SequenceHud
        {
            public VisualElement Container;
            public Label[] ArrowLabels = new Label[3];
            public Label EllipsisLabel;
            public Action<int> AdvancedHandler;
            public Action SequenceSetHandler;
        }

        private readonly HashSet<EnemyHealth> _trackedEnemies = new HashSet<EnemyHealth>();
        private readonly Dictionary<EnemyHealth, SequenceHud> _sequenceHuds = new Dictionary<EnemyHealth, SequenceHud>();
        private readonly List<FloatingText> _floatingTexts = new List<FloatingText>();
        private readonly List<EnemyHealth> _enemyBuffer = new List<EnemyHealth>(64);
        private readonly List<EnemyHealth> _removedEnemyBuffer = new List<EnemyHealth>(64);
        private float _resolveRetryTimer;
        private Camera _cachedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHudBootstrapper()
        {
            GameplayHudBootstrapper existing = FindBootstrapper();
            if (existing != null)
            {
                return;
            }

            GameObject root = new GameObject(RootObjectName);
            root.AddComponent<GameplayHudBootstrapper>();
        }

        private static GameplayHudBootstrapper FindBootstrapper()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<GameplayHudBootstrapper>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<GameplayHudBootstrapper>();
#endif
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<T>();
#endif
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureUi();
            HandleSceneLoaded();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeAll();
            ClearEnemies();

            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
            }
        }

        private void Update()
        {
            _resolveRetryTimer -= Time.unscaledDeltaTime;
            if (_resolveRetryTimer <= 0f && NeedsRuntimeReferenceRefresh())
            {
                _resolveRetryTimer = 0.5f;
                TryResolveSources();
            }

            SyncEnemies();

            if (_gangshinController != null &&
                (_gangshinController.CurrentState == GangshinState.Active || _gangshinController.CurrentState == GangshinState.Cooldown))
            {
                RefreshGangshin();
            }

            UpdateWorldElements(Time.unscaledDeltaTime);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneLoaded();
        }

        private void HandleSceneLoaded()
        {
            UnsubscribeAll();
            ClearEnemies();
            _playerHealth = null;
            _gangshinController = null;
            _playerLevelSystem = null;
            _waveCombatDirector = null;
            _bossEnemy = null;
            _cachedCamera = Camera.main;
            _resolveRetryTimer = 0f;
            EnsureUi();
            TryResolveSources();
        }

        private void EnsureUi()
        {
            if (_document != null)
            {
                return;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 500;
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _root = _document.rootVisualElement;
            _root.style.flexGrow = 1f;

            _overlay = Box(_root);
            Stretch(_overlay);
            _overlay.style.display = DisplayStyle.None;

            _worldRoot = Box(_root);
            Stretch(_worldRoot);

            _healthRoot = Panel(_root, 16f, 16f, 240f, 52f);
            _healthFill = Bar(_healthRoot, out _healthLabel, false);

            _gangshinRoot = Panel(_root, 790f, 912f, 340f, 70f);
            _gangshinStateLabel = Text(_gangshinRoot, 0f, 4f, 340f, 18f, 18, TextAnchor.MiddleCenter);
            _gangshinFill = Bar(_gangshinRoot, out _gangshinGaugeLabel, true);

            _experienceRoot = Panel(_root, 760f, 972f, 400f, 52f);
            _experienceFill = Bar(_experienceRoot, out _experienceLabel, true);

            _waveRoot = Panel(_root, 790f, 16f, 340f, 54f);
            _waveLabel = Text(_waveRoot, 0f, 4f, 340f, 22f, 22, TextAnchor.MiddleCenter);
            _remainingLabel = Text(_waveRoot, 0f, 28f, 340f, 18f, 16, TextAnchor.MiddleCenter);

            _bossRoot = Panel(_root, 360f, 74f, 1200f, 62f);
            _bossRoot.style.backgroundColor = new Color(0.18f, 0.04f, 0.04f, 0.78f);
            _bossLabel = Text(_bossRoot, 0f, 4f, 1200f, 18f, 20, TextAnchor.MiddleCenter);
            _bossFill = Bar(_bossRoot, out _, true);
            _bossFill.style.backgroundColor = new Color(0.88f, 0.12f, 0.12f);
            _bossRoot.style.display = DisplayStyle.None;

            const float panelW = 580f;
            const float panelH = 440f;

            _levelUpContainer = new VisualElement();
            _levelUpContainer.style.position = Position.Absolute;
            Stretch(_levelUpContainer);
            _levelUpContainer.style.justifyContent = Justify.Center;
            _levelUpContainer.style.alignItems = Align.Center;
            _levelUpContainer.style.display = DisplayStyle.None;
            _root.Add(_levelUpContainer);

            _levelUpPanel = new VisualElement();
            _levelUpPanel.style.width = panelW;
            _levelUpPanel.style.height = panelH;
            _levelUpPanel.style.backgroundColor = new Color(0.06f, 0.06f, 0.10f, 0.96f);
            _levelUpContainer.Add(_levelUpPanel);

            _levelUpTitle = Text(_levelUpPanel, 16f, 14f, panelW - 32f, 30f, 22, TextAnchor.MiddleCenter);

            VisualElement cardsContainer = new VisualElement();
            cardsContainer.style.position = Position.Absolute;
            cardsContainer.style.left = 16f;
            cardsContainer.style.right = 16f;
            // titleY(14) + titleHeight(30) + gap(12) = 56
            const float cardsTop = 14f + 30f + 12f;
            cardsContainer.style.top = cardsTop;
            cardsContainer.style.bottom = 16f;
            cardsContainer.style.flexDirection = FlexDirection.Column;
            cardsContainer.style.justifyContent = Justify.SpaceBetween;
            _levelUpPanel.Add(cardsContainer);

            for (int i = 0; i < 3; i++)
            {
                int choiceIndex = i;
                Button card = new Button(() =>
                {
                    if (_playerLevelSystem != null)
                    {
                        _playerLevelSystem.ApplyChoice(choiceIndex);
                    }
                });

                card.text = string.Empty;
                card.style.height = 108f;
                card.style.backgroundColor = new Color(0.12f, 0.14f, 0.22f, 0.98f);
                card.style.color = Color.white;
                card.style.paddingTop = 0f;
                card.style.paddingBottom = 0f;
                card.style.paddingLeft = 0f;
                card.style.paddingRight = 0f;
                card.style.borderTopLeftRadius = 6f;
                card.style.borderTopRightRadius = 6f;
                card.style.borderBottomLeftRadius = 6f;
                card.style.borderBottomRightRadius = 6f;
                cardsContainer.Add(card);

                // 스킬 아이콘
                VisualElement icon = new VisualElement();
                icon.style.position = Position.Absolute;
                icon.style.left = 14f;
                icon.style.top = 14f;
                icon.style.width = 80f;
                icon.style.height = 80f;
                icon.style.backgroundColor = new Color(0.20f, 0.22f, 0.32f, 1f);
                icon.style.borderTopLeftRadius = 4f;
                icon.style.borderTopRightRadius = 4f;
                icon.style.borderBottomLeftRadius = 4f;
                icon.style.borderBottomRightRadius = 4f;
                card.Add(icon);

                // 스킬 이름 + 레벨
                Label nameLabel = new Label();
                nameLabel.style.position = Position.Absolute;
                nameLabel.style.left = 108f;
                nameLabel.style.top = 10f;
                nameLabel.style.width = 418f;
                nameLabel.style.height = 28f;
                nameLabel.style.color = Color.white;
                nameLabel.style.fontSize = 19f;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                card.Add(nameLabel);

                // 스킬 설명
                Label descLabel = new Label();
                descLabel.style.position = Position.Absolute;
                descLabel.style.left = 108f;
                descLabel.style.top = 44f;
                descLabel.style.width = 418f;
                descLabel.style.height = 56f;
                descLabel.style.color = new Color(0.78f, 0.78f, 0.84f, 1f);
                descLabel.style.fontSize = 13f;
                descLabel.style.unityTextAlign = TextAnchor.UpperLeft;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                card.Add(descLabel);

                _cardSlots.Add(new CardSlot(card, icon, nameLabel, descLabel));
            }
        }

        private void TryResolveSources()
        {
            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
                if (_cachedCamera == null)
                {
                    Debug.LogWarning("[GameplayHudBootstrapper] Camera.main을 찾을 수 없습니다. 월드 UI 위치 계산이 비활성화됩니다.");
                }
            }

            if (_playerHealth == null)
            {
                _playerHealth = FindSceneObject<PlayerHealth>();
                if (_playerHealth != null)
                {
                    _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
                    RefreshHealth();
                }
                else
                {
                    Debug.LogWarning("[GameplayHudBootstrapper] PlayerHealth를 찾을 수 없습니다. 체력 HUD가 표시되지 않습니다.");
                }
            }

            if (_gangshinController == null)
            {
                _gangshinController = FindSceneObject<GangshinController>();
                if (_gangshinController != null)
                {
                    _gangshinController.OnGaugeChanged += HandleGangshinGaugeChanged;
                    _gangshinController.OnStateChanged += HandleGangshinStateChanged;
                    RefreshGangshin();
                }
                else
                {
                    Debug.LogWarning("[GameplayHudBootstrapper] GangshinController를 찾을 수 없습니다. 강신 게이지 HUD가 표시되지 않습니다.");
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
                    RefreshExperience();
                    RefreshLevelUp();
                }
                else
                {
                    Debug.LogWarning("[GameplayHudBootstrapper] PlayerLevelSystem을 찾을 수 없습니다. 경험치 HUD 및 레벨업 패널이 표시되지 않습니다.");
                }
            }

            if (_waveCombatDirector == null)
            {
                _waveCombatDirector = FindSceneObject<WaveCombatDirector>();
                if (_waveCombatDirector != null)
                {
                    _waveCombatDirector.OnWaveStarted += HandleWaveStarted;
                    _waveCombatDirector.OnWaveEnded += HandleWaveEnded;
                    _waveCombatDirector.OnRemainingEnemyCountChanged += HandleRemainingEnemyCountChanged;
                    _waveCombatDirector.OnAllWavesCompleted += HandleAllWavesCompleted;
                    RefreshWave();
                }
                else
                {
                    Debug.LogWarning("[GameplayHudBootstrapper] WaveCombatDirector를 찾을 수 없습니다. 웨이브 HUD가 표시되지 않습니다.");
                }
            }
        }

        private bool NeedsRuntimeReferenceRefresh()
        {
            return _cachedCamera == null ||
                   _playerHealth == null ||
                   _gangshinController == null ||
                   _playerLevelSystem == null ||
                   _waveCombatDirector == null;
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
                _waveCombatDirector.OnRemainingEnemyCountChanged -= HandleRemainingEnemyCountChanged;
                _waveCombatDirector.OnAllWavesCompleted -= HandleAllWavesCompleted;
            }
        }

        private void RefreshHud()
        {
            RefreshHealth();
            RefreshGangshin();
            RefreshExperience();
            RefreshWave();
            RefreshBoss();
            RefreshLevelUp();
        }

        private void RefreshHealth()
        {
            bool visible = _playerHealth != null;
            _healthRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            Fill(_healthFill, _playerHealth.HealthNormalized);
            _healthFill.style.backgroundColor = _playerHealth.HealthNormalized <= 0.3f ? new Color(0.95f, 0.24f, 0.2f) : new Color(0.82f, 0.22f, 0.22f);
            _healthLabel.text = $"HP {Mathf.CeilToInt(_playerHealth.CurrentHealth)}/{Mathf.CeilToInt(_playerHealth.MaxHealth)}";
        }

        private void RefreshGangshin()
        {
            bool visible = _gangshinController != null;
            _gangshinRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                _overlay.style.display = DisplayStyle.None;
                return;
            }

            Fill(_gangshinFill, _gangshinController.GaugeNormalized);
            _gangshinFill.style.backgroundColor = _gangshinController.IsReady ? new Color(1f, 0.47f, 0.22f) : new Color(0.96f, 0.82f, 0.28f);
            _gangshinGaugeLabel.text = $"Gangshin {Mathf.RoundToInt(_gangshinController.CurrentGauge)}/{Mathf.RoundToInt(_gangshinController.MaxGauge)}";

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
        }

        private void RefreshExperience()
        {
            bool visible = _playerLevelSystem != null;
            _experienceRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            float threshold = Mathf.Max(1f, _playerLevelSystem.CurrentThreshold);
            Fill(_experienceFill, _playerLevelSystem.CurrentExperience / threshold);
            _experienceLabel.text = $"Lv {_playerLevelSystem.CurrentLevel}  EXP {_playerLevelSystem.CurrentExperience:0.##}/{threshold:0.##}";
        }

        private void RefreshWave()
        {
            bool visible = _waveCombatDirector != null;
            _waveRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            _waveLabel.text = _waveCombatDirector.IsRunning ? $"Wave {_waveCombatDirector.CurrentWaveNumber}" : "Wave 0";
            _remainingLabel.text = $"Remaining: {(_waveCombatDirector.IsRunning ? _waveCombatDirector.RemainingEnemyCount : 0)}";
        }

        private void RefreshBoss()
        {
            ResolveBossEnemy();
            bool visible = _bossEnemy != null && _bossEnemy.IsAlive;
            _bossRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            _bossLabel.text = _bossEnemy.DisplayName;
            Fill(_bossFill, _bossEnemy.CurrentHealth / Mathf.Max(1f, _bossEnemy.MaxHealth));
        }

        private void RefreshLevelUp()
        {
            bool visible = _playerLevelSystem != null && _playerLevelSystem.IsSelectionOpen && _playerLevelSystem.CurrentChoices.Count > 0;
            _levelUpContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            _levelUpTitle.text = $"{Strings.LevelUpTitle} (Lv.{_playerLevelSystem.CurrentLevel})";
            IReadOnlyList<SkillData> choices = _playerLevelSystem.CurrentChoices;
            for (int i = 0; i < _cardSlots.Count; i++)
            {
                CardSlot slot = _cardSlots[i];
                bool hasChoice = i < choices.Count;
                slot.Button.style.display = hasChoice ? DisplayStyle.Flex : DisplayStyle.None;
                if (!hasChoice)
                {
                    continue;
                }

                SkillData choice = choices[i];
                int currentLevel = _playerLevelSystem.GetSkillLevel(choice.SkillId);
                int nextLevel = currentLevel + 1;

                // 아이콘
                if (choice.Icon != null)
                {
                    slot.Icon.style.backgroundImage = new StyleBackground(choice.Icon);
                }
                else
                {
                    slot.Icon.style.backgroundImage = StyleKeyword.None;
                }

                // 이름 + 레벨
                string levelText = currentLevel > 0 ? $"  Lv.{currentLevel} → {nextLevel}" : $"  Lv.{nextLevel}";
                slot.NameLabel.text = choice.DisplayName + levelText;

                // 설명
                slot.DescLabel.text = choice.Description;
            }
        }

        private void SyncEnemies()
        {
            _enemyBuffer.Clear();
            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                _enemyBuffer.Add(enemy);
                if (_trackedEnemies.Add(enemy))
                {
                    enemy.OnDamagedDetailed += HandleEnemyDamaged;
                    enemy.OnDeath += HandleEnemyDeath;
                    CreateSequenceHud(enemy);
                    if (enemy.IsBoss)
                    {
                        _bossEnemy = enemy;
                        RefreshBoss();
                    }
                }
            }

            _removedEnemyBuffer.Clear();
            foreach (EnemyHealth enemy in _trackedEnemies)
            {
                if (!_enemyBuffer.Contains(enemy))
                {
                    _removedEnemyBuffer.Add(enemy);
                }
            }

            for (int i = 0; i < _removedEnemyBuffer.Count; i++)
            {
                RemoveEnemy(_removedEnemyBuffer[i]);
            }
        }

        private void ClearEnemies()
        {
            _removedEnemyBuffer.Clear();
            foreach (EnemyHealth enemy in _trackedEnemies)
            {
                _removedEnemyBuffer.Add(enemy);
            }

            for (int i = 0; i < _removedEnemyBuffer.Count; i++)
            {
                RemoveEnemy(_removedEnemyBuffer[i]);
            }

            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                _floatingTexts[i].Label.RemoveFromHierarchy();
                _floatingTexts.RemoveAt(i);
            }
        }

        private void RemoveEnemy(EnemyHealth enemy)
        {
            if (enemy != null)
            {
                enemy.OnDamagedDetailed -= HandleEnemyDamaged;
                enemy.OnDeath -= HandleEnemyDeath;
            }

            _trackedEnemies.Remove(enemy);

            if (enemy != null && _sequenceHuds.TryGetValue(enemy, out SequenceHud hud))
            {
                EnemyAttackSequence seq = enemy.AttackSequence;
                if (seq != null)
                {
                    if (hud.AdvancedHandler != null)
                    {
                        seq.OnAdvanced -= hud.AdvancedHandler;
                    }

                    if (hud.SequenceSetHandler != null)
                    {
                        seq.OnSequenceSet -= hud.SequenceSetHandler;
                    }
                }

                hud.Container.RemoveFromHierarchy();
                _sequenceHuds.Remove(enemy);
            }

            if (_bossEnemy == enemy)
            {
                _bossEnemy = null;
            }
        }

        private void ResolveBossEnemy()
        {
            if (_bossEnemy != null && _bossEnemy.IsAlive && _bossEnemy.IsBoss)
            {
                return;
            }

            _bossEnemy = null;
            foreach (EnemyHealth enemy in _trackedEnemies)
            {
                if (enemy != null && enemy.IsAlive && enemy.IsBoss)
                {
                    _bossEnemy = enemy;
                    return;
                }
            }
        }

        private void CreateSequenceHud(EnemyHealth enemy)
        {
            VisualElement container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            container.style.borderTopLeftRadius = 4f;
            container.style.borderTopRightRadius = 4f;
            container.style.borderBottomLeftRadius = 4f;
            container.style.borderBottomRightRadius = 4f;
            container.style.paddingLeft = 4f;
            container.style.paddingRight = 4f;
            _worldRoot.Add(container);

            SequenceHud hud = new SequenceHud();
            hud.Container = container;

            for (int i = 0; i < 3; i++)
            {
                Label arrowLabel = new Label();
                arrowLabel.style.width = 28f;
                arrowLabel.style.height = 32f;
                arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                arrowLabel.style.whiteSpace = WhiteSpace.Normal;
                container.Add(arrowLabel);
                hud.ArrowLabels[i] = arrowLabel;
            }

            Label ellipsis = new Label();
            ellipsis.text = "...";
            ellipsis.style.width = 24f;
            ellipsis.style.height = 32f;
            ellipsis.style.color = new Color(1f, 1f, 1f, 0.5f);
            ellipsis.style.fontSize = 16f;
            ellipsis.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(ellipsis);
            hud.EllipsisLabel = ellipsis;

            _sequenceHuds[enemy] = hud;

            EnemyAttackSequence seq = enemy.AttackSequence;
            if (seq != null)
            {
                hud.AdvancedHandler = _ => RefreshSequenceHud(enemy);
                seq.OnAdvanced += hud.AdvancedHandler;

                hud.SequenceSetHandler = () => RefreshSequenceHud(enemy);
                seq.OnSequenceSet += hud.SequenceSetHandler;
            }

            RefreshSequenceHud(enemy);
        }

        private void RefreshSequenceHud(EnemyHealth enemy)
        {
            if (!_sequenceHuds.TryGetValue(enemy, out SequenceHud hud))
            {
                return;
            }

            EnemyAttackSequence seq = enemy.AttackSequence;

            if (seq == null)
            {
                hud.ArrowLabels[0].text = Arrow(enemy.SwipeDirection);
                hud.ArrowLabels[0].style.display = DisplayStyle.Flex;
                hud.ArrowLabels[0].style.color = new Color(1f, 0.94f, 0.5f);
                hud.ArrowLabels[0].style.fontSize = 24f;
                hud.ArrowLabels[0].style.unityFontStyleAndWeight = FontStyle.Bold;
                for (int i = 1; i < 3; i++)
                {
                    hud.ArrowLabels[i].style.display = DisplayStyle.None;
                }

                hud.EllipsisLabel.style.display = DisplayStyle.None;
                return;
            }

            int currentIdx = seq.CurrentIndex;
            int total = seq.SequenceLength;
            int remaining = total - currentIdx;

            for (int i = 0; i < 3; i++)
            {
                int seqIdx = currentIdx + i;
                Label label = hud.ArrowLabels[i];

                if (seqIdx < total)
                {
                    label.style.display = DisplayStyle.Flex;
                    label.text = Arrow(seq.Sequence[seqIdx]);

                    if (i == 0)
                    {
                        label.style.color = new Color(1f, 0.94f, 0.2f);
                        label.style.fontSize = 28f;
                        label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    }
                    else
                    {
                        label.style.color = new Color(1f, 1f, 1f, 0.45f);
                        label.style.fontSize = 20f;
                        label.style.unityFontStyleAndWeight = FontStyle.Normal;
                    }
                }
                else
                {
                    label.style.display = DisplayStyle.None;
                }
            }

            hud.EllipsisLabel.style.display = remaining > 3 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleEnemyDamaged(EnemyHealth enemy, float damageAmount, object source)
        {
            if (enemy == null)
            {
                return;
            }

            SpawnFloatingText(enemy, $"-{Mathf.RoundToInt(damageAmount)}", Color.white, 0f);
            if (source is GangshinController)
            {
                SpawnFloatingText(enemy, "정화", new Color(1f, 0.76f, 0.32f), 24f);
            }

            if (enemy == _bossEnemy)
            {
                RefreshBoss();
            }
        }

        private void HandleEnemyDeath(EnemyHealth enemy)
        {
            RemoveEnemy(enemy);
            RefreshBoss();
        }

        private void SpawnFloatingText(EnemyHealth enemy, string text, Color color, float offsetY)
        {
            Label label = Text(_worldRoot, 0f, 0f, 120f, 24f, 20, TextAnchor.MiddleCenter);
            label.text = text;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _floatingTexts.Add(new FloatingText
            {
                Enemy = enemy,
                Label = label,
                TimeLeft = 0.8f,
                OffsetY = offsetY
            });
        }

        private void UpdateWorldElements(float deltaTime)
        {
            Camera camera = _cachedCamera;
            if (camera == null)
            {
                return;
            }

            IPanel panel = _root?.panel;

            foreach (KeyValuePair<EnemyHealth, SequenceHud> pair in _sequenceHuds)
            {
                PositionSequenceHud(pair.Key, pair.Value, camera, panel);
            }

            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingText floatingText = _floatingTexts[i];
                floatingText.TimeLeft -= deltaTime;
                floatingText.OffsetY += deltaTime * 34f;
                if (floatingText.TimeLeft <= 0f || floatingText.Enemy == null)
                {
                    floatingText.Label.RemoveFromHierarchy();
                    _floatingTexts.RemoveAt(i);
                    continue;
                }

                PositionAtEnemy(floatingText.Enemy, floatingText.Label, camera, panel, 1.2f, floatingText.OffsetY);
                Color color = floatingText.Label.resolvedStyle.color;
                color.a = Mathf.Clamp01(floatingText.TimeLeft / 0.8f);
                floatingText.Label.style.color = color;
            }
        }

        private void HandlePlayerHealthChanged(float current, float max)
        {
            RefreshHealth();
        }

        private void HandleGangshinGaugeChanged(float current, float max)
        {
            RefreshGangshin();
        }

        private void HandleGangshinStateChanged(GangshinState state)
        {
            RefreshGangshin();
        }

        private void HandleExperienceChanged(int level, float current, float threshold)
        {
            RefreshExperience();
        }

        private void HandleLevelSelectionOpened(int level, IReadOnlyList<SkillData> choices)
        {
            RefreshLevelUp();
        }

        private void HandleLevelSelectionClosed(int level)
        {
            RefreshLevelUp();
        }

        private void HandleWaveStarted(int waveNumber, WaveDefinition wave)
        {
            RefreshWave();
        }

        private void HandleWaveEnded(int waveNumber, WaveEndReason endReason)
        {
            RefreshWave();
        }

        private void HandleRemainingEnemyCountChanged(int waveNumber, int remainingEnemyCount)
        {
            RefreshWave();
        }

        private static void PositionAtEnemy(EnemyHealth enemy, Label label, Camera camera, IPanel panel, float worldYOffset, float screenYOffset)
        {
            if (enemy == null || label == null || !enemy.IsAlive)
            {
                label.style.display = DisplayStyle.None;
                return;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * worldYOffset);
            if (screenPoint.z <= 0f)
            {
                label.style.display = DisplayStyle.None;
                return;
            }

            Vector2 panelPos = panel != null
                ? RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y))
                : new Vector2(screenPoint.x, Screen.height - screenPoint.y);

            label.style.display = DisplayStyle.Flex;
            label.style.left = panelPos.x;
            label.style.top = panelPos.y - screenYOffset;
            label.style.translate = new Translate(Length.Percent(-50f), 0f);
        }

        private static void PositionSequenceHud(EnemyHealth enemy, SequenceHud hud, Camera camera, IPanel panel)
        {
            VisualElement container = hud.Container;
            if (enemy == null || container == null || !enemy.IsAlive)
            {
                if (container != null)
                {
                    container.style.display = DisplayStyle.None;
                }

                return;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 1.6f);
            if (screenPoint.z <= 0f)
            {
                container.style.display = DisplayStyle.None;
                return;
            }

            Vector2 panelPos = panel != null
                ? RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y))
                : new Vector2(screenPoint.x, Screen.height - screenPoint.y);

            container.style.display = DisplayStyle.Flex;
            container.style.left = panelPos.x;
            container.style.top = panelPos.y - 24f;
            container.style.translate = new Translate(Length.Percent(-50f), 0f);
        }

        private static string Arrow(SwipeDirection direction)
        {
            switch (direction)
            {
                case SwipeDirection.Up:
                    return "↑";
                case SwipeDirection.Down:
                    return "↓";
                case SwipeDirection.Left:
                    return "←";
                case SwipeDirection.Right:
                    return "→";
                default:
                    return "•";
            }
        }

        private static VisualElement Panel(VisualElement parent, float left, float top, float width, float height)
        {
            VisualElement panel = Box(parent);
            panel.style.left = left;
            panel.style.top = top;
            panel.style.width = width;
            panel.style.height = height;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            return panel;
        }

        private static VisualElement Box(VisualElement parent)
        {
            var element = new VisualElement();
            element.style.position = Position.Absolute;
            parent.Add(element);
            return element;
        }

        private static Label Text(VisualElement parent, float left, float top, float width, float height, int fontSize, TextAnchor anchor)
        {
            var label = new Label();
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            label.style.width = width;
            label.style.height = height;
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = anchor;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        private static VisualElement Bar(VisualElement parent, out Label label, bool centered)
        {
            var barRoot = new VisualElement();
            barRoot.style.position = Position.Absolute;
            barRoot.style.left = 0f;
            barRoot.style.right = 0f;
            barRoot.style.bottom = 8f;
            barRoot.style.height = 20f;
            barRoot.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            parent.Add(barRoot);

            var fill = new VisualElement();
            fill.style.height = Length.Percent(100f);
            fill.style.width = Length.Percent(100f);
            fill.style.backgroundColor = new Color(0.82f, 0.22f, 0.22f);
            barRoot.Add(fill);

            label = Text(barRoot, 8f, 0f, 300f, 20f, 16, centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            label.style.right = 8f;
            return fill;
        }

        private static void Stretch(VisualElement element)
        {
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
        }

        private static void Fill(VisualElement fill, float normalized)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
        }

        private void HandleAllWavesCompleted()
        {
            _waveLabel.text = "All Waves Cleared";
            _remainingLabel.text = "Remaining: 0";
        }
    }
}
