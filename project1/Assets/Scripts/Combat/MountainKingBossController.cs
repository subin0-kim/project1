using System;
using System.Collections;
using Mukseon.Core.Input;
using Mukseon.Gameplay.UI;
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

        [SerializeField, Tooltip("포지셔닝 기준 카메라. 비우면 Camera.main을 사용한다.")]
        private Camera _camera;

        [Header("Positioning")]
        [SerializeField, Tooltip("등장 연출 시 보스가 진입하는 기본 홈 방향(오른쪽). 패턴은 매번 좌/우를 새로 고른다.")]
        private bool _homeOnRightSide = true;

        [SerializeField, Tooltip("패턴마다 좌/우를 랜덤으로 골라 양쪽에서 등장하게 할지 여부. 끄면 항상 홈 방향에서만 등장.")]
        private bool _randomizePatternSide = true;

        [SerializeField, Tooltip("보스가 항상 화면 중앙(플레이어)을 바라보도록 side에 맞춰 스프라이트를 좌우 반전. 기본 스프라이트는 왼쪽을 향한다고 가정.")]
        private bool _flipSpriteToFaceCenter = true;

        [Header("Entrance")]
        [SerializeField, Min(0f), Tooltip("등장 연출 — 플레이어 기준 오른쪽으로 보스가 설 거리(월드 단위). 플레이어 참조가 없으면 화면 중앙 우측으로 폴백.")]
        private float _entranceStandOffsetX = 2.5f;

        [SerializeField, Min(0.1f), Tooltip("등장 연출 이동 속도(월드 단위/초). 패턴 진입보다 느리게 두어 출현을 또렷이 보여준다.")]
        private float _entranceMoveSpeed = 6f;

        [SerializeField, Min(0f), Tooltip("등장 연출 — 플레이어 옆에 멈춰 출현을 알리는 시간(초).")]
        private float _entrancePauseSeconds = 1f;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private EnemyHealth _bossEnemyHealth;
        private BossHealthComponent _bossHealth;
        private SpriteRenderer _spriteRenderer;
        private Coroutine _combatRoutine;

        // 현재 보스가 자리잡은 화면 방향(오른쪽=true). 패턴마다 갱신되며 포지셔닝/미러링의 기준이 된다.
        private bool _currentOnRightSide = true;

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
        private float _counterDamageAccumulated;

        /// <summary>페이즈 전환이 실제 적용된 순간 발행(인자: 진입한 페이즈 인덱스). 2페이즈 연출/패턴 훅.</summary>
        public event Action<int> OnPhaseChanged;

        /// <summary>페이즈 전환 연출 시작 시 발행. 포효/타락 VFX·SFX·애니메이션을 여기에 연결한다(데이터/연출 분리).</summary>
        public event Action OnPhaseTransitionStarted;

        /// <summary>페이즈 전환 연출 종료 시 발행(무적 해제·패턴 재개 직전).</summary>
        public event Action OnPhaseTransitionEnded;

        public int CurrentPhaseIndex => _currentPhaseIndex;
        public bool IsCombatActive => _combatActive;

        private void Awake()
        {
            _bossEnemyHealth = GetComponent<EnemyHealth>();
            _bossHealth = GetComponent<BossHealthComponent>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _currentOnRightSide = _homeOnRightSide;
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_encounterDirector != null)
            {
                _encounterDirector.OnBossSpawned += HandleBossSpawned;
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

            if (_bossEnemyHealth != null)
            {
                _bossEnemyHealth.OnDamagedDetailed += HandleBossDamaged;
            }
        }

        private void OnDisable()
        {
            if (_encounterDirector != null)
            {
                _encounterDirector.OnBossSpawned -= HandleBossSpawned;
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

            if (_bossEnemyHealth != null)
            {
                _bossEnemyHealth.OnDamagedDetailed -= HandleBossDamaged;
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

        // 스폰 직후(아직 무적 대기 중): 플레이어 옆에 보이지 않도록 즉시 화면 밖으로 숨기고 체력바도 가린다.
        // 실제 등장 연출은 전투 시작(OnBossEncounterStarted) 시 재생한다.
        private void HandleBossSpawned(EnemyHealth boss)
        {
            if (boss != _bossEnemyHealth)
            {
                return;
            }

            SetSide(_homeOnRightSide);
            transform.position = ResolveWaitPosition();
            SetTargetable(false);
            GameplayHudBootstrapper.Instance?.SuppressBossHealthBar(true);
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

            // 보스를 홈(화면 밖) 대기 위치로 스냅하고 타격 불가로 둔다. 등장 연출은 홈 방향에서 진입한다.
            SetSide(_homeOnRightSide);
            transform.position = ResolveWaitPosition();
            SetTargetable(false);
            RollBossDirection();

            // 등장 연출 → 전투 루프 순으로 진행한다.
            _combatRoutine = StartCoroutine(EncounterRoutine());
        }

        private IEnumerator EncounterRoutine()
        {
            yield return EntranceRoutine();
            yield return CombatLoop();
        }

        // 등장 연출(#69): 화면 밖 → 플레이어 옆으로 진입(이동하며 체력바 채움) → 잠시 멈춰 출현 알림 → 화면 밖 복귀.
        // 연출 동안 보스는 타격 불가이며, 체력바는 실제 HP가 아닌 연출용 채움 비율로 표시된다.
        private IEnumerator EntranceRoutine()
        {
            SetTargetable(false);
            transform.position = ResolveWaitPosition();

            GameplayHudBootstrapper hud = GameplayHudBootstrapper.Instance;
            hud?.SetBossHealthRevealRatio(0f);

            // 1) 화면 안 플레이어 옆으로 진입하며 체력바를 0 → 100% 채운다.
            yield return MoveInWithHealthReveal(ResolveIntroPosition(), _entranceMoveSpeed);
            hud?.SetBossHealthRevealRatio(1f);

            // 2) 출현을 알리는 짧은 정지.
            if (_entrancePauseSeconds > 0f)
            {
                yield return new WaitForSeconds(_entrancePauseSeconds);
            }

            // 체력바를 실제 HP 기반 표시로 전환한다.
            hud?.EndBossHealthReveal();

            // 3) 다시 화면 밖으로 복귀(이후 패턴 진행).
            yield return MoveTo(ResolveWaitPosition(), _entranceMoveSpeed);
        }

        // 이동 진행도에 맞춰 보스 체력바를 채우며 목표 위치로 이동한다.
        private IEnumerator MoveInWithHealthReveal(Vector3 target, float speed)
        {
            GameplayHudBootstrapper hud = GameplayHudBootstrapper.Instance;
            float total = (target - transform.position).magnitude;

            while ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

                float progress = total > 0.0001f
                    ? 1f - (transform.position - target).magnitude / total
                    : 1f;
                hud?.SetBossHealthRevealRatio(Mathf.Clamp01(progress));
                yield return null;
            }

            transform.position = target;
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
            GameplayHudBootstrapper.Instance?.EndBossHealthReveal();
        }

        private IEnumerator CombatLoop()
        {
            while (_combatActive && IsBossAlive())
            {
                // 대기 중인 페이즈 전환이 있으면 연출을 먼저 재생한 뒤 적용한다(패턴 사이에서만).
                yield return TransitionIfPending();

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

            // 매 패턴 시작 시 등장 측면을 새로 고른다(좌/우 양쪽에서 출현). 포지셔닝/미러링의 기준이 된다.
            SetSide(RollPatternSide());

            // 새 측면의 화면 밖 대기 위치로 즉시 스냅(화면 밖이라 비가시) — 측면이 바뀌어도 화면을 가로지르지 않고
            // 항상 해당 측면 밖에서 진입하도록 한다.
            transform.position = ResolveWaitPosition();

            // 매 패턴 시작 시 본체 방향을 새로 굴린다(돌진의 BossDirection 카운터가 매번 달라지도록).
            RollBossDirection();

            SwipeDirection rolled = pattern.CounterType == BossCounterType.PatternDirection
                ? RandomDirection()
                : SwipeDirection.None;
            SwipeDirection required = ResolveCounterDirection(pattern, _bossEnemyHealth.SwipeDirection, rolled);

            // 화면 안으로 진입 + 타격 가능. 페이즈가 오를수록 진입이 빨라진다(BossData 페이즈 이동 배율).
            yield return MoveTo(ResolveAttackPosition(), _patternData.ApproachSpeed * PhaseMoveMultiplier());
            SetTargetable(true);

            // 예고: 카운터 방향이 본체와 독립인 패턴(할퀴기)만 인디케이터를 노출한다.
            // 돌진은 본체 색이 곧 카운터 색, 포효는 카운터가 없어 인디케이터가 불필요하다.
            if (pattern.ShowsIndicator)
            {
                SwipeDirection indicatorDir = required != SwipeDirection.None ? required : _bossEnemyHealth.SwipeDirection;
                ShowIndicator(indicatorDir, MirrorOffsetForSide(pattern.IndicatorOffset, _currentOnRightSide));
            }

            _counterResolved = false;
            _activeCounterRequired = required;
            _counterDamageAccumulated = 0f;
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
            yield return MoveTo(ResolveWaitPosition(), _patternData.RetreatSpeed * PhaseMoveMultiplier());
            SetTargetable(false);

            if (pattern.RecoverSeconds > 0f)
            {
                yield return new WaitForSeconds(pattern.RecoverSeconds);
            }

            _activePattern = null;
            _patternActive = false;
            // 페이즈 전환은 다음 루프 반복의 TransitionIfPending에서 연출과 함께 적용한다.
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
            if (!_counterWindowOpen || _counterResolved || _activePattern == null)
            {
                return;
            }

            // 방향 기반 카운터만 스와이프로 파훼된다. 돌진(DamageThreshold)은 피해 누적으로 처리한다.
            if (_activePattern.CounterType == BossCounterType.DamageThreshold)
            {
                return;
            }

            if (!IsCounterMatch(_activeCounterRequired, direction))
            {
                return;
            }

            ResolveCounterSuccess($"({direction})");
        }

        // 돌진(DamageThreshold) 카운터: 윈도우 동안 보스에 누적된 피해가 임계치에 도달하면 파훼한다(#69).
        // 본체를 계속 때리면 자동 파훼되던 방향 카운터와 달리, 일정 피해를 강제해 의미를 부여한다.
        private void HandleBossDamaged(EnemyHealth enemy, float amount, object source)
        {
            if (!_counterWindowOpen || _counterResolved || _activePattern == null)
            {
                return;
            }

            if (_activePattern.CounterType != BossCounterType.DamageThreshold)
            {
                return;
            }

            // 컨트롤러가 가한 카운터 보너스 피해는 누적에서 제외(자기 참조).
            if (ReferenceEquals(source, this))
            {
                return;
            }

            _counterDamageAccumulated += Mathf.Max(0f, amount);
            if (_counterDamageAccumulated >= _activePattern.CounterDamageThreshold)
            {
                ResolveCounterSuccess($"(피해 누적 {_counterDamageAccumulated:0.#})");
            }
        }

        // 카운터 성공 공통 처리: 윈도우를 닫고 패턴을 취소하며, 설정된 보너스 피해를 보스에 가한다.
        private void ResolveCounterSuccess(string debugContext)
        {
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
                Debug.Log($"[MountainKingBossController] 카운터 성공{debugContext} — 패턴 취소 + 보너스 {bonus} 데미지.");
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

        // 대기 중인 페이즈 전환이 있으면 연출을 재생한 뒤 실제 전환을 적용한다(패턴 사이에서만 호출).
        private IEnumerator TransitionIfPending()
        {
            if (_patternActive || _pendingPhaseIndex <= _currentPhaseIndex)
            {
                yield break;
            }

            yield return PhaseTransitionRoutine();
            TryApplyPendingPhase();
        }

        // 2페이즈 전환 연출(#69): 본체를 화면 안으로 들여 무적 상태로 포효 연출을 보여준 뒤 화면 밖으로 복귀한다.
        // 실제 포효/타락 VFX·SFX·애니메이션은 OnPhaseTransition* 이벤트에 연결한다(데이터/연출 분리).
        private IEnumerator PhaseTransitionRoutine()
        {
            // 연출 동안 다른 전환/패턴 적용을 막고, 인디케이터를 정리하며, 타격 불가(무적)로 둔다.
            _patternActive = true;
            HideIndicator();
            SetTargetable(false);

            // 현재 측면 화면 안으로 진입해 연출을 노출한다.
            SetSide(_currentOnRightSide);
            transform.position = ResolveWaitPosition();
            yield return MoveTo(ResolveAttackPosition(), _patternData.ApproachSpeed * PhaseMoveMultiplier());

            OnPhaseTransitionStarted?.Invoke();

            float cutscene = _patternData.PhaseTransitionCutsceneSeconds;
            if (cutscene > 0f)
            {
                yield return new WaitForSeconds(cutscene);
            }

            OnPhaseTransitionEnded?.Invoke();

            // 화면 밖으로 복귀(계속 타격 불가). 이후 다음 패턴이 새 페이즈 속도로 진입한다.
            yield return MoveTo(ResolveWaitPosition(), _patternData.RetreatSpeed * PhaseMoveMultiplier());
            SetTargetable(false);

            _patternActive = false;
        }

        // 현재 페이즈의 이동 속도 배율(BossData.PhaseMoveSpeeds). 데이터가 없으면 1배.
        private float PhaseMoveMultiplier()
        {
            BossData data = _bossHealth != null ? _bossHealth.BossData : null;
            return data != null ? data.GetPhaseMoveSpeed(_currentPhaseIndex) : 1f;
        }

        private void HandleBossDefeated(BossHealthComponent _)
        {
            StopCombat();
        }

        // ── 포지셔닝 헬퍼 ──────────────────────────────────────────────────

        private Vector3 ResolveWaitPosition()
        {
            ResolveCameraExtents(out Vector3 center, out float hExt, out _);
            float x = center.x + (_currentOnRightSide ? 1f : -1f) * (hExt + _patternData.OffscreenMargin);
            return new Vector3(x, center.y, 0f);
        }

        private Vector3 ResolveAttackPosition()
        {
            ResolveCameraExtents(out Vector3 center, out float hExt, out _);
            float x = center.x + (_currentOnRightSide ? 1f : -1f) * Mathf.Max(0f, hExt - _patternData.OnscreenMargin);
            return new Vector3(x, center.y, 0f);
        }

        // 보스가 자리잡을 화면 방향을 설정하고, 항상 중앙(플레이어)을 바라보도록 스프라이트를 반전한다.
        private void SetSide(bool onRight)
        {
            _currentOnRightSide = onRight;

            if (_flipSpriteToFaceCenter && _spriteRenderer != null)
            {
                // 기본 스프라이트가 왼쪽을 향한다고 가정 — 오른쪽 측면이면 그대로(왼쪽=중앙), 왼쪽 측면이면 반전.
                _spriteRenderer.flipX = !onRight;
            }
        }

        // 패턴 시작 측면을 고른다. 랜덤이 꺼져 있으면 홈 방향을 유지한다.
        private bool RollPatternSide()
        {
            return _randomizePatternSide ? UnityEngine.Random.value >= 0.5f : _homeOnRightSide;
        }

        // 측면에 맞춰 인디케이터 오프셋을 미러링한다(오프셋은 오른쪽 기준으로 작성됨). 왼쪽이면 x만 반전.
        internal static Vector2 MirrorOffsetForSide(Vector2 offset, bool onRight)
        {
            return onRight ? offset : new Vector2(-offset.x, offset.y);
        }

        // 등장 연출에서 보스가 멈춰 서는 위치 — 플레이어 오른쪽. 플레이어 참조가 없으면 화면 중앙 우측으로 폴백.
        private Vector3 ResolveIntroPosition()
        {
            if (_playerHealth != null)
            {
                Vector3 p = _playerHealth.transform.position;
                return new Vector3(p.x + _entranceStandOffsetX, p.y, 0f);
            }

            ResolveCameraExtents(out Vector3 center, out float hExt, out _);
            return new Vector3(center.x + hExt * 0.4f, center.y, 0f);
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

        // 인디케이터 색 오브는 일반 적 머리 위 색 오브와 동일한 HUD 요소를 재사용한다(#69).
        // 보스 본체 위치(앵커) + 패턴 오프셋 상단에 떠서, 접근성(화살표) 모드도 HUD 한 곳에서 처리된다(#83).
        private void ShowIndicator(SwipeDirection direction, Vector2 offset)
        {
            GameplayHudBootstrapper.Instance?.ShowPatternOrb(transform, offset, direction);
        }

        private void HideIndicator()
        {
            GameplayHudBootstrapper.Instance?.HidePatternOrb();
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
