using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 보스 전투의 공통 데이터(#37). 총 체력 · 페이즈 전환 임계값 · 페이즈별 이동 속도 · 접촉 데미지 · 처치 보상을 담는다.
    /// 타락한 산군 등 개별 보스의 패턴 데이터는 별도 SO(#69)에서 이 데이터를 참조/확장한다.
    /// 보스 프리팹의 <see cref="MonsterData"/>(IsBoss=true)와 함께 사용하며, 총 체력은 이 SO가 권위를 갖는다
    /// (<see cref="BossHealthComponent"/>가 EnemyHealth 최대 체력을 <see cref="TotalHealth"/>로 덮어쓴다).
    /// </summary>
    [CreateAssetMenu(fileName = "BossData", menuName = "Mukseon/Data/Boss Data")]
    public class BossData : ScriptableObject
    {
        [SerializeField]
        private string _bossId = "boss.default";

        [SerializeField]
        private string _displayName = "Boss";

        [Tooltip("보스 총 체력. 보스 HP의 권위 있는 소스 — 프리팹 MonsterData.MaxHealth보다 우선한다.")]
        [SerializeField, Min(1f)]
        private float _totalHealth = 1000f;

        [Tooltip("페이즈 전환 체력 비율(0~1). 내림차순으로 입력. 예: [0.5] → HP 50% 도달 시 2페이즈. 항목 N개 → 페이즈 N+1개.")]
        [SerializeField]
        private List<float> _phaseHealthThresholds = new List<float> { 0.5f };

        [Tooltip("페이즈별 이동 속도. 인덱스 = 페이즈(0부터). 패턴 시스템(#69)에서 사용. 부족하면 마지막 값으로 폴백.")]
        [SerializeField]
        private List<float> _phaseMoveSpeeds = new List<float> { 1f, 1.5f };

        [Tooltip("보스 본체 접촉 시 초당 데미지.")]
        [SerializeField, Min(0f)]
        private float _contactDamagePerSecond = 10f;

        [Header("Rewards")]
        [SerializeField, Min(0)]
        private int _goldReward = 500;

        [SerializeField, Min(0)]
        private int _soulReward = 10;

        public string BossId => string.IsNullOrWhiteSpace(_bossId) ? name : _bossId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public float TotalHealth => Mathf.Max(1f, _totalHealth);
        public IReadOnlyList<float> PhaseHealthThresholds => _phaseHealthThresholds;

        /// <summary>페이즈 수. 임계값 N개 → 페이즈 N+1개.</summary>
        public int PhaseCount => (_phaseHealthThresholds?.Count ?? 0) + 1;
        public float ContactDamagePerSecond => Mathf.Max(0f, _contactDamagePerSecond);
        public int GoldReward => Mathf.Max(0, _goldReward);
        public int SoulReward => Mathf.Max(0, _soulReward);

        /// <summary>지정 페이즈의 이동 속도. 범위를 벗어나면 가까운 끝값으로 클램프한다.</summary>
        public float GetPhaseMoveSpeed(int phaseIndex)
        {
            if (_phaseMoveSpeeds == null || _phaseMoveSpeeds.Count == 0)
            {
                return 1f;
            }

            int clamped = Mathf.Clamp(phaseIndex, 0, _phaseMoveSpeeds.Count - 1);
            return Mathf.Max(0f, _phaseMoveSpeeds[clamped]);
        }

        public bool IsValid(out string reason)
        {
            if (_totalHealth < 1f)
            {
                reason = "Total health must be at least 1.";
                return false;
            }

            if (_phaseHealthThresholds != null)
            {
                float previous = 1f;
                for (int i = 0; i < _phaseHealthThresholds.Count; i++)
                {
                    float threshold = _phaseHealthThresholds[i];
                    if (threshold <= 0f || threshold >= 1f)
                    {
                        reason = $"Phase threshold[{i}]={threshold} must be within (0, 1).";
                        return false;
                    }

                    if (threshold >= previous)
                    {
                        reason = $"Phase thresholds must be strictly descending. threshold[{i}]={threshold} >= {previous}.";
                        return false;
                    }

                    previous = threshold;
                }
            }

            reason = null;
            return true;
        }

        /// <summary>테스트 전용 구성 헬퍼. 직렬화 필드를 코드로 설정한다.</summary>
        internal void ConfigureForTests(float totalHealth, params float[] thresholds)
        {
            _totalHealth = totalHealth;
            _phaseHealthThresholds = thresholds != null
                ? new List<float>(thresholds)
                : new List<float>();
        }
    }
}
