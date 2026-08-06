using System;
using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Pool;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// WaveCombatDirector.OnMiniBossMarkReached를 구독하여 미니 보스를 소환한다.
    /// 챕터 적 풀에서 1종을 랜덤 선택하고 차수별 체력·크기·속도 배율을 적용한다.
    /// 처치 시 OnMiniBossDefeated 이벤트를 발행하여 #63 보장 스킬 선택 시스템과 연동한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniBossSpawner : MonoBehaviour
    {
        [Header("Chapter")]
        [Tooltip("이 챕터의 적 풀과 미니 보스 차수를 사용한다(#64). 비우면 아래 값들을 그대로 쓴다. " +
                 "런타임에는 스테이지 선택 결과(RunContext.SelectedChapter)가 이 값보다 우선한다.")]
        [SerializeField]
        private ChapterData _chapterData;

        [Header("References")]
        [SerializeField]
        private WaveCombatDirector _director;

        [SerializeField, Tooltip("넉백 방향 계산에 사용할 플레이어 Transform.")]
        private Transform _playerTransform;

        [SerializeField, Tooltip("영혼 오브 프리팹 (보상 드랍용). 비워두면 오브를 드랍하지 않는다.")]
        private SoulOrb _soulOrbPrefab;

        [Header("Chapter Enemy Pool")]
        [Tooltip("챕터 적 풀. 미니 보스 소환 시 이 중 1종이 랜덤으로 선택되어 강화된다.")]
        [SerializeField]
        private List<WaveEnemySpawnEntry> _enemyPool = new List<WaveEnemySpawnEntry>();

        [Header("Mini Boss Stages")]
        [Tooltip("차수별 데이터. SpawnMinuteMark는 WaveCombatDirector._miniBossMinuteMarks 값과 일치해야 한다.")]
        [SerializeField]
        private List<MiniBossStageData> _stages = new List<MiniBossStageData>();

        [Header("Knockback Settings")]
        [SerializeField, Min(0f)]
        private float _knockbackDistance = 3f;

        [SerializeField, Min(0.05f)]
        private float _knockbackDuration = 0.25f;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private readonly Dictionary<EnemyHealth, int> _activeMiniBosses = new Dictionary<EnemyHealth, int>();
        private readonly Dictionary<float, int> _markToStageIndex = new Dictionary<float, int>();
        private readonly List<WaveEnemySpawnEntry> _validPoolCache = new List<WaveEnemySpawnEntry>();

        /// <summary>미니 보스 처치 시 발행. 인자: (차수 인덱스, 차수 데이터). #63 보장 스킬 선택 시스템이 구독.</summary>
        public event Action<int, MiniBossStageData> OnMiniBossDefeated;

        /// <summary>골드 보상 발행. 인자: (차수 인덱스, 골드량). 골드 시스템(#61)이 구독.</summary>
        public event Action<int, int> OnMiniBossGoldDropped;

        /// <summary>영혼 재화 보상 발행. 인자: (차수 인덱스, 영혼량). 영혼 재화 시스템(#61)이 구독.</summary>
        public event Action<int, int> OnMiniBossSoulCurrencyDropped;

        /// <summary>
        /// 챕터 해석은 <c>Awake</c>에서 끝낸다 — <c>OnEnable</c>의 <see cref="BuildMarkIndex"/>가 차수 목록을 읽으므로
        /// 그 전에 이 챕터의 차수가 자리잡아 있어야 한다. <see cref="WaveCombatDirector"/>에서 건네받지 않고
        /// 각자 <see cref="RunContext"/>를 읽는 이유는 컴포넌트 간 <c>Awake</c> 순서에 기대지 않기 위해서다.
        /// </summary>
        private void Awake()
        {
            ApplyChapterData();
        }

        private void ApplyChapterData()
        {
            ChapterData chapter = RunContext.SelectedChapter != null ? RunContext.SelectedChapter : _chapterData;
            if (chapter == null)
            {
                return;
            }

            _chapterData = chapter;

            // 로스터 전체가 아니라 '미니 보스 후보'를 쓴다 — 챕터가 후보를 따로 지정하지 않았으면
            // ChapterData가 알아서 로스터 전체를 돌려준다. 비어 있으면 미니 보스가 아예 소환되지 않으므로
            // 챕터 쪽이 비어 있는 경우엔 씬 값을 남긴다.
            IReadOnlyList<WaveEnemySpawnEntry> candidates = chapter.MiniBossCandidates;
            if (candidates != null && candidates.Count > 0)
            {
                _enemyPool = new List<WaveEnemySpawnEntry>(candidates);
            }

            // 차수는 WaveCombatDirector가 발행하는 마크와 짝을 이뤄야 한다. 디렉터도 같은 챕터에서
            // 마크를 파생시키므로(ChapterData.MiniBossMinuteMarks) 여기서는 그대로 반영하면 항상 일치한다.
            _stages = chapter.MiniBossStages != null
                ? new List<MiniBossStageData>(chapter.MiniBossStages)
                : new List<MiniBossStageData>();
        }

        private void OnEnable()
        {
            BuildMarkIndex();

            if (_director != null)
            {
                _director.OnMiniBossMarkReached += HandleMarkReached;
            }
        }

        private void OnDisable()
        {
            if (_director != null)
            {
                _director.OnMiniBossMarkReached -= HandleMarkReached;
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<EnemyHealth, int> kvp in _activeMiniBosses)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.OnDeath -= HandleMiniBossDeath;
                }
            }

            _activeMiniBosses.Clear();
        }

        private void BuildMarkIndex()
        {
            _markToStageIndex.Clear();
            for (int i = 0; i < _stages.Count; i++)
            {
                if (_stages[i] == null)
                {
                    continue;
                }

                float mark = _stages[i].SpawnMinuteMark;
                if (!_markToStageIndex.ContainsKey(mark))
                {
                    _markToStageIndex[mark] = i;
                }
            }
        }

        private void HandleMarkReached(float mark)
        {
            if (!_markToStageIndex.TryGetValue(mark, out int stageIndex))
            {
#if UNITY_EDITOR
                if (_showDebugLogs)
                {
                    Debug.LogWarning($"[MiniBossSpawner] 분 마크 {mark}에 해당하는 차수 데이터가 없습니다.");
                }
#endif
                return;
            }

            SpawnMiniBoss(stageIndex);
        }

        private void SpawnMiniBoss(int stageIndex)
        {
            MiniBossStageData stage = _stages[stageIndex];
            WaveEnemySpawnEntry selected = PickRandomEntry();

            if (selected == null)
            {
                Debug.LogWarning("[MiniBossSpawner] 적 풀에 유효한 엔트리가 없어 미니 보스를 소환할 수 없습니다.");
                return;
            }

            if (selected.EnemyPrefab == null)
            {
                Debug.LogWarning("[MiniBossSpawner] 선택된 엔트리의 EnemyPrefab이 null입니다.");
                return;
            }

            Vector3 spawnPos = ResolveOffscreenPosition();
            GameObject prefabGO = selected.EnemyPrefab.gameObject;
            GameObject spawnedObj;
            EnemyHealth enemy;

            if (PoolManager.Instance != null)
            {
                spawnedObj = PoolManager.Instance.GetInactive(prefabGO, spawnPos, Quaternion.identity);
                if (spawnedObj == null) return;

                enemy = spawnedObj.GetComponent<EnemyHealth>();
                if (enemy == null)
                {
                    Debug.LogError($"[MiniBossSpawner] {spawnedObj.name}에 EnemyHealth 컴포넌트가 없습니다.");
                    return;
                }

                SetupMiniBoss(enemy, spawnedObj, stage, selected);
                enemy.PrepareForReuse();
                spawnedObj.SetActive(true);
            }
            else
            {
                spawnedObj = Instantiate(prefabGO, spawnPos, Quaternion.identity);
                enemy = spawnedObj.GetComponent<EnemyHealth>();
                if (enemy == null)
                {
                    Debug.LogError($"[MiniBossSpawner] {spawnedObj.name}에 EnemyHealth 컴포넌트가 없습니다.");
                    Destroy(spawnedObj);
                    return;
                }

                SetupMiniBoss(enemy, spawnedObj, stage, selected);
            }

            AttachKnockback(enemy, stage);

            enemy.OnDeath += HandleMiniBossDeath;
            _activeMiniBosses[enemy] = stageIndex;

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[MiniBossSpawner] {stageIndex + 1}차 미니 보스 소환: {enemy.DisplayName} " +
                          $"(HP x{stage.HealthMultiplier}, 크기 x{stage.SizeMultiplier})");
            }
#endif
        }

        private void SetupMiniBoss(EnemyHealth enemy, GameObject go, MiniBossStageData stage, WaveEnemySpawnEntry entry)
        {
            enemy.ApplyMonsterData(entry.MonsterData);

            // 체력 배율 적용 — ApplyMonsterData 이후에 Override해야 PrepareForReuse에서 배율이 반영된다.
            float baseHp = entry.MonsterData != null ? entry.MonsterData.MaxHealth : enemy.MaxHealth;
            enemy.SetMaxHealth(baseHp * stage.HealthMultiplier);

            enemy.SetMoveSpeed(stage.MoveSpeed);

            // 크기 배율 적용
            go.transform.localScale = Vector3.one * stage.SizeMultiplier;

            // EnemySoulDropper가 있으면 MonsterData 기반 드롭을 그대로 사용
            EnemySoulDropper soulDropper = go.GetComponent<EnemySoulDropper>();
            if (soulDropper != null && entry.MonsterData != null)
            {
                soulDropper.ApplyMonsterData(entry.MonsterData);
            }
        }

        private void AttachKnockback(EnemyHealth enemy, MiniBossStageData stage)
        {
            MiniBossKnockbackBehaviour kb = enemy.GetComponent<MiniBossKnockbackBehaviour>();
            if (kb == null)
            {
                kb = enemy.gameObject.AddComponent<MiniBossKnockbackBehaviour>();
            }

            float threshold = enemy.MaxHealth * stage.KnockbackThresholdRatio;
            kb.Initialise(enemy, threshold, _knockbackDistance, _knockbackDuration, _playerTransform);
        }

        private void HandleMiniBossDeath(EnemyHealth enemy)
        {
            if (!_activeMiniBosses.TryGetValue(enemy, out int stageIndex))
            {
                return;
            }

            enemy.OnDeath -= HandleMiniBossDeath;
            _activeMiniBosses.Remove(enemy);

            // 풀 반환 전 크기를 원래대로 복원한다.
            enemy.transform.localScale = Vector3.one;

            MiniBossStageData stage = _stages[stageIndex];

            DropSoulOrbs(enemy.transform.position, stage);

            OnMiniBossGoldDropped?.Invoke(stageIndex, stage.GoldReward);
            OnMiniBossSoulCurrencyDropped?.Invoke(stageIndex, stage.SoulCurrencyReward);
            OnMiniBossDefeated?.Invoke(stageIndex, stage);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[MiniBossSpawner] {stageIndex + 1}차 미니 보스 처치. " +
                          $"골드: {stage.GoldReward}, 영혼: {stage.SoulCurrencyReward}");
            }
#endif
        }

        private void DropSoulOrbs(Vector3 position, MiniBossStageData stage)
        {
            if (_soulOrbPrefab == null || stage.SoulCurrencyReward <= 0)
            {
                return;
            }

            for (int i = 0; i < stage.SoulCurrencyReward; i++)
            {
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Get(_soulOrbPrefab.gameObject, position, Quaternion.identity);
                }
                else
                {
                    Instantiate(_soulOrbPrefab, position, Quaternion.identity);
                }
            }
        }

        private WaveEnemySpawnEntry PickRandomEntry()
        {
            _validPoolCache.Clear();
            for (int i = 0; i < _enemyPool.Count; i++)
            {
                WaveEnemySpawnEntry entry = _enemyPool[i];
                if (entry != null && entry.IsValid(out _))
                {
                    _validPoolCache.Add(entry);
                }
            }

            if (_validPoolCache.Count == 0)
            {
                return null;
            }

            return _validPoolCache[UnityEngine.Random.Range(0, _validPoolCache.Count)];
        }

        private Vector3 ResolveOffscreenPosition()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return transform.position;
            }

            float vExt = cam.orthographicSize;
            float hExt = vExt * cam.aspect;
            const float Margin = 1f;
            Vector3 center = cam.transform.position;

            float ox, oy;
            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0:
                    ox = UnityEngine.Random.Range(-hExt, hExt);
                    oy = vExt + Margin;
                    break;
                case 1:
                    ox = UnityEngine.Random.Range(-hExt, hExt);
                    oy = -vExt - Margin;
                    break;
                case 2:
                    ox = -hExt - Margin;
                    oy = UnityEngine.Random.Range(-vExt, vExt);
                    break;
                default:
                    ox = hExt + Margin;
                    oy = UnityEngine.Random.Range(-vExt, vExt);
                    break;
            }

            return new Vector3(center.x + ox, center.y + oy, 0f);
        }
    }
}
