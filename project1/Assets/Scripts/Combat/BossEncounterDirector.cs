using System;
using System.Collections;
using Mukseon.Core;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// <see cref="WaveCombatDirector.OnBossPhaseStarted"/>를 구독하여 보스 등장 시퀀스를 오케스트레이션한다(#37).
    /// <list type="number">
    /// <item>남은 일반 적 즉시 제거(풀 반환)</item>
    /// <item>보스 스폰(등장 연출 동안 무적)</item>
    /// <item>등장 연출 대기 후 무적 해제 + <see cref="OnBossEncounterStarted"/> 발행</item>
    /// <item>보스 처치 시 사망 연출 후 <see cref="OnChapterCleared"/> 발행</item>
    /// </list>
    /// 타락한 산군의 패턴/카운터(#69)는 <see cref="OnBossEncounterStarted"/>를 구독해 전투를 시작하고,
    /// 결과 화면 전환(#36)은 <see cref="OnChapterCleared"/>를 구독해 연결한다.
    /// 구조는 <see cref="MiniBossSpawner"/>의 스폰/풀링 패턴을 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossEncounterDirector : MonoBehaviour
    {
        [Header("Chapter")]
        [Tooltip("이 챕터의 메인 보스를 사용한다(#64). 비우면 아래 보스 프리팹을 그대로 쓴다. " +
                 "런타임에는 스테이지 선택 결과(RunContext.SelectedChapter)가 이 값보다 우선한다.")]
        [SerializeField]
        private ChapterData _chapterData;

        [Header("References")]
        [SerializeField]
        private WaveCombatDirector _director;

        [SerializeField, Tooltip("EnemyHealth + BossHealthComponent를 포함한 보스 프리팹.")]
        private EnemyHealth _bossPrefab;

        [SerializeField, Tooltip("보스 스폰 위치. 비우면 카메라 중앙 기준으로 폴백한다. (좌/우 포지셔닝은 #69)")]
        private Transform _bossSpawnPoint;

        [Header("Encounter Flow")]
        [SerializeField, Min(0f), Tooltip("등장 연출 시간(초). 이 동안 보스는 무적이며 패턴은 시작되지 않는다.")]
        private float _introDurationSeconds = 2f;

        [SerializeField, Tooltip("등장 연출 동안 보스를 무적 처리할지 여부.")]
        private bool _invincibleDuringIntro = true;

        [SerializeField, Min(0f), Tooltip("사망 연출 시간(초). 이후 챕터 클리어를 발행한다.")]
        private float _deathDelaySeconds = 1.5f;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private EnemyHealth _activeBoss;
        private BossHealthComponent _activeBossHealth;
        private bool _encounterStarted;

        public EnemyHealth ActiveBoss => _activeBoss;
        public BossHealthComponent ActiveBossHealth => _activeBossHealth;

        /// <summary>보스가 스폰된 직후 발행(연출 시작 시점, 아직 무적일 수 있음).</summary>
        public event Action<EnemyHealth> OnBossSpawned;

        /// <summary>등장 연출 종료 후 무적이 해제되어 전투가 시작될 때 발행. #69 보스 컨트롤러가 구독.</summary>
        public event Action<EnemyHealth> OnBossEncounterStarted;

        /// <summary>보스 처치 + 사망 연출 종료 후 발행. 결과 화면(#36)이 구독한다.</summary>
        public event Action OnChapterCleared;

        /// <summary>
        /// 보스 프리팹은 보스 마크(기본 10분)에 가서야 읽히므로 해석 시점에 여유가 있지만,
        /// 나머지 두 디렉터와 같은 규약을 유지해 <c>Awake</c>에서 한 번에 끝낸다.
        /// </summary>
        private void Awake()
        {
            ApplyChapterData();
        }

        /// <summary>
        /// 반영 단위는 <b>챕터 통째로</b>다 — 프리팹만 씬 값으로 남기면 "2장인데 보스는 1장 산군"이 되므로,
        /// 검증에 걸리는 챕터는 통째로 무시한다(<see cref="WaveCombatDirector"/>와 동일한 판정).
        /// </summary>
        private void ApplyChapterData()
        {
            ChapterData chapter = RunContext.SelectedChapter != null ? RunContext.SelectedChapter : _chapterData;
            if (chapter == null)
            {
                return;
            }

            if (!chapter.IsValid(out string reason))
            {
                Debug.LogWarning(
                    $"[BossEncounterDirector] 챕터 '{chapter.DisplayName}'가 유효하지 않아 씬 설정으로 진행합니다: {reason}");
                return;
            }

            _chapterData = chapter;

            // IsValid가 "보스 마크가 있으면 프리팹도 있다"를 보장한다. 그래도 프리팹이 비어 있다면 보스 마크가 0인
            // 보스 없는 챕터이므로, 씬 값을 남기지 않고 그대로 비운다 — 어차피 보스 페이즈 자체가 시작되지 않는다.
            _bossPrefab = chapter.BossPrefab;
        }

        private void OnEnable()
        {
            if (_director != null)
            {
                _director.OnBossPhaseStarted += HandleBossPhaseStarted;
            }
        }

        private void OnDisable()
        {
            if (_director != null)
            {
                _director.OnBossPhaseStarted -= HandleBossPhaseStarted;
            }

            if (_activeBoss != null)
            {
                _activeBoss.OnDeath -= HandleBossDeath;
            }
        }

        // 보스 등장은 런(run)당 1회. WaveCombatDirector가 재시작되어도 본 디렉터는 단순 1회 가드를 사용한다.
        private void HandleBossPhaseStarted()
        {
            if (_encounterStarted)
            {
                return;
            }

            if (_bossPrefab == null)
            {
                Debug.LogError("[BossEncounterDirector] 보스 프리팹이 비어 있어 보스를 소환할 수 없습니다.", this);
                return;
            }

            _encounterStarted = true;

            // 1) 남은 일반 적 즉시 제거. Kill()이 아닌 풀 반환 경로라 OnDeath가 발행되지 않아 영혼 등 보상이 드랍되지 않는다.
            if (_director != null)
            {
                _director.DespawnTrackedEnemies();
            }

            // 2) 보스 스폰.
            if (!SpawnBoss())
            {
                return;
            }

            // 3) 등장 연출 → 무적 해제 → 전투 시작.
            StartCoroutine(IntroRoutine());
        }

        private bool SpawnBoss()
        {
            Vector3 spawnPosition = ResolveSpawnPosition();
            GameObject prefabGO = _bossPrefab.gameObject;

            bool pooled = PoolManager.Instance != null;
            GameObject spawned = pooled
                ? PoolManager.Instance.GetInactive(prefabGO, spawnPosition, Quaternion.identity)
                : Instantiate(prefabGO, spawnPosition, Quaternion.identity);

            if (spawned == null)
            {
                Debug.LogError("[BossEncounterDirector] 보스 스폰에 실패했습니다.", this);
                return false;
            }

            CacheBossComponents(spawned);

            // null 체크는 _activeBoss를 사용(PrepareForReuse 등)하기 전에 수행해 NRE를 방지한다.
            if (_activeBoss == null)
            {
                Debug.LogError("[BossEncounterDirector] 스폰된 보스에 EnemyHealth 컴포넌트가 없습니다.", this);
                if (pooled)
                {
                    PoolManager.Instance.Release(spawned);
                }
                else
                {
                    Destroy(spawned);
                }

                return false;
            }

            InitializeBoss();

            if (pooled)
            {
                // 풀 재사용 시에만 상태 복원이 필요하다. Instantiate 신규 인스턴스는 Awake에서 초기화되므로 생략한다.
                _activeBoss.PrepareForReuse();
                spawned.SetActive(true);
            }

            // PrepareForReuse가 IsTargetable을 true로 되돌리므로, 무적 처리는 그 이후에 적용한다.
            if (_invincibleDuringIntro && _activeBossHealth != null)
            {
                _activeBossHealth.SetInvincible(true);
            }

            // 풀 재사용 시 이전 구독이 남아 있을 수 있으므로 중복 구독을 방지한다.
            _activeBoss.OnDeath -= HandleBossDeath;
            _activeBoss.OnDeath += HandleBossDeath;
            OnBossSpawned?.Invoke(_activeBoss);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[BossEncounterDirector] 보스 등장: {_activeBoss.DisplayName}");
            }
#endif
            return true;
        }

        private void CacheBossComponents(GameObject spawned)
        {
            _activeBoss = spawned.GetComponent<EnemyHealth>();
            _activeBossHealth = spawned.GetComponent<BossHealthComponent>();
        }

        private void InitializeBoss()
        {
            if (_activeBossHealth != null)
            {
                _activeBossHealth.Initialize();
            }
            else
            {
                Debug.LogWarning("[BossEncounterDirector] 보스 프리팹에 BossHealthComponent가 없어 페이즈/무적 처리가 비활성화됩니다.", this);
            }
        }

        private IEnumerator IntroRoutine()
        {
            if (_introDurationSeconds > 0f)
            {
                yield return new WaitForSeconds(_introDurationSeconds);
            }

            if (_activeBossHealth != null)
            {
                _activeBossHealth.SetInvincible(false);
            }

            OnBossEncounterStarted?.Invoke(_activeBoss);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log("[BossEncounterDirector] 등장 연출 종료 — 보스 전투 시작.");
            }
#endif
        }

        private void HandleBossDeath(EnemyHealth boss)
        {
            if (boss != null)
            {
                boss.OnDeath -= HandleBossDeath;
            }

            StartCoroutine(DeathRoutine(boss));
        }

        private IEnumerator DeathRoutine(EnemyHealth boss)
        {
            if (_deathDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(_deathDelaySeconds);
            }

            OnChapterCleared?.Invoke();

            // 사망 연출 후 보스 오브젝트를 풀에 반환(없으면 파괴)해 씬에 남지 않도록 정리한다.
            if (boss != null)
            {
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Release(boss.gameObject);
                }
                else
                {
                    Destroy(boss.gameObject);
                }
            }

            if (_activeBoss == boss)
            {
                _activeBoss = null;
                _activeBossHealth = null;
            }

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log("[BossEncounterDirector] 보스 처치 — 챕터 클리어.");
            }
#endif
        }

        private Vector3 ResolveSpawnPosition()
        {
            if (_bossSpawnPoint != null)
            {
                return _bossSpawnPoint.position;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 center = camera.transform.position;
                return new Vector3(center.x, center.y, 0f);
            }

            return transform.position;
        }
    }
}
