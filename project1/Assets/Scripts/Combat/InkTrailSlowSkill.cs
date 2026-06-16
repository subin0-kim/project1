using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Core.Pool;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 끈적한 묵액(#75) — 발동 방식 ① 스와이프 시 확률 발동(공용 스킬).
    /// 스와이프 공격마다 레벨별 확률로 스와이프 끝점에 먹물 자국(<see cref="InkTrailMark"/>)을 남긴다.
    /// 같은 자리에 다시 생성되면 새 자국 대신 지속 시간을 갱신하고, 동시 존재 자국 수에 상한을 둔다.
    ///
    /// 레벨 추적은 <see cref="PlayerLevelSystem.OnSkillEffectPending"/> 구독 + OnEnable 동기화로 처리한다.
    /// 수치(확률/감속률/지속)는 인스펙터에서 관리한다(skill_balance_mvp.md §4).
    /// </summary>
    [DisallowMultipleComponent]
    public class InkTrailSlowSkill : MonoBehaviour
    {
        public readonly struct SlowSpec
        {
            /// <summary>이동 속도 배수(0~1). 예: 감속률 30% → 0.7.</summary>
            public readonly float SlowMultiplier;
            public readonly float Duration;

            public SlowSpec(float slowMultiplier, float duration)
            {
                SlowMultiplier = slowMultiplier;
                Duration = duration;
            }
        }

        [Header("References")]
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField]
        private PlayerSwipeAttackController _swipeAttackController;

        [SerializeField]
        private Camera _camera;

        [SerializeField, Tooltip("연동 SkillData의 SkillId. OnEnable 레벨 동기화에 사용.")]
        private string _skillId = "ink_trail_slow";

        [Header("Mark")]
        [SerializeField, Tooltip("먹물 자국 프리팹(InkTrailMark 보유)")]
        private GameObject _markPrefab;

        [SerializeField, Min(0.1f), Tooltip("자국 슬로우 반경(월드 유닛)")]
        private float _markRadius = 1.1f;

        [SerializeField, Min(1), Tooltip("동시 존재 가능한 자국 최대 개수")]
        private int _maxConcurrent = 8;

        [SerializeField, Min(0f), Tooltip("이 거리 이내에 기존 자국이 있으면 새로 만들지 않고 지속 시간만 갱신")]
        private float _mergeDistance = 0.8f;

        [Header("Per-Level (index 0 = Lv1) — skill_balance_mvp.md §4")]
        [SerializeField, Tooltip("레벨별 발동 확률(0~1)")]
        private float[] _triggerChancePerLevel = { 0.3f, 0.45f, 0.6f };

        [SerializeField, Tooltip("레벨별 이동속도 감소율(0~1). 0.3 = 30% 감속")]
        private float[] _slowPercentPerLevel = { 0.3f, 0.4f, 0.5f };

        [SerializeField, Tooltip("레벨별 자국 지속 시간(초)")]
        private float[] _durationPerLevel = { 2.0f, 2.5f, 3.0f };

        public const int MaxLevel = 3;

        private int _level;
        private readonly List<InkTrailMark> _activeMarks = new List<InkTrailMark>(16);

        public int Level => _level;

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }

            if (_swipeAttackController == null)
            {
                _swipeAttackController = GetComponent<PlayerSwipeAttackController>();
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

            if (_swipeAttackController != null)
            {
                _swipeAttackController.OnAttackExecuted += HandleAttackExecuted;
            }
        }

        private void OnDisable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
            }

            if (_swipeAttackController != null)
            {
                _swipeAttackController.OnAttackExecuted -= HandleAttackExecuted;
            }
        }

        private void HandleSkillEffectPending(SkillData skill, int nextLevel)
        {
            if (skill == null || skill.EffectType != LevelUpSkillEffectType.InkTrailSlow)
            {
                return;
            }

            ApplyLevel(nextLevel);
        }

        /// <summary>레벨을 직접 설정한다(이벤트 핸들러 및 테스트에서 사용). [0, MaxLevel]로 클램프.</summary>
        public void ApplyLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, MaxLevel);
        }

        private void HandleAttackExecuted(SwipeDirection direction, Vector2 endScreenPosition)
        {
            if (_markPrefab == null)
            {
                return;
            }

            if (!TryRollSlow(out SlowSpec spec))
            {
                return;
            }

            Vector2 world = ScreenToWorld(endScreenPosition);
            SpawnOrRefreshMark(world, spec);
        }

        /// <summary>현재 레벨 확률로 발동 판정. 발동 시 <paramref name="spec"/>에 감속 배수/지속 시간을 채운다.</summary>
        public bool TryRollSlow(out SlowSpec spec)
        {
            float chance = GetChance(_level);
            float roll = chance >= 1f ? 0f : Random.value;
            return TryRollSlow(roll, out spec);
        }

        /// <summary>난수(<paramref name="roll"/>, [0,1))를 주입하는 결정론적 오버로드(테스트용).</summary>
        public bool TryRollSlow(float roll, out SlowSpec spec)
        {
            spec = default;
            float chance = GetChance(_level);
            if (chance <= 0f || roll >= chance)
            {
                return false;
            }

            float slowPercent = GetPerLevel(_slowPercentPerLevel, _level, 0.3f);
            float duration = GetPerLevel(_durationPerLevel, _level, 2f);
            spec = new SlowSpec(Mathf.Clamp01(1f - slowPercent), Mathf.Max(0.1f, duration));
            return true;
        }

        private void SpawnOrRefreshMark(Vector2 position, SlowSpec spec)
        {
            PruneInactiveMarks();

            // 중첩: 기존 자국이 mergeDistance 이내면 새로 만들지 않고 지속 시간만 갱신.
            float mergeSqr = _mergeDistance * _mergeDistance;
            for (int i = 0; i < _activeMarks.Count; i++)
            {
                if ((_activeMarks[i].WorldPosition - position).sqrMagnitude <= mergeSqr)
                {
                    _activeMarks[i].Refresh(spec.Duration);
                    return;
                }
            }

            // 동시 존재 상한 초과 시 이번 발동은 스킵(가장 오래된 것을 유지).
            if (_activeMarks.Count >= _maxConcurrent)
            {
                return;
            }

            GameObject go = AcquireMark(position);
            if (go == null)
            {
                return;
            }

            var mark = go.GetComponent<InkTrailMark>();
            if (mark == null)
            {
                return;
            }

            mark.Initialize(spec.Duration, spec.SlowMultiplier, _markRadius);
            go.SetActive(true);
            _activeMarks.Add(mark);
        }

        private GameObject AcquireMark(Vector2 position)
        {
            var pos = new Vector3(position.x, position.y, 0f);

            // 비활성으로 꺼내 Initialize 후 활성화해야 OnEnable이 올바른 값으로 시작한다.
            if (PoolManager.Instance != null)
            {
                return PoolManager.Instance.GetInactive(_markPrefab, pos, Quaternion.identity);
            }

            GameObject obj = Instantiate(_markPrefab, pos, Quaternion.identity);
            obj.SetActive(false);
            return obj;
        }

        private void PruneInactiveMarks()
        {
            for (int i = _activeMarks.Count - 1; i >= 0; i--)
            {
                if (_activeMarks[i] == null || !_activeMarks[i].IsActiveMark)
                {
                    _activeMarks.RemoveAt(i);
                }
            }
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                return screenPosition;
            }

            float depth = -_camera.transform.position.z; // 월드 z=0 평면
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            return new Vector2(world.x, world.y);
        }

        private float GetChance(int level)
        {
            return GetPerLevel(_triggerChancePerLevel, level, 0f, requireOwned: true);
        }

        private static float GetPerLevel(float[] perLevel, int level, float fallback, bool requireOwned = false)
        {
            if (level < 1 || perLevel == null || perLevel.Length == 0)
            {
                return requireOwned ? 0f : fallback;
            }

            int index = Mathf.Clamp(level - 1, 0, perLevel.Length - 1);
            return perLevel[index];
        }
    }
}
