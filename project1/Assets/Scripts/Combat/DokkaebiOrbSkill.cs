using System.Collections.Generic;
using Mukseon.Core.Pool;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 도깨비불 소환(#72) — 발동 방식 ② 쿨타임 자동 발동(공용 스킬).
    /// 플레이어 주변을 궤도 비행하는 도깨비불 드론(<see cref="DokkaebiOrbDrone"/>)을 레벨별 개수만큼 소환한다.
    /// 각 드론은 독립적으로 탐지·돌진·자폭하며, 자폭 후 해당 드론만 재소환 쿨타임을 거쳐 재소환된다.
    ///
    /// 이 컴포넌트는 레벨 추적과 드론 풀 관리를 담당하고, 개별 드론의 이동/탐지/폭발은
    /// <see cref="DokkaebiOrbDrone"/>이 이 컴포넌트의 현재 수치(반경·데미지·쿨타임 등)를 실시간 조회해 처리한다.
    /// 레벨 추적은 <see cref="PlayerLevelSystem.OnSkillEffectPending"/> 구독 + OnEnable 동기화로 처리한다.
    /// 수치는 인스펙터에서 관리한다(skill_balance_mvp.md §1).
    /// </summary>
    [DisallowMultipleComponent]
    public class DokkaebiOrbSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField, Tooltip("궤도 중심(미지정 시 이 컴포넌트의 Transform). 보통 플레이어.")]
        private Transform _orbitCenter;

        [SerializeField, Tooltip("연동 SkillData의 SkillId. OnEnable 레벨 동기화에 사용.")]
        private string _skillId = "dokkaebi_orb";

        [SerializeField, Tooltip("도깨비불 드론 프리팹(DokkaebiOrbDrone 보유). 오브젝트 풀링 대상.")]
        private GameObject _dronePrefab;

        [Header("Orbit")]
        [SerializeField, Min(0.1f), Tooltip("플레이어 중심 궤도 반경(월드 유닛)")]
        private float _orbitRadius = 1.8f;

        [SerializeField, Tooltip("궤도 회전 각속도(도/초)")]
        private float _orbitAngularSpeedDeg = 120f;

        [Header("Drone Behavior")]
        [SerializeField, Min(0.1f), Tooltip("탐지 기본 반경(월드 유닛). 레벨별 배수가 곱해진다.")]
        private float _baseDetectRange = 4f;

        [SerializeField, Min(0.1f), Tooltip("자폭 폭발 반경(월드 유닛)")]
        private float _explosionRadius = 1.5f;

        [SerializeField, Min(0.1f), Tooltip("적을 향해 돌진하는 속도(월드 유닛/초)")]
        private float _chargeSpeed = 9f;

        [SerializeField, Min(0.01f), Tooltip("타깃에 이 거리 이내로 도달하면 자폭한다.")]
        private float _detonateDistance = 0.35f;

        [Header("Per-Level (index 0 = Lv1) — skill_balance_mvp.md §1")]
        [SerializeField, Tooltip("레벨별 도깨비불 수")]
        private int[] _droneCountPerLevel = { 1, 1, 2, 2, 3 };

        [SerializeField, Tooltip("레벨별 탐지 범위 배수(기본=1.0, +20%=1.2, +30%=1.3)")]
        private float[] _detectRangeMultiplierPerLevel = { 1.0f, 1.2f, 1.2f, 1.3f, 1.3f };

        [SerializeField, Tooltip("레벨별 폭발 데미지")]
        private float[] _explosionDamagePerLevel = { 50f, 65f, 65f, 80f, 100f };

        [SerializeField, Tooltip("레벨별 재소환 쿨타임(초)")]
        private float[] _resummonCooldownPerLevel = { 5.0f, 4.5f, 4.5f, 4.0f, 3.5f };

        public const int MaxLevel = 5;

        private int _level;
        private readonly List<DokkaebiOrbDrone> _drones = new List<DokkaebiOrbDrone>(MaxLevel);

        // 공유 재소환 쿨타임 — 드론별 개별 쿨타임 대신, 돌진 시작 시 시작해 경과하면 소비된 드론을 일괄 재소환한다.
        private readonly DokkaebiOrbResummonClock _resummonClock = new DokkaebiOrbResummonClock();

        public int Level => _level;

        /// <summary>현재 레벨의 도깨비불 수(미보유=0).</summary>
        public int CurrentDroneCount => _level < 1 ? 0 : Mathf.Max(0, GetPerLevel(_droneCountPerLevel, _level, 0));

        /// <summary>현재 탐지 반경(미보유=0).</summary>
        public float CurrentDetectRange => _level < 1 ? 0f : _baseDetectRange * GetPerLevel(_detectRangeMultiplierPerLevel, _level, 1f);

        /// <summary>현재 폭발 데미지(미보유=0).</summary>
        public float CurrentExplosionDamage => _level < 1 ? 0f : GetPerLevel(_explosionDamagePerLevel, _level, 0f);

        /// <summary>현재 재소환 쿨타임(초).</summary>
        public float CurrentResummonCooldown => Mathf.Max(0f, GetPerLevel(_resummonCooldownPerLevel, _level, 5f));

        // 드론이 실시간 조회하는 공유 수치.
        public Transform OrbitCenter => _orbitCenter != null ? _orbitCenter : transform;
        public float OrbitRadius => _orbitRadius;
        public float OrbitAngularSpeedDeg => _orbitAngularSpeedDeg;
        public float ExplosionRadius => _explosionRadius;
        public float ChargeSpeed => _chargeSpeed;
        public float DetonateDistance => _detonateDistance;

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }

            if (_orbitCenter == null)
            {
                _orbitCenter = transform;
            }
        }

        private void OnEnable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
                // 비활성 중 부여/레벨업 이벤트를 놓쳤을 수 있어 현재 레벨을 직접 동기화한다.
                ApplyLevel(_playerLevelSystem.GetSkillLevel(_skillId));
            }
        }

        private void OnDisable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
            }

            // 스킬이 해제되면(게임오버·씬 언로드 등) 모든 드론을 회수해 잔존하지 않도록 한다.
            ReleaseAllDrones();
            _resummonClock.Reset();
        }

        private void Update()
        {
            if (_level < 1)
            {
                return;
            }

            // 드론이 처음 돌진을 시작하면 공유 쿨타임이 시작되고, 경과 시 소비된 드론만 일괄 재소환한다.
            // 아직 날아가는 중인 드론은 건드리지 않는다(그대로 날아가 자폭).
            if (_resummonClock.Tick(Time.deltaTime, AnyDroneCharging(), CurrentResummonCooldown))
            {
                ResummonConsumedDrones();
            }
        }

        private bool AnyDroneCharging()
        {
            for (int i = 0; i < _drones.Count; i++)
            {
                DokkaebiOrbDrone drone = _drones[i];
                if (drone != null && drone.State == DokkaebiOrbDrone.DroneState.Charging)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResummonConsumedDrones()
        {
            for (int i = 0; i < _drones.Count; i++)
            {
                DokkaebiOrbDrone drone = _drones[i];
                if (drone != null && drone.State == DokkaebiOrbDrone.DroneState.Consumed)
                {
                    drone.Resummon();
                }
            }
        }

        private void HandleSkillEffectPending(SkillData skill, int nextLevel)
        {
            if (skill == null || skill.EffectType != LevelUpSkillEffectType.SummonDokkaebiOrb)
            {
                return;
            }

            ApplyLevel(nextLevel);
        }

        /// <summary>레벨을 직접 설정한다(이벤트 핸들러 및 테스트에서 사용). [0, MaxLevel]로 클램프 후 드론 수를 동기화.</summary>
        public void ApplyLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, MaxLevel);

            // 활성 상태가 아니면(OnEnable 이전) 드론 스폰을 보류한다. OnEnable에서 다시 동기화된다.
            if (isActiveAndEnabled)
            {
                SyncDrones();
            }
        }

        /// <summary>현재 레벨의 도깨비불 수에 맞춰 드론을 스폰/회수하고, 궤도 위상을 균등 분배한다.</summary>
        private void SyncDrones()
        {
            PruneDestroyedDrones();

            int desired = CurrentDroneCount;

            // 부족하면 스폰, 초과하면(레벨 하향 등) 회수.
            while (_drones.Count < desired)
            {
                if (!TrySpawnDrone())
                {
                    break;
                }
            }

            while (_drones.Count > desired)
            {
                ReleaseDrone(_drones.Count - 1);
            }

            // 드론들을 궤도에 균등 분배한다(개수 변동 시 재배치).
            int count = _drones.Count;
            for (int i = 0; i < count; i++)
            {
                float phaseDeg = count > 0 ? (360f / count) * i : 0f;
                _drones[i].SetOrbitPhase(phaseDeg);
            }
        }

        private bool TrySpawnDrone()
        {
            // 프리팹 미지정 시 조용히 보류한다(InkTrailSlowSkill의 마크 프리팹 처리와 동일).
            // 드론이 나타나지 않는 것으로 설정 누락이 즉시 드러난다.
            if (_dronePrefab == null)
            {
                return false;
            }

            Vector3 spawnPos = OrbitCenter.position;

            // 비활성으로 꺼내 Initialize 후 활성화해야 OnEnable이 올바른 값으로 시작한다.
            GameObject go = PoolManager.Instance != null
                ? PoolManager.Instance.GetInactive(_dronePrefab, spawnPos, Quaternion.identity)
                : InstantiateInactive(spawnPos);

            if (go == null)
            {
                return false;
            }

            var drone = go.GetComponent<DokkaebiOrbDrone>();
            if (drone == null)
            {
                Debug.LogError("[DokkaebiOrbSkill] 드론 프리팹에 DokkaebiOrbDrone 컴포넌트가 없습니다.");
                ReleaseGameObject(go);
                return false;
            }

            drone.Initialize(this, 0f);
            go.SetActive(true);
            _drones.Add(drone);
            return true;
        }

        private GameObject InstantiateInactive(Vector3 position)
        {
            GameObject obj = Instantiate(_dronePrefab, position, Quaternion.identity);
            obj.SetActive(false);
            return obj;
        }

        private void ReleaseAllDrones()
        {
            for (int i = _drones.Count - 1; i >= 0; i--)
            {
                ReleaseDrone(i);
            }
        }

        private void ReleaseDrone(int index)
        {
            if (index < 0 || index >= _drones.Count)
            {
                return;
            }

            DokkaebiOrbDrone drone = _drones[index];
            _drones.RemoveAt(index);

            if (drone != null)
            {
                ReleaseGameObject(drone.gameObject);
            }
        }

        private void ReleaseGameObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(go);
            }
            else
            {
                Destroy(go);
            }
        }

        private void PruneDestroyedDrones()
        {
            for (int i = _drones.Count - 1; i >= 0; i--)
            {
                if (_drones[i] == null)
                {
                    _drones.RemoveAt(i);
                }
            }
        }

        // 레벨별 배열에서 현재 레벨 값을 읽는다(미보유=fallback, 범위 초과 시 마지막 값으로 클램프).
        // float/int 등 타입에 무관하게 동작하도록 제네릭으로 통합한다.
        private static T GetPerLevel<T>(T[] perLevel, int level, T fallback)
        {
            if (level < 1 || perLevel == null || perLevel.Length == 0)
            {
                return fallback;
            }

            int index = Mathf.Clamp(level - 1, 0, perLevel.Length - 1);
            return perLevel[index];
        }
    }
}
