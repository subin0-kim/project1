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
        private bool _isInvincible;

        /// <summary>무적 치트가 켜져 있는지. 오버레이 표시에 사용.</summary>
        public bool IsInvincible => _isInvincible;

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
        // 무적 상태도 새 런에 자동으로 따라가지 않게 해제한다 — 시연자가 껐다고 착각하는 편이 위험하다.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _playerHealth = null;
            _playerLevelSystem = null;
            _gangshinController = null;
            _waveCombatDirector = null;
            _isInvincible = false;
            ResolveSources();
        }

        private void ResolveSources()
        {
            _playerHealth = SceneObjectFinder.Find<PlayerHealth>();
            _playerLevelSystem = SceneObjectFinder.Find<PlayerLevelSystem>();
            _gangshinController = SceneObjectFinder.Find<GangshinController>();
            _waveCombatDirector = SceneObjectFinder.Find<WaveCombatDirector>();
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
            if (_playerHealth == null)
            {
                return;
            }

            _isInvincible = !_isInvincible;
            _playerHealth.SetInvincible(_isInvincible);
        }

        private void LevelUp()
        {
            if (_playerLevelSystem == null)
            {
                return;
            }

            // 임계치까지 남은 만큼만 넣어 정확히 1레벨만 오르게 한다(큰 값을 넣으면 여러 번 겹쳐 오른다).
            float remaining = _playerLevelSystem.CurrentThreshold - _playerLevelSystem.CurrentExperience;
            _playerLevelSystem.AddExperience(Mathf.Max(0f, remaining) + LevelUpExperienceMargin);
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
            if (_gangshinController == null)
            {
                return;
            }

            _gangshinController.AddGauge(_gangshinController.MaxGauge);
            _gangshinController.TryActivate();
        }

        // 강신 어빌리티는 SO가 아니라 씬에 배치된 MonoBehaviour라, 씬에서 찾아 미장착인 것을 슬롯에 넣는다.
        private void GrantGangshinSlot()
        {
            if (_gangshinController == null || !_gangshinController.HasFreeSlot)
            {
                return;
            }

            GangshinAbilityBase[] abilities =
                Object.FindObjectsByType<GangshinAbilityBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < abilities.Length; i++)
            {
                if (IsEquipped(abilities[i]))
                {
                    continue;
                }

                if (_gangshinController.TryAddAbility(abilities[i]) >= 0)
                {
                    return;
                }
            }
        }

        private bool IsEquipped(GangshinAbilityBase ability)
        {
            IReadOnlyList<GangshinSlot> slots = _gangshinController.Slots;
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
            _waveCombatDirector?.SkipToBossPhase();
        }
    }
}

#endif
