using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Gameplay.Combat;
using Mukseon.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mukseon.Gameplay.UI
{
    /// <summary>
    /// 적 종류가 이번 런에서 처음 등장할 때 이름표를 띄우고, 그 개체를 화살표로 가리키는 연출(#70).
    ///
    /// 챕터 1은 10분 동안 9종이 순차 투입되는데, 새 적이 조용히 섞여 들어오면 플레이어는 죽고 나서야
    /// "저건 뭐였지"를 알게 된다. 이름표만으로는 화면의 수십 마리 중 누가 새 적인지 못 찾으므로
    /// 상단 배너와 해당 개체 위 화살표를 <b>함께</b> 띄운다(표시는 <see cref="EnemyIntroductionView"/>).
    ///
    /// <see cref="GameplayHudBootstrapper"/>와 같은 자가 부트스트랩 방식이다 — 씬에 배치할 필요 없이
    /// 스스로 생성되고, <see cref="WaveCombatDirector"/>를 런타임에 찾아 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyIntroductionPresenter : MonoBehaviour
    {
        private sealed class PendingIntroduction
        {
            public string DisplayName;
            public EnemyHealth Enemy;
            public object SpeciesKey;
        }

        private const string RootObjectName = "EnemyIntroductionRuntime";

        // 인게임 HUD(500) 위, 결과(600)·조작 안내(700) 아래. 배너는 상단 중앙이라
        // 화면 중앙에 뜨는 레벨업 카드와 시각적으로 겹치지 않는다.
        private const int BannerSortingOrder = 550;

        private const float HoldSeconds = 1.8f;
        private const float FadeSeconds = 0.5f;
        private const float TotalSeconds = HoldSeconds + FadeSeconds;

        // 폴링 상한은 HUD와 같은 이유 — 비게임플레이 씬(타이틀/캐릭터선택)에는 웨이브 디렉터가 없어
        // 상한이 없으면 0.5초마다 Find를 영원히 반복한다.
        private const int MaxResolveRetries = 6;
        private const float ResolveIntervalSeconds = 0.5f;

        private readonly EnemyIntroductionTracker _tracker = new EnemyIntroductionTracker();
        private readonly List<PendingIntroduction> _queue = new List<PendingIntroduction>(4);

        private WaveCombatDirector _director;
        private Camera _cachedCamera;
        private float _resolveRetryTimer;
        private int _resolveRetryCount;
        private bool _stopPolling;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private EnemyIntroductionView _view;

        private PendingIntroduction _current;
        private float _remainingSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePresenter()
        {
            if (SceneObjectFinder.Find<EnemyIntroductionPresenter>() != null)
            {
                return;
            }

            var root = new GameObject(RootObjectName);
            root.AddComponent<EnemyIntroductionPresenter>();
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
            Unsubscribe();

            // PanelSettings는 에셋이 아니라 런타임 인스턴스라 직접 파기해야 누수되지 않는다.
            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
                _panelSettings = null;
            }
        }

        private void Update()
        {
            if (!_stopPolling && _director == null)
            {
                _resolveRetryTimer -= Time.unscaledDeltaTime;
                if (_resolveRetryTimer <= 0f)
                {
                    _resolveRetryTimer = ResolveIntervalSeconds;
                    TryResolveDirector();

                    _resolveRetryCount++;
                    if (_resolveRetryCount >= MaxResolveRetries)
                    {
                        _stopPolling = true;
                    }
                }
            }

            // 레벨업 정지 중에도 배너가 흘러가도록 unscaled를 쓴다(HUD 플로팅 텍스트와 동일).
            TickPresentation(Time.unscaledDeltaTime);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneLoaded();
        }

        private void HandleSceneLoaded()
        {
            Unsubscribe();
            _director = null;
            _cachedCamera = Camera.main;
            _resolveRetryTimer = 0f;
            _resolveRetryCount = 0;
            _stopPolling = false;

            // ResetRun이 배너를 숨기므로 UI가 먼저 존재해야 한다.
            EnsureUi();
            // 씬이 바뀌면 새 런이다 — 이전 런의 등장 기록과 대기열을 모두 버린다.
            ResetRun();
            TryResolveDirector();
        }

        private void TryResolveDirector()
        {
            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
            }

            if (_director != null)
            {
                return;
            }

            _director = SceneObjectFinder.Find<WaveCombatDirector>();
            if (_director == null)
            {
                return;
            }

            _director.OnEnemySpawned += HandleEnemySpawned;
            _director.OnWaveStarted += HandleWaveStarted;
        }

        private void Unsubscribe()
        {
            if (_director == null)
            {
                return;
            }

            _director.OnEnemySpawned -= HandleEnemySpawned;
            _director.OnWaveStarted -= HandleWaveStarted;
        }

        // 1번 웨이브 시작 = 타임라인이 처음부터 다시 도는 것(StartWaves)이므로 등장 기록을 초기화한다.
        private void HandleWaveStarted(int waveNumber, WaveDefinition wave)
        {
            if (waveNumber <= 1)
            {
                ResetRun();
            }
        }

        private void HandleEnemySpawned(EnemyHealth enemy, WaveEnemySpawnEntry entry)
        {
            if (enemy == null || entry == null)
            {
                return;
            }

            // 키와 이름은 스포너와 같은 기준(엔트리)에서만 읽는다 — 적 인스턴스에서 유추하면
            // 풀에서 재사용된 개체가 들고 있는 이전 종류의 MonsterData를 읽어 판정이 어긋난다.
            object speciesKey = entry.SpeciesKey;
            if (!_tracker.TryMarkIntroduced(speciesKey))
            {
                return;
            }

            _queue.Add(new PendingIntroduction
            {
                DisplayName = entry.DisplayName,
                Enemy = enemy,
                SpeciesKey = speciesKey
            });
        }

        private void ResetRun()
        {
            _tracker.Reset();
            _queue.Clear();
            _current = null;
            _remainingSeconds = 0f;
            _view?.Hide();
        }

        private void TickPresentation(float deltaTime)
        {
            if (_current == null)
            {
                if (_queue.Count == 0)
                {
                    return;
                }

                _current = _queue[0];
                _queue.RemoveAt(0);
                _remainingSeconds = TotalSeconds;
                _view.ShowBanner(_current.DisplayName);
            }

            _remainingSeconds -= deltaTime;
            if (_remainingSeconds <= 0f)
            {
                _current = null;
                _view.Hide();
                return;
            }

            // 마지막 FadeSeconds 구간에서만 서서히 사라진다.
            _view.SetOpacity(_remainingSeconds / FadeSeconds);
            UpdateMarker();
        }

        /// <summary>
        /// 소개 중인 개체 머리 위로 화살표를 옮긴다. 개체가 죽었거나 풀에서 다른 종류로 재사용됐으면
        /// (SpeciesKey 불일치) 화살표만 감추고 배너는 그대로 둔다.
        /// </summary>
        private void UpdateMarker()
        {
            EnemyHealth enemy = _current.Enemy;

            // 지금 이 개체에 배정된 종류는 디렉터의 등록부에서 읽는다 — 사망·정리 시 등록이 지워지고
            // 재사용 시 새 키로 덮이므로, 인스턴스에서 유추하는 것과 달리 스포너의 판단과 항상 일치한다.
            // 키 비교는 Equals — 폴백 키가 문자열이면 매번 새 인스턴스라 참조 비교로는 항상 어긋난다.
            bool trackable = enemy != null && enemy.IsAlive && _director != null &&
                             _director.TryGetSpeciesKey(enemy, out object currentKey) &&
                             Equals(currentKey, _current.SpeciesKey);
            if (!trackable)
            {
                _view.HideMarker();
                return;
            }

            _view.UpdateMarker(_cachedCamera, enemy.transform.position);
        }

        private void EnsureUi()
        {
            if (_document != null)
            {
                return;
            }

            _panelSettings = ScreenUiFactory.CreatePanelSettings(BannerSortingOrder);
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;

            VisualElement root = _document.rootVisualElement;
            root.style.flexGrow = 1f;
            // 루트가 입력을 먹으면 그 아래 전투 스와이프가 막힌다. 연출은 표시 전용이다.
            root.pickingMode = PickingMode.Ignore;

            _view = new EnemyIntroductionView(root);
        }
    }
}
