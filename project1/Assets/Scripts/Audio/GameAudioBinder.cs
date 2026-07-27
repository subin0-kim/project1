using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using Mukseon.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mukseon.Audio
{
    /// <summary>
    /// 게임 이벤트를 오디오 큐로 옮기는 배선 담당(#38).
    ///
    /// 전투·성장 시스템에 <c>AudioManager.PlaySfx</c>를 직접 심지 않고 여기 한 곳에 모은 이유:
    /// 소리 하나를 빼거나 옮길 때 건드릴 파일이 하나여야 하고, 그래야 각 시스템이 오디오를 몰라도 된다
    /// (이벤트 기반 아키텍처 규칙).
    ///
    /// 씬 안의 이벤트 소스는 씬마다 다른 인스턴스이므로 씬이 로드될 때마다 다시 붙는다.
    /// 소스가 씬 로드보다 늦게 생성되는 경우가 있어, 다 붙을 때까지 <see cref="Update"/>에서 재시도한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioBinder : MonoBehaviour
    {
        private const string RootObjectName = "GameAudioBinder";

        private static GameAudioBinder _instance;

        private PlayerSwipeAttackController _attack;
        private SoulCollector _soulCollector;
        private PlayerLevelSystem _levelSystem;
        private GangshinController _gangshin;
        private WaveCombatDirector _waves;
        private GameOverHandler _gameOver;

        // 전투 씬에서만 소스를 찾는다. 타이틀·캐릭터선택 씬에서 매 프레임 헛되이 훑지 않기 위함.
        private bool _bindingNeeded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBinder()
        {
            if (SceneObjectFinder.Find<GameAudioBinder>() != null)
            {
                return;
            }

            var root = new GameObject(RootObjectName);
            root.AddComponent<GameAudioBinder>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnemyHealth.AnyEnemyDamaged += HandleEnemyDamaged;
            EnemyHealth.AnyEnemyDied += HandleEnemyDied;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        // 첫 씬은 이 컴포넌트가 생기기 전에 이미 로드가 끝나 sceneLoaded를 못 받는다. 여기서 한 번 맞춘다.
        // Awake가 아니라 Start인 이유: AudioManager도 같은 시점에 스스로 뜨는데 생성 순서가 정해져 있지
        // 않아, Awake에서 BGM을 요청하면 매니저가 아직 없어 조용히 무시될 수 있다.
        private void Start()
        {
            ApplySceneAudio(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            EnemyHealth.AnyEnemyDamaged -= HandleEnemyDamaged;
            EnemyHealth.AnyEnemyDied -= HandleEnemyDied;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindSceneSources();
            _instance = null;
        }

        private void Update()
        {
            if (_bindingNeeded)
            {
                TryBindSceneSources();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnbindSceneSources();
            ApplySceneAudio(scene);
        }

        private void ApplySceneAudio(Scene scene)
        {
            bool isGameplay = scene.name == ScreenFlow.GameplayScene;

            // 타이틀·캐릭터선택은 같은 로비 트랙을 쓴다. 같은 트랙이면 BgmPlayer가 재시작하지 않으므로
            // 두 화면을 오가도 음악이 끊기지 않는다.
            AudioManager.PlayBgm(isGameplay ? BgmTrack.Battle : BgmTrack.Lobby);

            _bindingNeeded = isGameplay;
            if (isGameplay)
            {
                TryBindSceneSources();
            }
        }

        // 아직 못 찾은 소스만 골라 붙인다. 이미 붙은 것은 참조가 남아 있어 다시 구독되지 않는다.
        private void TryBindSceneSources()
        {
            if (_attack == null && TryFind(out _attack))
            {
                _attack.OnAttackExecuted += HandleAttackExecuted;
            }

            if (_soulCollector == null && TryFind(out _soulCollector))
            {
                _soulCollector.OnSoulCollected += HandleSoulCollected;
            }

            if (_levelSystem == null && TryFind(out _levelSystem))
            {
                _levelSystem.OnLevelSelectionOpened += HandleLevelSelectionOpened;
            }

            if (_gangshin == null && TryFind(out _gangshin))
            {
                _gangshin.OnActivated += HandleGangshinActivated;
            }

            if (_waves == null && TryFind(out _waves))
            {
                _waves.OnBossPhaseStarted += HandleBossPhaseStarted;
                _waves.OnAllWavesCompleted += HandleRunEnded;
            }

            if (_gameOver == null && TryFind(out _gameOver))
            {
                _gameOver.OnGameOver += HandleRunEnded;
            }

            _bindingNeeded = _attack == null || _soulCollector == null || _levelSystem == null
                             || _gangshin == null || _waves == null || _gameOver == null;
        }

        private void UnbindSceneSources()
        {
            // 파괴된 컴포넌트는 Unity의 == 오버로드가 null로 보고하고, 델리게이트도 함께 사라지므로
            // 살아 있는 것만 떼어 낸다(같은 씬이 유지되는 애디티브 로드 대비).
            if (_attack != null)
            {
                _attack.OnAttackExecuted -= HandleAttackExecuted;
            }

            if (_soulCollector != null)
            {
                _soulCollector.OnSoulCollected -= HandleSoulCollected;
            }

            if (_levelSystem != null)
            {
                _levelSystem.OnLevelSelectionOpened -= HandleLevelSelectionOpened;
            }

            if (_gangshin != null)
            {
                _gangshin.OnActivated -= HandleGangshinActivated;
            }

            if (_waves != null)
            {
                _waves.OnBossPhaseStarted -= HandleBossPhaseStarted;
                _waves.OnAllWavesCompleted -= HandleRunEnded;
            }

            if (_gameOver != null)
            {
                _gameOver.OnGameOver -= HandleRunEnded;
            }

            _attack = null;
            _soulCollector = null;
            _levelSystem = null;
            _gangshin = null;
            _waves = null;
            _gameOver = null;
        }

        private static bool TryFind<T>(out T found) where T : Object
        {
            found = SceneObjectFinder.Find<T>();
            return found != null;
        }

        private static void HandleAttackExecuted(SwipeDirection direction, Vector2 origin)
        {
            AudioManager.PlaySfx(AudioCue.Swipe);
        }

        private static void HandleEnemyDamaged(EnemyHealth enemy, float amount)
        {
            AudioManager.PlaySfx(AudioCue.EnemyHit);
        }

        private static void HandleEnemyDied(EnemyHealth enemy)
        {
            AudioManager.PlaySfx(AudioCue.EnemyDeath);
        }

        private static void HandleSoulCollected(int amount)
        {
            AudioManager.PlaySfx(AudioCue.SoulCollect);
        }

        private static void HandleLevelSelectionOpened(int level, IReadOnlyList<SkillData> choices)
        {
            AudioManager.PlaySfx(AudioCue.LevelUp);
        }

        private static void HandleGangshinActivated()
        {
            AudioManager.PlaySfx(AudioCue.GangshinActivate);
        }

        private static void HandleBossPhaseStarted()
        {
            AudioManager.PlayBgm(BgmTrack.Boss);
        }

        // 런이 끝나면(사망·정화 성공 모두) 결과 화면 위로 전투 BGM이 계속 울리지 않도록 조용한 트랙으로 내린다.
        private static void HandleRunEnded()
        {
            AudioManager.PlayBgm(BgmTrack.Lobby);
        }
    }
}
