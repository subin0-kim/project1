using System;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 보스 패턴 1종의 데이터(#69). <see cref="MountainKingBossPatternData"/>가 페이즈별로 리스트로 담는다.
    /// 인디케이터 위치는 코드에 하드코딩하지 않고 <see cref="IndicatorOffset"/>으로 Inspector에서 조정한다
    /// (아트 완성 후 위치 조율).
    /// </summary>
    [Serializable]
    public class BossPatternDefinition
    {
        [SerializeField]
        private BossPatternType _type = BossPatternType.Charge;

        [Tooltip("예고 시간(초). 패턴 표시 후 실제 발동까지의 대기. 인디케이터가 이 동안 노출된다.")]
        [SerializeField, Min(0f)]
        private float _telegraphSeconds = 1f;

        [Tooltip("발동(실행) 시간(초). 돌진 이동/할퀴기 이펙트 등 패턴 본동작 지속 시간.")]
        [SerializeField, Min(0f)]
        private float _executeSeconds = 0.5f;

        [Tooltip("후딜(복귀) 시간(초). 패턴 종료 후 다음 패턴 주기 시작 전 회복 시간.")]
        [SerializeField, Min(0f)]
        private float _recoverSeconds = 0.5f;

        [SerializeField]
        private BossCounterType _counterType = BossCounterType.BossDirection;

        [Tooltip("카운터 입력을 받는 시간(초). 0 이하이면 예고+발동 시간을 윈도우로 사용한다.")]
        [SerializeField, Min(0f)]
        private float _counterWindowSeconds = 1f;

        [Tooltip("미처리(카운터 실패/시간초과) 시 플레이어가 받는 피해.")]
        [SerializeField, Min(0f)]
        private float _unhandledDamage = 10f;

        [Tooltip("카운터 성공 시 보스에 가하는 기본 피해. 데이터의 카운터 보너스 배율이 곱해진다.")]
        [SerializeField, Min(0f)]
        private float _counterBonusDamage = 20f;

        [Tooltip("DamageThreshold 카운터 전용 — 카운터 윈도우 동안 보스에 누적해야 하는 피해량. 도달 시 패턴 파훼. 다른 카운터 타입에서는 무시된다.")]
        [SerializeField, Min(0f)]
        private float _counterDamageThreshold = 0f;

        [Tooltip("인디케이터 위치 오프셋(보스 기준, 월드 단위). 하드코딩 금지 — 아트 기준 수동 조정.")]
        [SerializeField]
        private Vector2 _indicatorOffset = Vector2.zero;

        [Tooltip("포효 전용 — 소환할 부하 호랑이 수. 0이면 부하를 소환하지 않는다(돌진/할퀴기).")]
        [SerializeField, Min(0)]
        private int _minionCount = 0;

        [Tooltip("DirectionSequence 카운터 전용 — 순서대로 스와이프해야 하는 방향 개수(연속 할퀴기). 2 미만이면 단일 방향 카운터와 동일.")]
        [SerializeField, Min(0)]
        private int _counterSequenceLength = 0;

        public BossPatternType Type => _type;
        public float TelegraphSeconds => Mathf.Max(0f, _telegraphSeconds);
        public float ExecuteSeconds => Mathf.Max(0f, _executeSeconds);
        public float RecoverSeconds => Mathf.Max(0f, _recoverSeconds);
        public BossCounterType CounterType => _counterType;
        public float UnhandledDamage => Mathf.Max(0f, _unhandledDamage);
        public float CounterBonusDamage => Mathf.Max(0f, _counterBonusDamage);
        public float CounterDamageThreshold => Mathf.Max(0f, _counterDamageThreshold);
        public Vector2 IndicatorOffset => _indicatorOffset;
        public int MinionCount => Mathf.Max(0, _minionCount);
        public int CounterSequenceLength => Mathf.Max(0, _counterSequenceLength);

        /// <summary>카운터로 파훼 가능한 패턴인지 여부.</summary>
        public bool IsCounterable => _counterType != BossCounterType.None;

        /// <summary>
        /// 패턴 인디케이터(색 오브)를 표시할지 여부. 카운터 방향이 본체와 독립인
        /// <see cref="BossCounterType.PatternDirection"/>(할퀴기) 또는
        /// <see cref="BossCounterType.DirectionSequence"/>(연속 할퀴기)일 때 표시한다.
        /// 돌진(BossDirection)은 본체 색이 곧 카운터 색이라, 포효(None)는 카운터가 없어 불필요하다.
        /// </summary>
        public bool ShowsIndicator =>
            _counterType == BossCounterType.PatternDirection ||
            _counterType == BossCounterType.DirectionSequence;

        /// <summary>실제 카운터 입력 수령 시간(초). 미지정(0 이하) 시 예고+발동 시간으로 폴백.</summary>
        public float ResolvedCounterWindowSeconds =>
            _counterWindowSeconds > 0f ? _counterWindowSeconds : TelegraphSeconds + ExecuteSeconds;

        public BossPatternDefinition()
        {
        }

        /// <summary>테스트 전용 구성자. Inspector 직렬화 대신 코드로 값을 주입한다.</summary>
        internal BossPatternDefinition(
            BossPatternType type,
            BossCounterType counterType,
            float telegraphSeconds = 1f,
            float executeSeconds = 0.5f,
            float recoverSeconds = 0.5f,
            float counterWindowSeconds = 1f,
            float unhandledDamage = 10f,
            float counterBonusDamage = 20f,
            int minionCount = 0,
            Vector2 indicatorOffset = default,
            float counterDamageThreshold = 0f,
            int counterSequenceLength = 0)
        {
            _type = type;
            _counterType = counterType;
            _telegraphSeconds = telegraphSeconds;
            _executeSeconds = executeSeconds;
            _recoverSeconds = recoverSeconds;
            _counterWindowSeconds = counterWindowSeconds;
            _unhandledDamage = unhandledDamage;
            _counterBonusDamage = counterBonusDamage;
            _minionCount = minionCount;
            _indicatorOffset = indicatorOffset;
            _counterSequenceLength = counterSequenceLength;
            _counterDamageThreshold = counterDamageThreshold;
        }
    }
}
