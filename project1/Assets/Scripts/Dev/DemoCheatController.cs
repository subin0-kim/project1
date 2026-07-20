// 시연/개발 전용(#111). 출시 빌드에는 이 파일의 내용이 통째로 빠진다.
// 파일 전체를 감싸는 이유: 클래스 선언까지 제외해야 다른 코드가 실수로 참조해 출시 빌드를 깨뜨리는 일이 없다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mukseon.Dev
{
    /// <summary>
    /// 시연용 디버그 치트 실행부(#111).
    ///
    /// 10분 런 + 보스라는 설계를 시연 부스에서 그대로 굴릴 수 없어서 존재한다. 대부분의 치트는
    /// 이미 있는 public API를 키에 연결한 것뿐이고, 보스 점프만 <see cref="WaveCombatDirector.SkipToBossPhase"/>를
    /// 새로 필요로 했다.
    ///
    /// GameplayInputGate/GameplayHudBootstrapper와 같은 자가 부트스트랩 패턴을 쓴다 — 씬을 편집하지 않으므로
    /// 씬 파일에 시연용 오브젝트가 커밋되는 일이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoCheatController : MonoBehaviour
    {
        private const string RootObjectName = "DemoCheatRuntime";

        /// <summary>즉시 레벨업 시 임계치를 확실히 넘기기 위한 여유분.</summary>
        private const float LevelUpExperienceMargin = 1f;

        private static DemoCheatController _instance;

        private PlayerHealth _playerHealth;
        private PlayerLevelSystem _playerLevelSystem;
        private GangshinController _gangshinController;
        private WaveCombatDirector _waveCombatDirector;

        private readonly List<EnemyHealth> _killBuffer = new List<EnemyHealth>(64);

        private DemoCheatOverlay _overlay;

        /// <summary>
        /// 무적 치트가 켜져 있는지. 오버레이 표시에 사용.
        ///
        /// 별도 bool을 캐시하지 않고 <see cref="PlayerHealth.IsInvincible"/>을 그대로 읽는다 —
        /// 상태를 두 곳에 두면 어긋날 수 있고, 실제 무적 여부의 소유자는 PlayerHealth다.
        /// 여기서는 지연 해석을 하지 않는다(오버레이가 매 프레임 호출하므로 씬 탐색이 반복되면 안 된다).
        /// </summary>
        public bool IsInvincible => _playerHealth != null && _playerHealth.IsInvincible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureController()
        {
            if (SceneObjectFinder.Find<DemoCheatController>() != null)
            {
                return;
            }

            var root = new GameObject(RootObjectName);
            root.AddComponent<DemoCheatController>();
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
            SceneManager.sceneLoaded += HandleSceneLoaded;

            _overlay = new DemoCheatOverlay(this);
            ResolveSources();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _overlay?.Dispose();
            _overlay = null;
        }

        // 씬이 바뀌면 이전 씬의 참조가 전부 무효해지므로 다시 해석한다.
        //
        // 무적 상태를 여기서 따로 끄지 않는 이유: 무적 여부의 소유자가 PlayerHealth이고 이 컨트롤러는
        // 캐시를 두지 않으므로, 새 씬의 PlayerHealth는 애초에 무적이 아닌 상태로 시작한다.
        // 애디티브 로드로 이전 플레이어가 살아남는 경우에도 IsInvincible이 실제 상태를 그대로 보고하므로
        // 표시와 실제가 어긋나지 않는다.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _playerHealth = null;
            _playerLevelSystem = null;
            _gangshinController = null;
            _waveCombatDirector = null;
            ResolveSources();
        }

        private void ResolveSources()
        {
            _playerHealth = SceneObjectFinder.Find<PlayerHealth>();
            _playerLevelSystem = SceneObjectFinder.Find<PlayerLevelSystem>();
            _gangshinController = SceneObjectFinder.Find<GangshinController>();
            _waveCombatDirector = SceneObjectFinder.Find<WaveCombatDirector>();
        }

        // 치트 실행 시점의 지연 재해석용 접근자(PR #113 리뷰 반영).
        //
        // 씬 로드 직후에 해석해 캐시하지만, 플레이어나 디렉터가 씬 로드 이후에 동적으로 생성되면
        // 캐시가 비어 있는 채로 남아 치트가 조용히 먹지 않는다. 실제로 눌렀을 때 한 번 더 찾아본다.
        //
        // Unity의 == 오버로드가 파괴된 오브젝트도 null로 판정하므로, 이전 참조가 파괴된 경우에도
        // 자동으로 다시 해석된다. 비용은 캐시가 비었을 때(=어차피 no-op이었을 때)만 발생한다.
        private PlayerHealth Health => ResolveLazily(ref _playerHealth);
        private PlayerLevelSystem Levels => ResolveLazily(ref _playerLevelSystem);
        private GangshinController Gangshin => ResolveLazily(ref _gangshinController);
        private WaveCombatDirector Waves => ResolveLazily(ref _waveCombatDirector);

        private static T ResolveLazily<T>(ref T cached) where T : Object
        {
            if (cached == null)
            {
                cached = SceneObjectFinder.Find<T>();
            }

            return cached;
        }

        // 게임이 정지(timeScale 0)돼 있어도 치트는 먹어야 하므로 Update에서 직접 키를 읽는다.
        private void Update()
        {
            IReadOnlyList<DemoCheatBindings.Binding> bindings = DemoCheatBindings.All;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (Input.GetKeyDown(bindings[i].Key))
                {
                    Execute(bindings[i].Action);
                }
            }

            _overlay?.Update();
        }

        private void Execute(DemoCheatAction action)
        {
            switch (action)
            {
                case DemoCheatAction.ToggleInvincible:
                    ToggleInvincible();
                    break;
                case DemoCheatAction.LevelUp:
                    LevelUp();
                    break;
                case DemoCheatAction.KillEnemies:
                    KillEnemies();
                    break;
                case DemoCheatAction.ActivateGangshin:
                    ActivateGangshin();
                    break;
                case DemoCheatAction.GrantGangshinSlot:
                    GrantGangshinSlot();
                    break;
                case DemoCheatAction.SkipToBoss:
                    SkipToBoss();
                    break;
                case DemoCheatAction.ToggleOverlay:
                    _overlay?.ToggleVisible();
                    break;
            }
        }

        private void ToggleInvincible()
        {
            PlayerHealth health = Health;
            if (health == null)
            {
                return;
            }

            // 현재 값을 PlayerHealth에서 직접 읽어 뒤집는다 — 별도 캐시를 두지 않으므로 어긋날 여지가 없다.
            health.SetInvincible(!health.IsInvincible);
        }

        private void LevelUp()
        {
            PlayerLevelSystem levels = Levels;
            if (levels == null)
            {
                return;
            }

            // 임계치까지 남은 만큼만 넣어 정확히 1레벨만 오르게 한다(큰 값을 넣으면 여러 번 겹쳐 오른다).
            float remaining = levels.CurrentThreshold - levels.CurrentExperience;
            levels.AddExperience(Mathf.Max(0f, remaining) + LevelUpExperienceMargin);
        }

        private void KillEnemies()
        {
            // ActiveEnemies는 사망 처리 중 변경되므로 버퍼에 복사한 뒤 순회한다.
            _killBuffer.Clear();
            _killBuffer.AddRange(EnemyHealth.ActiveEnemies);

            for (int i = 0; i < _killBuffer.Count; i++)
            {
                EnemyHealth enemy = _killBuffer[i];
                if (enemy != null && enemy.IsAlive)
                {
                    enemy.Kill();
                }
            }

            _killBuffer.Clear();
        }

        private void ActivateGangshin()
        {
            GangshinController gangshin = Gangshin;
            if (gangshin == null)
            {
                return;
            }

            gangshin.AddGauge(gangshin.MaxGauge);
            gangshin.TryActivate();
        }

        // 강신 어빌리티는 SO가 아니라 씬에 배치된 MonoBehaviour라, 씬에서 찾아 미장착인 것을 슬롯에 넣는다.
        private void GrantGangshinSlot()
        {
            GangshinController gangshin = Gangshin;
            if (gangshin == null || !gangshin.HasFreeSlot)
            {
                return;
            }

            GangshinAbilityBase[] abilities =
                Object.FindObjectsByType<GangshinAbilityBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < abilities.Length; i++)
            {
                // 같은 프레임에 파괴된 오브젝트가 섞여 올 수 있어 null 판정을 먼저 한다.
                if (abilities[i] == null || IsEquipped(gangshin, abilities[i]))
                {
                    continue;
                }

                if (gangshin.TryAddAbility(abilities[i]) >= 0)
                {
                    return;
                }
            }
        }

        private static bool IsEquipped(GangshinController gangshin, GangshinAbilityBase ability)
        {
            IReadOnlyList<GangshinSlot> slots = gangshin.Slots;
            if (slots == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Ability == ability)
                {
                    return true;
                }
            }

            return false;
        }

        private void SkipToBoss()
        {
            WaveCombatDirector waves = Waves;
            if (waves != null)
            {
                waves.SkipToBossPhase();
            }
        }
    }
}

#endif
