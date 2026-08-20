using System;
using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using Mukseon.UI;
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
            public const string SettingsButton = "설정";
        }

        private const string RootObjectName = "GameplayHudRuntime";

        /// <summary>씬 전역 단일 인스턴스. 보스 컨트롤러가 패턴 인디케이터 오브를 띄울 때 사용한다(#69).</summary>
        public static GameplayHudBootstrapper Instance { get; private set; }

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
            public Action<SwipeDirection> DirectionChangedHandler;
            public EnemyDirectionColorView ColorView;

            // 표시할 슬롯이 하나도 없는 상태(#83). 위치 갱신이 매 프레임 컨테이너를 다시 켜지 않도록
            // RefreshSequenceHud가 계산한 결과를 여기 남겨 PositionSequenceHud가 읽는다.
            public bool HasVisibleMarker = true;
        }

        private readonly HashSet<EnemyHealth> _trackedEnemies = new HashSet<EnemyHealth>();
        private readonly Dictionary<EnemyHealth, SequenceHud> _sequenceHuds = new Dictionary<EnemyHealth, SequenceHud>();
        private readonly List<FloatingText> _floatingTexts = new List<FloatingText>();
        private readonly List<EnemyHealth> _enemyBuffer = new List<EnemyHealth>(64);
        // 설정 변경 시 방향 표시를 다시 그릴 때 쓰는 버퍼(#83). SyncEnemies의 _enemyBuffer와 공유하면
        // 두 순회가 겹칠 때 서로의 내용을 지운다.
        private readonly List<EnemyHealth> _markerRefreshBuffer = new List<EnemyHealth>(64);
        private readonly List<EnemyHealth> _removedEnemyBuffer = new List<EnemyHealth>(64);
        private float _resolveRetryTimer;
        private Camera _cachedCamera;

        // 비게임플레이 씬(타이틀/캐릭터선택)에는 플레이어·웨이브가 없어 NeedsRuntimeReferenceRefresh가
        // 영구히 true가 된다. 상한이 없으면 0.5초마다 Find와 경고 로그를 영원히 반복하므로,
        // GameplayInputGate와 동일하게 일정 횟수 후 폴링을 멈춘다. 씬이 새로 로드되면 다시 초기화된다(#36).
        private const int MaxResolveRetries = 6;
        private int _resolveRetryCount;
        private bool _stopPolling;

        // 패턴 인디케이터 오브(#69): 적 머리 위 색 오브와 동일한 HUD 요소를, 보스 패턴 텔레그래프
        // 위치 상단에 띄운다. 접근성(화살표) 모드는 적 오브와 같은 ApplyDirectionMarker 경로로 함께 처리된다(#83).
        private Label _patternOrb;
        private Transform _patternOrbAnchor;
        private Vector3 _patternOrbWorldOffset;
        private bool _patternOrbActive;
        // 설정이 바뀌면 오브를 다시 그려야 하므로 현재 표시 중인 방향을 들고 있는다(#83).
        private SwipeDirection _patternOrbDirection;
        private const float PatternOrbWorldYOffset = 0.8f;

        // 보스 등장 연출(#69): 연출 동안 체력바를 가렸다가(스폰 직후) 채움 비율로 드러낸다.
        // _bossHealthRevealing이 true이면 실제 HP 대신 _bossHealthRevealRatio로 표시한다.
        private bool _suppressBossHud;
        private bool _bossHealthRevealing;
        private float _bossHealthRevealRatio;

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
            return SceneObjectFinder.Find<GameplayHudBootstrapper>();
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            // 인게임에서 설정을 바꿔도 이미 떠 있는 방향 표시가 즉시 따라가야 한다(#83).
            DirectionColorSettings.OnChanged += RefreshDirectionMarkers;
            EnsureUi();
            HandleSceneLoaded();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            DirectionColorSettings.OnChanged -= RefreshDirectionMarkers;
            UnsubscribeAll();
            ClearEnemies();

            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
            }
        }

        private void Update()
        {
            if (!_stopPolling && NeedsRuntimeReferenceRefresh())
            {
                _resolveRetryTimer -= Time.unscaledDeltaTime;
                if (_resolveRetryTimer <= 0f)
                {
                    _resolveRetryTimer = 0.5f;
                    TryResolveSources();
                    ApplyHudVisibility();

                    _resolveRetryCount++;
                    if (_resolveRetryCount >= MaxResolveRetries)
                    {
                        _stopPolling = true;
                    }
                }
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
            HidePatternOrb();
            _suppressBossHud = false;
            _bossHealthRevealing = false;
            _bossHealthRevealRatio = 0f;
            _playerHealth = null;
            _gangshinController = null;
            _playerLevelSystem = null;
            _waveCombatDirector = null;
            _bossEnemy = null;
            _cachedCamera = Camera.main;
            _resolveRetryTimer = 0f;
            _resolveRetryCount = 0;
            _stopPolling = false;
            EnsureUi();
            TryResolveSources();
            ApplyHudVisibility();
        }

        /// <summary>
        /// 게임플레이 씬이 아니면 HUD 전체를 숨긴다(#36). 이 부트스트래퍼는 DontDestroyOnLoad라
        /// 타이틀·캐릭터선택 씬까지 따라오는데, 그대로 두면 빈 체력바/웨이브 패널이 메타 화면 위에 남는다.
        /// 플레이어의 존재를 게임플레이 씬 판정 기준으로 삼는다.
        /// </summary>
        private void ApplyHudVisibility()
        {
            if (_root == null)
            {
                return;
            }

            _root.style.display = _playerHealth != null ? DisplayStyle.Flex : DisplayStyle.None;
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

            BuildSettingsButton();

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
                _playerHealth = SceneObjectFinder.Find<PlayerHealth>();
                if (_playerHealth != null)
                {
                    _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
                    RefreshHealth();
                }
            }

            if (_gangshinController == null)
            {
                _gangshinController = SceneObjectFinder.Find<GangshinController>();
                if (_gangshinController != null)
                {
                    _gangshinController.OnGaugeChanged += HandleGangshinGaugeChanged;
                    _gangshinController.OnStateChanged += HandleGangshinStateChanged;
                    RefreshGangshin();
                }
            }

            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = SceneObjectFinder.Find<PlayerLevelSystem>();
                if (_playerLevelSystem != null)
                {
                    _playerLevelSystem.OnExperienceChanged += HandleExperienceChanged;
                    _playerLevelSystem.OnLevelSelectionOpened += HandleLevelSelectionOpened;
                    _playerLevelSystem.OnLevelSelectionClosed += HandleLevelSelectionClosed;
                    RefreshExperience();
                    RefreshLevelUp();
                }
            }

            if (_waveCombatDirector == null)
            {
                _waveCombatDirector = SceneObjectFinder.Find<WaveCombatDirector>();
                if (_waveCombatDirector != null)
                {
                    _waveCombatDirector.OnWaveStarted += HandleWaveStarted;
                    _waveCombatDirector.OnWaveEnded += HandleWaveEnded;
                    _waveCombatDirector.OnRemainingEnemyCountChanged += HandleRemainingEnemyCountChanged;
                    _waveCombatDirector.OnAllWavesCompleted += HandleAllWavesCompleted;
                    RefreshWave();
                }
            }

            WarnMissingSources();
        }

        /// <summary>
        /// 빠진 HUD 소스를 알린다. 단, 4종이 <b>전부</b> 없으면 비게임플레이 씬(타이틀/캐릭터선택)이라는
        /// 뜻이므로 침묵한다 — 이 부트스트래퍼는 DontDestroyOnLoad라 메타 화면까지 따라오는데, 거기서
        /// 소스가 없는 건 정상이다. 일부만 빠진 경우에만 게임플레이 씬의 배선 오류로 보고 경고한다(#36).
        /// </summary>
        private void WarnMissingSources()
        {
            bool anyResolved = _playerHealth != null ||
                               _gangshinController != null ||
                               _playerLevelSystem != null ||
                               _waveCombatDirector != null;
            if (!anyResolved)
            {
                return;
            }

            if (_playerHealth == null)
            {
                Debug.LogWarning("[GameplayHudBootstrapper] PlayerHealth를 찾을 수 없습니다. 체력 HUD가 표시되지 않습니다.");
            }

            if (_gangshinController == null)
            {
                Debug.LogWarning("[GameplayHudBootstrapper] GangshinController를 찾을 수 없습니다. 강신 게이지 HUD가 표시되지 않습니다.");
            }

            if (_playerLevelSystem == null)
            {
                Debug.LogWarning("[GameplayHudBootstrapper] PlayerLevelSystem을 찾을 수 없습니다. 경험치 HUD 및 레벨업 패널이 표시되지 않습니다.");
            }

            if (_waveCombatDirector == null)
            {
                Debug.LogWarning("[GameplayHudBootstrapper] WaveCombatDirector를 찾을 수 없습니다. 웨이브 HUD가 표시되지 않습니다.");
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
            // 등장 연출 스폰 직후에는 보스가 살아 있어도 체력바를 가린다(_suppressBossHud).
            bool visible = _bossEnemy != null && _bossEnemy.IsAlive && !_suppressBossHud;
            _bossRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            _bossLabel.text = _bossEnemy.DisplayName;
            // 등장 연출 중에는 실제 HP가 아닌 연출용 채움 비율로 표시한다.
            float ratio = _bossHealthRevealing
                ? _bossHealthRevealRatio
                : _bossEnemy.CurrentHealth / Mathf.Max(1f, _bossEnemy.MaxHealth);
            Fill(_bossFill, ratio);
        }

        /// <summary>보스 체력바를 강제로 가리거나 다시 표시한다(등장 연출 — 스폰 후 화면 밖 대기 동안 숨김).</summary>
        public void SuppressBossHealthBar(bool suppress)
        {
            _suppressBossHud = suppress;
            RefreshBoss();
        }

        /// <summary>등장 연출 채움 모드로 전환하고 체력바를 주어진 비율(0~1)로 표시한다. 숨김도 함께 해제한다.</summary>
        public void SetBossHealthRevealRatio(float ratio)
        {
            _bossHealthRevealing = true;
            _bossHealthRevealRatio = Mathf.Clamp01(ratio);
            _suppressBossHud = false;
            RefreshBoss();
        }

        /// <summary>등장 연출 채움 모드를 끝내고 실제 HP 기반 표시로 되돌린다.</summary>
        public void EndBossHealthReveal()
        {
            _bossHealthRevealing = false;
            RefreshBoss();
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
                if (hud.DirectionChangedHandler != null)
                {
                    enemy.OnDirectionChanged -= hud.DirectionChangedHandler;
                }

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
                _suppressBossHud = false;
                _bossHealthRevealing = false;
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

            // 색 오브 색상을 적의 외곽선 글로우와 동일한 팔레트 인스턴스에서 가져오기 위해 캐싱한다(#82).
            hud.ColorView = enemy.GetComponent<EnemyDirectionColorView>();
            _sequenceHuds[enemy] = hud;

            // 단일 방향 적(예: MokgwiRoot, MaeguProjectile)은 스폰 이후 SetSwipeDirection으로
            // 방향이 동적으로 바뀔 수 있으므로, 방향 변경 시 색 오브가 갱신되도록 구독한다(#82).
            hud.DirectionChangedHandler = _ => RefreshSequenceHud(enemy);
            enemy.OnDirectionChanged += hud.DirectionChangedHandler;

            EnemyAttackSequence seq = enemy.AttackSequence;
            if (seq != null && enemy.UsesAttackSequence)
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

            if (!enemy.UsesAttackSequence)
            {
                // 색상 1차 표시(#82): 단일 방향 적은 현재 방향 표시 하나만 띄운다.
                // 이 표시는 외곽선 글로우와 같은 정보라, 표시 방식이 '글로우 전용'이면 숨긴다(#83).
                SwipeDirection direction = enemy.SwipeDirection;
                ApplyDirectionMarker(hud.ArrowLabels[0], direction, ResolveDirectionColor(hud, direction), true, false);
                for (int i = 1; i < 3; i++)
                {
                    hud.ArrowLabels[i].style.display = DisplayStyle.None;
                }

                hud.EllipsisLabel.style.display = DisplayStyle.None;
                SyncSequenceContainerVisibility(hud);
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
                    // 시퀀스 적(보스): 현재 타격 대상(i==0)만 강조, 이후는 흐리게.
                    // 다음 순번 방향은 글로우(현재 방향 하나)가 담지 못하는 정보라, 표시 방식이
                    // '글로우 전용'이어도 숨기지 않는다 — 숨기면 보스 시퀀스를 읽을 방법이 사라진다(#83).
                    SwipeDirection direction = seq.Sequence[seqIdx];
                    ApplyDirectionMarker(label, direction, ResolveDirectionColor(hud, direction), i == 0, true);
                }
                else
                {
                    label.style.display = DisplayStyle.None;
                }
            }

            hud.EllipsisLabel.style.display = remaining > 3 ? DisplayStyle.Flex : DisplayStyle.None;
            SyncSequenceContainerVisibility(hud);
        }

        /// <summary>
        /// 표시할 슬롯이 하나도 없으면 컨테이너까지 숨긴다(#83).
        /// 컨테이너는 반투명 검정 배경과 좌우 패딩을 갖고 있어, 그대로 두면 표시 방식이
        /// '글로우 전용'일 때 적 머리 위에 빈 검은 알약만 떠 있게 된다.
        /// </summary>
        private static void SyncSequenceContainerVisibility(SequenceHud hud)
        {
            bool anyVisible = hud.EllipsisLabel.style.display == DisplayStyle.Flex;
            for (int i = 0; i < hud.ArrowLabels.Length && !anyVisible; i++)
            {
                anyVisible = hud.ArrowLabels[i].style.display == DisplayStyle.Flex;
            }

            hud.HasVisibleMarker = anyVisible;
            hud.Container.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
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

            PositionPatternOrb(camera, panel);

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
            // 표시 방식이 '글로우 전용'이면 그릴 슬롯이 없다(#83). 이 검사를 빼면 매 프레임
            // 컨테이너를 다시 켜 버려, 적 머리 위에 빈 검은 알약만 남는다.
            if (enemy == null || container == null || !enemy.IsAlive || !hud.HasVisibleMarker)
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

        // ── 패턴 인디케이터 오브(#69) ───────────────────────────────────────
        // 적 머리 위 색 오브와 동일한 HUD 요소를, 보스 패턴 텔레그래프 위치 상단에 띄운다.
        // 일반 적 오브와 같은 ApplyDirectionMarker 경로를 쓰므로, 접근성(화살표) 모드는 한 곳에서 처리된다(#83).

        /// <summary>
        /// 인게임 환경설정 진입점(#83). 방향 표시 방식은 실제 전투 화면과 대조해야 고를 수 있으므로,
        /// 타이틀에서만 열 수 있으면 설정의 목적을 절반 잃는다.
        ///
        /// 좌상단은 체력바, 상단 중앙은 웨이브 패널이 차지하므로 우상단에 붙인다.
        /// 오조작으로 전투가 멈추지 않도록 작게 두고, 열리면 오버레이가 정지를 소유한다.
        /// </summary>
        private void BuildSettingsButton()
        {
            var button = new Button(SettingsOverlay.Open) { text = Strings.SettingsButton };
            button.style.position = Position.Absolute;
            button.style.right = 16f;
            button.style.top = 16f;
            button.style.width = 92f;
            button.style.height = 44f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.fontSize = 18;
            button.style.color = new Color(0.92f, 0.90f, 0.86f, 0.9f);
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            button.style.borderTopLeftRadius = 6f;
            button.style.borderTopRightRadius = 6f;
            button.style.borderBottomLeftRadius = 6f;
            button.style.borderBottomRightRadius = 6f;
            _root.Add(button);
        }

        /// <summary>
        /// 표시 방식·화살표 병행·커스텀 색이 바뀌었을 때 화면에 떠 있는 모든 방향 표시를 다시 그린다(#83).
        /// 적 외곽선 글로우는 각 <see cref="EnemyDirectionColorView"/>가 같은 이벤트를 따로 구독해 처리한다.
        /// </summary>
        private void RefreshDirectionMarkers()
        {
            // RefreshSequenceHud가 딕셔너리를 수정하지는 않지만, 열거 중 적이 죽어 항목이 빠지는 상황을
            // 피하려고 키를 먼저 버퍼에 복사한다(SyncEnemies와 같은 방식).
            _markerRefreshBuffer.Clear();
            foreach (EnemyHealth enemy in _sequenceHuds.Keys)
            {
                _markerRefreshBuffer.Add(enemy);
            }

            for (int i = 0; i < _markerRefreshBuffer.Count; i++)
            {
                RefreshSequenceHud(_markerRefreshBuffer[i]);
            }

            _markerRefreshBuffer.Clear();

            if (_patternOrbActive && _patternOrb != null)
            {
                ApplyDirectionMarker(_patternOrb, _patternOrbDirection, ResolvePatternOrbColor(_patternOrbDirection), true, true);
            }
        }

        /// <summary>보스 패턴 텔레그래프 위치(앵커+오프셋) 상단에 카운터 방향 색 오브를 띄운다.</summary>
        public void ShowPatternOrb(Transform anchor, Vector2 worldOffset, SwipeDirection direction)
        {
            if (anchor == null)
            {
                return;
            }

            EnsurePatternOrb();
            if (_patternOrb == null)
            {
                return;
            }

            _patternOrbAnchor = anchor;
            _patternOrbWorldOffset = new Vector3(worldOffset.x, worldOffset.y, 0f);
            _patternOrbActive = true;
            _patternOrbDirection = direction;
            // 패턴 텔레그래프는 적 개체가 아니라 공간을 가리키므로 글로우로 대체될 수 없다 → alwaysVisible.
            ApplyDirectionMarker(_patternOrb, direction, ResolvePatternOrbColor(direction), true, true);
        }

        /// <summary>패턴 인디케이터 오브를 숨긴다.</summary>
        public void HidePatternOrb()
        {
            _patternOrbActive = false;
            _patternOrbAnchor = null;
            if (_patternOrb != null)
            {
                _patternOrb.style.display = DisplayStyle.None;
            }
        }

        private void EnsurePatternOrb()
        {
            if (_patternOrb != null || _worldRoot == null)
            {
                return;
            }

            _patternOrb = new Label();
            _patternOrb.style.position = Position.Absolute;
            _patternOrb.style.display = DisplayStyle.None;
            // 접근성 화살표 글리프가 슬롯 중앙에 오도록 한다(#83). 색 오브만 쓸 때는 영향이 없다.
            _patternOrb.style.unityTextAlign = TextAnchor.MiddleCenter;
            _patternOrb.style.whiteSpace = WhiteSpace.Normal;
            _worldRoot.Add(_patternOrb);
        }

        // 보스 본체 색 오브와 동일한 팔레트로 해석해 색을 일치시킨다. 보스가 없으면 정적 디폴트로 폴백.
        private Color ResolvePatternOrbColor(SwipeDirection direction)
        {
            if (_bossEnemy != null && _sequenceHuds.TryGetValue(_bossEnemy, out SequenceHud hud))
            {
                return ResolveDirectionColor(hud, direction);
            }

            return DirectionColorPalette.DefaultColor(direction);
        }

        private void PositionPatternOrb(Camera camera, IPanel panel)
        {
            if (!_patternOrbActive || _patternOrb == null)
            {
                return;
            }

            if (_patternOrbAnchor == null)
            {
                _patternOrb.style.display = DisplayStyle.None;
                return;
            }

            Vector3 world = _patternOrbAnchor.position + _patternOrbWorldOffset + Vector3.up * PatternOrbWorldYOffset;
            Vector3 screenPoint = camera.WorldToScreenPoint(world);
            if (screenPoint.z <= 0f)
            {
                _patternOrb.style.display = DisplayStyle.None;
                return;
            }

            Vector2 panelPos = panel != null
                ? RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y))
                : new Vector2(screenPoint.x, Screen.height - screenPoint.y);

            _patternOrb.style.display = DisplayStyle.Flex;
            _patternOrb.style.left = panelPos.x;
            _patternOrb.style.top = panelPos.y;
            _patternOrb.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
        }

        // 접근성 화살표 병행 표시(#83)의 글리프. 색으로만 방향을 구분할 수 없는 유저를 위한 형태 단서다.
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

        /// <summary>
        /// 색 오브 색상을 적의 <see cref="EnemyDirectionColorView"/>(외곽선 글로우와 동일한 팔레트
        /// 인스턴스)에서 조회한다(#82). 뷰가 없으면 정적 디폴트로 폴백해, HUD 색 오브와 월드의
        /// 외곽선 글로우 색이 팔레트 에셋을 수정하더라도 항상 일치하도록 보장한다.
        /// </summary>
        private static Color ResolveDirectionColor(SequenceHud hud, SwipeDirection direction)
        {
            // 폴백도 반드시 Resolve를 거쳐야 유저 커스텀 매핑(#83)이 적용된다.
            return hud.ColorView != null
                ? hud.ColorView.ResolveColor(direction)
                : DirectionColorPalette.Resolve(null, direction);
        }

        /// <summary>
        /// 방향 표시 슬롯을 현재 표시 설정에 맞춰 스타일링한다(#82/#83, `combat_system.md` §3).
        ///
        /// 색 오브(둥근 색 구슬)와 접근성 화살표는 서로 독립적으로 켜진다:
        /// <list type="bullet">
        /// <item>오브 O / 화살표 X — 색 구슬만 (기본)</item>
        /// <item>오브 O / 화살표 O — 색 구슬 안에 방향 화살표 (색맹·색약 유저도 방향을 읽을 수 있다)</item>
        /// <item>오브 X / 화살표 O — 배경 없이 방향 색 화살표만</item>
        /// <item>오브 X / 화살표 X — 슬롯을 숨긴다(<paramref name="alwaysVisible"/>이면 오브로 폴백)</item>
        /// </list>
        ///
        /// <paramref name="current"/>=true면 현재 타격 대상으로 크게·불투명·밝은 외곽선 강조, false면 작게·반투명.
        /// 색상은 <see cref="ResolveDirectionColor"/>가 적 팔레트에서 해석한 값을 받는다.
        /// </summary>
        /// <param name="alwaysVisible">
        /// 외곽선 글로우가 대체할 수 없는 정보를 담은 슬롯(보스 시퀀스의 다음 순번, 패턴 텔레그래프)은
        /// 표시 방식이 '글로우 전용'이어도 숨기면 안 되므로 true를 넘긴다.
        /// </param>
        private static void ApplyDirectionMarker(Label slot, SwipeDirection direction, Color color, bool current, bool alwaysVisible)
        {
            bool showArrow = DirectionColorSettings.ArrowAssistEnabled;
            bool showOrb = DirectionColorSettings.OrbEnabled || (alwaysVisible && !showArrow);

            if (!showOrb && !showArrow)
            {
                slot.style.display = DisplayStyle.None;
                return;
            }

            slot.style.display = DisplayStyle.Flex;

            // 화살표 글리프가 들어가면 같은 지름으로는 읽히지 않으므로 슬롯을 키운다.
            float size = showArrow
                ? (current ? 26f : 19f)
                : (current ? 20f : 14f);
            slot.style.width = size;
            slot.style.height = size;
            slot.style.marginLeft = 3f;
            slot.style.marginRight = 3f;

            float radius = size * 0.5f;
            slot.style.borderTopLeftRadius = radius;
            slot.style.borderTopRightRadius = radius;
            slot.style.borderBottomLeftRadius = radius;
            slot.style.borderBottomRightRadius = radius;

            // 아직 차례가 오지 않은 슬롯은 반투명하게 낮춰 현재 타격 대상과 구분한다.
            float alpha = current ? 1f : 0.5f;

            Color fill = color;
            fill.a = showOrb ? alpha : 0f;
            slot.style.backgroundColor = fill;

            if (showArrow)
            {
                slot.text = Arrow(direction);
                slot.style.fontSize = size * 0.72f;
                slot.style.unityTextAlign = TextAnchor.MiddleCenter;
                slot.style.unityFontStyleAndWeight = FontStyle.Bold;

                // 오브 위에서는 대비를 위해 흰 글리프, 오브 없이 단독일 때는 방향 색 자체를 글리프에 쓴다.
                Color glyph = showOrb ? Color.white : color;
                glyph.a = alpha;
                slot.style.color = glyph;
            }
            else
            {
                slot.text = string.Empty;
            }

            // 외곽선 강조는 오브가 있을 때만 의미가 있다(배경 없는 화살표에 테두리를 두르면 사각형이 보인다).
            float border = current && showOrb ? 2f : 0f;
            slot.style.borderTopWidth = border;
            slot.style.borderBottomWidth = border;
            slot.style.borderLeftWidth = border;
            slot.style.borderRightWidth = border;

            Color borderColor = new Color(1f, 1f, 1f, border > 0f ? 0.9f : 0f);
            slot.style.borderTopColor = borderColor;
            slot.style.borderBottomColor = borderColor;
            slot.style.borderLeftColor = borderColor;
            slot.style.borderRightColor = borderColor;
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
