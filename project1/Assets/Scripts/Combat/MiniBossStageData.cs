using System;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [Serializable]
    public class MiniBossStageData
    {
        [Tooltip("이 차수가 발동되는 분 단위 마크. WaveCombatDirector._miniBossMinuteMarks와 일치해야 한다.")]
        [SerializeField, Min(0f)]
        private float _spawnMinuteMark = 3f;

        [Tooltip("기본 적 체력 대비 배율. 예: 5 → 5배 체력.")]
        [SerializeField, Min(1f)]
        private float _healthMultiplier = 5f;

        [Tooltip("기본 적 크기 대비 배율. 예: 1.5 → 1.5배 크기.")]
        [SerializeField, Min(0.1f)]
        private float _sizeMultiplier = 1.5f;

        [Tooltip("누적 데미지가 최대 체력의 이 비율에 도달하면 넉백. 예: 0.1 → 총 HP의 10%.")]
        [SerializeField, Range(0.01f, 1f)]
        private float _knockbackThresholdRatio = 0.1f;

        [Tooltip("미니 보스의 이동 속도. 느리게 고정하여 처치 기회를 보장한다.")]
        [SerializeField, Min(0f)]
        private float _moveSpeed = 0.5f;

        [Header("Rewards")]
        [Tooltip("처치 시 지급할 골드.")]
        [SerializeField, Min(0)]
        private int _goldReward = 100;

        [Tooltip("처치 시 지급할 영혼 재화.")]
        [SerializeField, Min(0)]
        private int _soulCurrencyReward = 1;

        public float SpawnMinuteMark => _spawnMinuteMark;
        public float HealthMultiplier => _healthMultiplier;
        public float SizeMultiplier => _sizeMultiplier;
        public float KnockbackThresholdRatio => _knockbackThresholdRatio;
        public float MoveSpeed => _moveSpeed;
        public int GoldReward => _goldReward;
        public int SoulCurrencyReward => _soulCurrencyReward;
    }
}
