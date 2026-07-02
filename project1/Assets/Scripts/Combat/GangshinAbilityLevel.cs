using System;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 스킬의 레벨별 발동 수치(gangshin_balance_mvp.md).
    /// 살풀이 검무 / 파천의 징이 공용으로 사용하며, 사용하지 않는 필드는 0 / false로 둔다.
    /// </summary>
    [Serializable]
    public struct GangshinAbilityLevel
    {
        [SerializeField, Min(0f), Tooltip("발동 데미지")]
        private float _damage;

        [SerializeField, Range(0f, 100f), Tooltip("발동에 필요한 게이지 비율(%). 예: 100 또는 90.")]
        private float _requiredGaugePercent;

        [SerializeField, Min(0f), Tooltip("적 기절 지속시간(초). 0이면 기절 없음.")]
        private float _stunDuration;

        [SerializeField, Tooltip("파동을 2회 연속 발사한다(파천의 징 Lv3).")]
        private bool _doubleWave;

        public GangshinAbilityLevel(float damage, float requiredGaugePercent, float stunDuration, bool doubleWave)
        {
            _damage = Mathf.Max(0f, damage);
            _requiredGaugePercent = Mathf.Clamp(requiredGaugePercent, 0f, 100f);
            _stunDuration = Mathf.Max(0f, stunDuration);
            _doubleWave = doubleWave;
        }

        public float Damage => Mathf.Max(0f, _damage);
        public float RequiredGaugePercent => Mathf.Clamp(_requiredGaugePercent, 0f, 100f);

        /// <summary>발동에 필요한 게이지 정규화 값(0~1). 게이지 임계값 연동(#59)에서 사용.</summary>
        public float RequiredGaugeNormalized => RequiredGaugePercent / 100f;

        public float StunDuration => Mathf.Max(0f, _stunDuration);
        public bool StunsEnemies => StunDuration > 0f;
        public bool DoubleWave => _doubleWave;
    }
}
