using System;
using System.Collections;
using Mukseon.Core.Input;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 타락한 산군 보스 전투의 두뇌(#69, 1페이즈 핵심). 보스 프리팹에 부착한다.
    /// <list type="bullet">
    /// <item><see cref="BossEncounterDirector.OnBossEncounterStarted"/>로 전투를 시작한다(자기 인스턴스 한정).</item>
    /// <item>좌/우 화면 밖 대기 ↔ 화면 안 진입 포지셔닝. 화면 밖이면 <see cref="EnemyHealth.IsTargetable"/>=false → 타격 불가.</item>
    /// <item>페이즈별 패턴(돌진/발톱 할퀴기/포효)을 주기적으로 발동한다.</item>
    /// <item>카운터는 <see cref="SwipeInputDetector.OnSwipeDetected"/>를 직접 구독한다(본체 데미지 경로와 독립).</item>
    /// <item>포효는 부하 호랑이를 소환하고, 1페이즈에서는 전멸 전까지 다른 패턴을 멈춘다.</item>
    /// <item><see cref="BossHealthComponent.OnPhaseThresholdReached"/>는 플래그만 세우고, 실제 전환은 현재 패턴 종료 후 적용한다.</item>
    /// </list>
    /// 본체 공격은 별도 처리 없이, 보스가 화면 안(타격 가능)일 때 기존 스와이프 파이프라인
    /// (<see cref="SwipeAttackEventListener"/>)이 그대로 데미지를 준다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(BossHealthComponent))]
    public class MountainKingBossController : MonoBehaviour
    {
        private static readonly SwipeDirection[] CardinalDirections =
        {
            SwipeDirection.Up,
            SwipeDirection.Down,
            SwipeDirection.Left,
            SwipeDirection.Right
        };

        [Header("Data")]
        [SerializeField]
        private MountainKingBossPatternData _patternData;

        [Header("References")]
        [SerializeField]
        private BossEncounterDirector _encounterDirector;

        [SerializeField]
        private SwipeInputDetector _swipeInputDetector;

        [SerializeField]
        private MountainKingMinionSpawner _minionSpawner;

        [SerializeField]
        private PlayerHealth _playerHealth;

        [SerializeField, Tooltip("패턴 인디케이터 프리팹(BossPatternIndicator). 풀링으로 관리된다.")]
        private BossPatternIndicator _indicatorPrefab;

        [SerializeField, Tooltip("포지셔닝 기준 카메라. 비우면 Camera.main을 사용한다.")]
        private Camera _camera;

        [Header("Positioning")]
        [SerializeField, Tooltip("보스 홈 위치를 화면 오른쪽으로 둘지 여부(꺼지면 왼쪽).")]
        private bool _homeOnRightSide = true;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private EnemyHealth _bossEnemyHealth;
        private BossHealthComponent _bossHealth;
        private BossPatternIndicator _activeIndicator;
        private Coroutine _combatRoutine;

        private bool _combatActive;
        private bool _patternActive;
        private int _currentPhaseIndex;
        private int _pendingPhaseIndex = -1;
        private int _lastPatternIndex = -1;

        // 카운터 윈도우 상태.
        private BossPatternDefinition _activePattern;
        private SwipeDirection _activeCounterRequired = SwipeDirection.None;
        private bool _counterWindowOpen;
        private bool _counterResolved;

        /// <summary>페이즈 전환이 실제 적용된 순간 발행(인자: 진입한 페이즈 인덱스). 2페이즈 연출/패턴 훅.</summary>
        public event Action<int> OnPhaseChanged;

        public int CurrentPhaseIndex => _currentPhaseIndex;
        public bool IsCombatActive => _combatActive;

        private void Awake()
        {
            _bossEnemyHealth = GetComponent<EnemyHealth>();
            _bossHealth = GetComponent<BossHealthComponent>();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_encounterDirector != null)
            {
                _encounterDirector.OnBossEncounterStarted += HandleBossEncounterStarted;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnPhaseThresholdReached += HandlePhaseThresholdReached;
                _bossHealth.OnDefeated += HandleBossDefeated;
            }

            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.OnSwipeDetected += HandleSwipeDetected;
            }
        }

        private void OnDisable()
        {
            if (_encounterDirector != null)
            {
                _encounterDirector.OnBossEncounterStarted -= HandleBossEncounterStarted;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnPhaseThresholdReached -= HandlePhaseThresholdReached;
                _bossHealth.OnDefeated -= HandleBossDefeated;
            }

            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.OnSwipeDetected -= HandleSwipeDetected;
            }

            StopCombat();
        }

        private void ResolveReferences()
        {
            if (_encounterDirector == null)
            {
                _encounterDirector = FindAnyObjectByType<BossEncounterDirector>();
            }

            if (_swipeInputDetector == null)
            {
                _swipeInputDetector = FindAnyObjectByType<SwipeInputDetector>();
            }

            if (_playerHealth == null)
            {
                _playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (_minionSpawner == null)
            {
                _minionSpawner = FindAnyObjectByType<MountainKingMinionSpawner>();
            }
        }

        private void HandleBossEncounterStarted(EnemyHealth boss)
        {
            // 디렉터는 모든 보스 등장에 대해 발행하므로 자기 인스턴스인지 확인한다.
            if (boss != _bossEnemyHealth)
            {
                return;
            }

            BeginCombat();
        }

        private void BeginCombat()
        {
            if (_combatActive)
            {
                return;
            }

            string reason = null;
            if (_patternData == null || !_patternData.IsValid(out reason))
            {
                Debug.LogWarning($"[MountainKingBossController] 패턴 데이터가 없거나 무효하여 전투를 시작하지 않습니다: {reason ?? "데이터 없음"}", this);
                return;
            }

            _combatActive = true;
            _patternActive = false;
            _currentPhaseIndex = 0;
            _pendingPhaseIndex = -1;
            _lastPatternIndex = -1;

            // 보스를 홈(화면 밖) 대기 위치로 스냅하고 타격 불가로 둔다.
            transform.position = ResolveWaitPosition();
            SetTargetable(false);
            RollBossDirection();

            _combatRoutine = StartCoroutine(CombatLoop());
        }

        private void StopCombat()
        {
            _combatActive = false;
            _patternActive = false;
            _counterWindowOpen = false;

            if (_combatRoutine != null)
            {
                StopCoroutine(_combatRoutine);
                _combatRoutine = null;
            }

            HideIndicator();
        }

        private IEnumerator CombatLoop()
        {
            while (_combatActive && IsBossAlive())
            {
                TryApplyPendingPhase();

                float interval = _patternData.GetRandomCycleInterval(_currentPhaseIndex);
                float waited = 0f;
                while (waited < interval && _combatActive && IsBossAlive())
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (!_combatActive || !IsBossAlive())
                {
                    break;
                }

                BossPatternDefinition pattern = SelectPattern();
                if (pattern == null)
                {
                    yield return null;
                    continue;
                }

                yield return RunPattern(pattern);
            }

            StopCombat();
        }

        private IEnumerator RunPattern(BossPatternDefinition pattern)
        {
            _patternActive = true;
            _activePattern = pattern;

            // 매 패턴 시작 시 본체 방향을 새로 굴린다(돌진의 BossDirection 카운터가 매번 달라지도록).
            RollBossDirection();

            SwipeDirection rolled = pattern.CounterType == BossCounterType.PatternDirection
                ? RandomDirection()
                : SwipeDirection.None;
            SwipeDirection required = ResolveCounterDirection(pattern, _bossEnemyHealth.SwipeDirection, rolled);

            // 화면 안으로 진입 + 타격 가능.
            yield return MoveTo(ResolveAttackPosition(), _patternData.ApproachSpeed);
            SetTargetable(true);

            // 예고: 인디케이터 노출 + 카운터 윈도우 개방.
            SwipeDirection indicatorDir = required != SwipeDirection.None ? required : _bossEnemyHealth.SwipeDirection;
            ShowIndicator(indicatorDir, pattern.IndicatorOffset);

            _counterResolved = false;
            _activeCounterRequired = required;
            _counterWindowOpen = pattern.IsCounterable;

            float window = pattern.ResolvedCounterWindowSeconds;
            float elapsed = 0f;
            while (elapsed < window && !_counterResolved && _combatActive && IsBossAlive())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            _counterWindowOpen = false;
            HideIndicator();

            if (!_counterResolved && _combatActive && IsBossAlive())
            {
                // 패턴이 그대로 적중.
                if (pattern.Type == BossPatternType.Roar)
                {
                    SpawnMinions(pattern);
                }
                else
                {
                    ApplyUnhandledDamageToPlayer(pattern.UnhandledDamage);
                }
            }

            // 포효(1페이즈): 부하 전멸 전까지 다음 패턴 발동 정지.
            if (pattern.Type == BossPatternType.Roar && _minionSpawner != null)
            {
                while (_minionSpawner.HasActiveMinions && _combatActive && IsBossAlive())
                {
                    yield return null;
                }
            }

            // 화면 밖으로 복귀 + 타격 불가.
            yield return MoveTo(ResolveWaitPosition(), _patternData.RetreatSpeed);
            SetTargetable(false);

            if (pattern.RecoverSeconds > 0f)
            {
                yield return new WaitForSeconds(pattern.RecoverSeconds);
            }

            _activePattern = null;
            _patternActive = false;
            TryApplyPendingPhase();
        }

        private IEnumerator MoveTo(Vector3 target, float speed)
        {
            while ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = target;
        }

        // ── 카운터 ─────────────────────────────────────────────────────────

        private void HandleSwipeDetected(SwipeDirection direction, Vector2 endScreenPosition)
        {
            if (!_counterWindowOpen || _counterResolved)
            {
                return;
            }

            if (!IsCounterMatch(_activeCounterRequired, direction))
            {
                return;
            }

            _counterResolved = true;
            _counterWindowOpen = false;

            float bonus = ComputeCounterDamage(_activePattern, _patternData.CounterBonusMultiplier);
            if (bonus > 0f && _bossEnemyHealth != null)
            {
                _bossEnemyHealth.ApplyDamage(bonus, this);
            }

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[MountainKingBossController] 카운터 성공({direction}) — 패턴 취소 + 보너스 {bonus} 데미지.");
            }
#endif
        }

        private void ApplyUnhandledDamageToPlayer(float damage)
        {
            if (damage <= 0f || _playerHealth == null)
            {
                return;
            }

            _playerHealth.TakeDamage(damage, this);
        }

        // ── 부하 소환 ──────────────────────────────────────────────────────

        private void SpawnMinions(BossPatternDefinition pattern)
        {
            if (_minionSpawner == null || pattern.MinionCount <= 0)
            {
                return;
            }

            _minionSpawner.SpawnWave(transform.position, pattern.MinionCount);
        }

        // ── 페이즈 전환(지연 적용) ─────────────────────────────────────────

        private void HandlePhaseThresholdReached(int phaseIndex)
        {
            RequestPhaseTransition(phaseIndex);
        }

        private void HandleBossDefeated(BossHealthComponent _)
        {
            StopCombat();
        }

        // ── 포지셔닝 헬퍼 ──────────────────────────────────────────────────

        private Vector3 ResolveWaitPosition()
        {
            ResolveCameraExtents(out Vector3 center, out float hExt, out _);
            float x = center.x + (_homeOnRightSide ? 1f : -1f) * (hExt + _patternData.OffscreenMargin);
            return new Vector3(x, center.y, 0f);
        }

        private Vector3 ResolveAttackPosition()
        {
            ResolveCameraExtents(out Vector3 center, out float hExt, out _);
            float x = center.x + (_homeOnRightSide ? 1f : -1f) * Mathf.Max(0f, hExt - _patternData.OnscreenMargin);
            return new Vector3(x, center.y, 0f);
        }

        private void ResolveCameraExtents(out Vector3 center, out float horizontalExtent, out float verticalExtent)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null && _camera.orthographic)
            {
                verticalExtent = _camera.orthographicSize;
                horizontalExtent = verticalExtent * _camera.aspect;
                center = _camera.transform.position;
                center.z = 0f;
                return;
            }

            center = transform.position;
            verticalExtent = 5f;
            horizontalExtent = 9f;
        }

        // ── 인디케이터 ─────────────────────────────────────────────────────

        private void ShowIndicator(SwipeDirection direction, Vector2 offset)
        {
            if (_indicatorPrefab == null)
            {
                return;
            }

            Vector3 pos = transform.position + (Vector3)offset;

            if (_activeIndicator == null)
            {
                GameObject go = PoolManager.Instance != null
                    ? PoolManager.Instance.Get(_indicatorPrefab.gameObject, pos, Quaternion.identity)
                    : Instantiate(_indicatorPrefab.gameObject, pos, Quaternion.identity);
                _activeIndicator = go.GetComponent<BossPatternIndicator>();
            }

            if (_activeIndicator != null)
            {
                _activeIndicator.Show(direction, pos);
            }
        }

        private void HideIndicator()
        {
            if (_activeIndicator == null)
            {
                return;
            }

            _activeIndicator.Hide();

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(_activeIndicator.gameObject);
            }
            else
            {
                Destroy(_activeIndicator.gameObject);
            }

            _activeIndicator = null;
        }

        // ── 보스 본체 상태 ─────────────────────────────────────────────────

        private void SetTargetable(bool targetable)
        {
            if (_bossEnemyHealth != null)
            {
                _bossEnemyHealth.IsTargetable = targetable;
            }
        }

        private bool IsBossAlive()
        {
            return _bossEnemyHealth != null && _bossEnemyHealth.IsAlive;
        }

        private void RollBossDirection()
        {
            if (_bossEnemyHealth != null)
            {
                _bossEnemyHealth.SetSwipeDirection(RandomDirection());
            }
        }

        private static SwipeDirection RandomDirection()
        {
            return CardinalDirections[UnityEngine.Random.Range(0, CardinalDirections.Length)];
        }

        // ── 순수 로직 (테스트 seam) ────────────────────────────────────────

        /// <summary>카운터 종류에 따라 요구 입력 방향을 해석한다.</summary>
        internal static SwipeDirection ResolveCounterDirection(
            BossPatternDefinition pattern,
            SwipeDirection bossDirection,
            SwipeDirection rolledPatternDirection)
        {
            if (pattern == null)
            {
                return SwipeDirection.None;
            }

            switch (pattern.CounterType)
            {
                case BossCounterType.BossDirection:
                    return bossDirection;
                case BossCounterType.PatternDirection:
                    return rolledPatternDirection;
                default:
                    return SwipeDirection.None;
            }
        }

        /// <summary>요구 방향이 있고 입력이 그와 일치하면 카운터 성공.</summary>
        internal static bool IsCounterMatch(SwipeDirection required, SwipeDirection input)
        {
            return required != SwipeDirection.None && input == required;
        }

        /// <summary>카운터 성공 시 보스에 가하는 피해 = 패턴 기본 보너스 × 데이터 배율.</summary>
        internal static float ComputeCounterDamage(BossPatternDefinition pattern, float multiplier)
        {
            if (pattern == null)
            {
                return 0f;
            }

            return pattern.CounterBonusDamage * Mathf.Max(1f, multiplier);
        }

        /// <summary>현재 페이즈 패턴 목록에서 직전과 다른 패턴 인덱스를 고른다.</summary>
        internal int SelectNextPatternIndex(int patternCount, int lastIndex)
        {
            if (patternCount <= 0)
            {
                return -1;
            }

            if (patternCount == 1)
            {
                return 0;
            }

            int index;
            do
            {
                index = UnityEngine.Random.Range(0, patternCount);
            }
            while (index == lastIndex);

            return index;
        }

        private BossPatternDefinition SelectPattern()
        {
            BossPhaseDefinition phase = _patternData.GetPhase(_currentPhaseIndex);
            if (phase == null || phase.PatternCount == 0)
            {
                return null;
            }

            int index = SelectNextPatternIndex(phase.PatternCount, _lastPatternIndex);
            if (index < 0)
            {
                return null;
            }

            _lastPatternIndex = index;
            return phase.Patterns[index];
        }

        /// <summary>HP 임계값 도달 알림을 받아 전환 대기 플래그만 세운다(즉시 전환 안 함).</summary>
        internal void RequestPhaseTransition(int phaseIndex)
        {
            if (phaseIndex > _pendingPhaseIndex)
            {
                _pendingPhaseIndex = phaseIndex;
            }
        }

        /// <summary>
        /// 대기 중인 페이즈 전환을 적용할 수 있으면 적용한다. 현재 패턴이 진행 중이면 적용을 미룬다.
        /// 실제 적용이 일어나면 true.
        /// </summary>
        internal bool TryApplyPendingPhase()
        {
            if (_patternActive || _pendingPhaseIndex <= _currentPhaseIndex)
            {
                return false;
            }

            _currentPhaseIndex = _pendingPhaseIndex;
            _pendingPhaseIndex = -1;
            OnPhaseChanged?.Invoke(_currentPhaseIndex);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[MountainKingBossController] {_currentPhaseIndex + 1}페이즈로 전환.");
            }
#endif
            return true;
        }

        // ── 테스트 전용 ────────────────────────────────────────────────────

        internal int PendingPhaseIndexForTests => _pendingPhaseIndex;
        internal bool PatternActiveForTests
        {
            get => _patternActive;
            set => _patternActive = value;
        }

        internal void ConfigureForTests(MountainKingBossPatternData data, int currentPhaseIndex = 0)
        {
            _patternData = data;
            _currentPhaseIndex = currentPhaseIndex;
            _pendingPhaseIndex = -1;
            _patternActive = false;
        }
    }
}
