using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 한 페이즈의 패턴 목록과 발동 주기(#69). 포효의 부하 소환 수는 패턴별
    /// <see cref="BossPatternDefinition.MinionCount"/>로 지정하므로(페이즈마다 다른 포효 패턴을 둠)
    /// 별도 페이즈 단위 부하 수 필드는 두지 않는다.
    /// </summary>
    [Serializable]
    public class BossPhaseDefinition
    {
        [SerializeField]
        private string _phaseName = "Phase";

        [SerializeField]
        private List<BossPatternDefinition> _patterns = new List<BossPatternDefinition>();

        [Tooltip("패턴 발동 주기 최소 초.")]
        [SerializeField, Min(0f)]
        private float _cycleIntervalMinSeconds = 8f;

        [Tooltip("패턴 발동 주기 최대 초. 최소보다 작으면 최소값으로 클램프된다.")]
        [SerializeField, Min(0f)]
        private float _cycleIntervalMaxSeconds = 10f;

        public string PhaseName => string.IsNullOrWhiteSpace(_phaseName) ? "Phase" : _phaseName;
        public IReadOnlyList<BossPatternDefinition> Patterns => _patterns;
        public int PatternCount => _patterns != null ? _patterns.Count : 0;
        public float CycleIntervalMinSeconds => Mathf.Max(0f, _cycleIntervalMinSeconds);
        public float CycleIntervalMaxSeconds => Mathf.Max(CycleIntervalMinSeconds, _cycleIntervalMaxSeconds);

        public BossPhaseDefinition()
        {
        }

        /// <summary>테스트 전용 구성자.</summary>
        internal BossPhaseDefinition(
            List<BossPatternDefinition> patterns,
            float cycleIntervalMinSeconds = 8f,
            float cycleIntervalMaxSeconds = 10f,
            string phaseName = "Phase")
        {
            _patterns = patterns ?? new List<BossPatternDefinition>();
            _cycleIntervalMinSeconds = cycleIntervalMinSeconds;
            _cycleIntervalMaxSeconds = cycleIntervalMaxSeconds;
            _phaseName = phaseName;
        }
    }

    /// <summary>
    /// 타락한 산군(#69)의 패턴·카운터·포지셔닝 데이터. HP/페이즈 임계값/이동 속도/보상 등
    /// 공통 수치는 <see cref="BossData"/>(#37)가 권위를 가지며, 본 SO는 그 위에 패턴 메커니즘을 더한다.
    /// 페이즈 리스트는 <see cref="BossData.PhaseHealthThresholds"/>의 페이즈 수와 일치하도록 작성한다
    /// (인덱스 0 = 1페이즈). 부족하면 마지막 페이즈로 폴백한다.
    /// </summary>
    [CreateAssetMenu(fileName = "MountainKingBossPatternData", menuName = "Mukseon/Data/MountainKing Boss Pattern Data")]
    public class MountainKingBossPatternData : ScriptableObject
    {
        [Header("Phases")]
        [Tooltip("페이즈별 패턴/주기. 인덱스 0 = 1페이즈. 이번 작업은 1페이즈만 채운다(2페이즈는 후속).")]
        [SerializeField]
        private List<BossPhaseDefinition> _phases = new List<BossPhaseDefinition>();

        [Header("Counter")]
        [Tooltip("카운터 성공 시 보스에 가하는 보너스 데미지 배율. 패턴의 CounterBonusDamage에 곱해진다.")]
        [SerializeField, Min(1f)]
        private float _counterBonusMultiplier = 1.5f;

        [Header("Positioning")]
        [Tooltip("대기 위치: 화면 가장자리에서 이만큼(월드 단위) 바깥. 화면 밖이므로 타격 불가.")]
        [SerializeField, Min(0f)]
        private float _offscreenMargin = 2f;

        [Tooltip("공격 위치: 화면 가장자리에서 이만큼(월드 단위) 안쪽으로 진입.")]
        [SerializeField, Min(0f)]
        private float _onscreenMargin = 2f;

        [Tooltip("화면 안으로 진입하는 이동 속도(월드 단위/초).")]
        [SerializeField, Min(0.01f)]
        private float _approachSpeed = 12f;

        [Tooltip("화면 밖으로 복귀하는 이동 속도(월드 단위/초).")]
        [SerializeField, Min(0.01f)]
        private float _retreatSpeed = 10f;

        [Header("Phase Transition")]
        [Tooltip("2페이즈 전환 연출(포효 + 타락) 시간(초). 이 동안 무적. 사용은 2페이즈 커밋.")]
        [SerializeField, Min(0f)]
        private float _phaseTransitionCutsceneSeconds = 2f;

        public float CounterBonusMultiplier => Mathf.Max(1f, _counterBonusMultiplier);
        public float OffscreenMargin => Mathf.Max(0f, _offscreenMargin);
        public float OnscreenMargin => Mathf.Max(0f, _onscreenMargin);
        public float ApproachSpeed => Mathf.Max(0.01f, _approachSpeed);
        public float RetreatSpeed => Mathf.Max(0.01f, _retreatSpeed);
        public float PhaseTransitionCutsceneSeconds => Mathf.Max(0f, _phaseTransitionCutsceneSeconds);
        public int PhaseCount => _phases != null ? _phases.Count : 0;

        /// <summary>지정 페이즈 정의. 범위를 벗어나면 가까운 끝(있는 마지막 페이즈)으로 폴백한다.</summary>
        public BossPhaseDefinition GetPhase(int phaseIndex)
        {
            if (_phases == null || _phases.Count == 0)
            {
                return null;
            }

            int clamped = Mathf.Clamp(phaseIndex, 0, _phases.Count - 1);
            return _phases[clamped];
        }

        /// <summary>지정 페이즈의 패턴 발동 주기를 [min, max]에서 랜덤으로 뽑는다.</summary>
        public float GetRandomCycleInterval(int phaseIndex)
        {
            BossPhaseDefinition phase = GetPhase(phaseIndex);
            if (phase == null)
            {
                return 0f;
            }

            return UnityEngine.Random.Range(phase.CycleIntervalMinSeconds, phase.CycleIntervalMaxSeconds);
        }

        public bool IsValid(out string reason)
        {
            if (_phases == null || _phases.Count == 0)
            {
                reason = "At least one phase must be defined.";
                return false;
            }

            for (int i = 0; i < _phases.Count; i++)
            {
                BossPhaseDefinition phase = _phases[i];
                if (phase == null || phase.PatternCount == 0)
                {
                    reason = $"Phase[{i}] has no patterns.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        /// <summary>테스트 전용 구성 헬퍼.</summary>
        internal void ConfigureForTests(List<BossPhaseDefinition> phases, float counterBonusMultiplier = 1.5f)
        {
            _phases = phases ?? new List<BossPhaseDefinition>();
            _counterBonusMultiplier = counterBonusMultiplier;
        }
    }
}
