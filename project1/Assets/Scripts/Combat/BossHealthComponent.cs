using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 보스 공통 체력 · 페이즈 컴포넌트(#37). <see cref="EnemyHealth"/> 위에 얹어 보스 전용 동작을 더한다:
    /// <list type="bullet">
    /// <item><see cref="BossData.TotalHealth"/>로 EnemyHealth 최대 체력을 덮어쓴다.</item>
    /// <item>체력 비율이 페이즈 임계값을 넘으면 <see cref="OnPhaseThresholdReached"/>를 발행한다(감지만 — 실제
    /// 페이즈 전환/연출은 "현재 패턴 종료 후" #69 보스 컨트롤러가 처리).</item>
    /// <item>등장/페이즈 전환 연출용 무적 토글을 제공한다(<see cref="SetInvincible"/> → EnemyHealth.IsTargetable).</item>
    /// </list>
    /// 플레이어 스와이프 타격과 상단 HP바(GameplayHudBootstrapper)는 EnemyHealth/IsBoss 경로를 그대로 재사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class BossHealthComponent : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        [SerializeField]
        private BossData _bossData;

        private EnemyHealth _enemyHealth;
        private bool _subscribed;
        private int _currentPhaseIndex;

        public BossData BossData => _bossData;
        public int CurrentPhaseIndex => _currentPhaseIndex;
        public int PhaseCount => _bossData != null ? _bossData.PhaseCount : 1;
        public bool IsInvincible => Health != null && !Health.IsTargetable;

        /// <summary>페이즈 임계값 도달 시 발행. 인자: 진입할 페이즈 인덱스(1부터). #69 보스 컨트롤러가 구독.</summary>
        public event Action<int> OnPhaseThresholdReached;

        /// <summary>보스 사망 시 발행(= EnemyHealth.OnDeath 중계). #37 BossEncounterDirector가 구독.</summary>
        public event Action<BossHealthComponent> OnDefeated;

        private EnemyHealth Health
        {
            get
            {
                if (_enemyHealth == null)
                {
                    _enemyHealth = GetComponent<EnemyHealth>();
                }

                return _enemyHealth;
            }
        }

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// 스폰 직후 <see cref="BossEncounterDirector"/>가 호출. BossData를 적용해 EnemyHealth 최대 체력을
        /// 총 체력으로 설정하고 페이즈 상태를 초기화한다.
        /// </summary>
        public void Initialize()
        {
            if (_bossData == null)
            {
                Debug.LogWarning("[BossHealthComponent] BossData가 비어 있어 초기화를 건너뜁니다.", this);
                return;
            }

            if (!_bossData.IsValid(out string reason))
            {
                Debug.LogWarning($"[BossHealthComponent] BossData '{_bossData.name}' 무효: {reason}", this);
            }

            EnemyHealth health = Health;
            if (health != null)
            {
                health.SetMaxHealth(_bossData.TotalHealth);
                health.ResetHealth();
            }

            _currentPhaseIndex = 0;
            Subscribe();
        }

        /// <summary>등장 연출/페이즈 전환 연출 동안 무적 처리. EnemyHealth.IsTargetable을 토글한다.</summary>
        public void SetInvincible(bool invincible)
        {
            EnemyHealth health = Health;
            if (health != null)
            {
                health.IsTargetable = !invincible;
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            EnemyHealth health = Health;
            if (health == null)
            {
                return;
            }

            health.OnDamaged += HandleDamaged;
            health.OnDeath += HandleDeath;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (_enemyHealth != null)
            {
                _enemyHealth.OnDamaged -= HandleDamaged;
                _enemyHealth.OnDeath -= HandleDeath;
            }

            _subscribed = false;
        }

        // EnemyHealth.OnDamaged 시그니처: (현재 체력, 실제 데미지)
        private void HandleDamaged(float currentHealth, float actualDamage)
        {
            if (_bossData == null || _enemyHealth == null)
            {
                return;
            }

            float fraction = currentHealth / Mathf.Max(1f, _enemyHealth.MaxHealth);
            int newPhase = ComputePhaseIndex(fraction, _bossData.PhaseHealthThresholds);

            // 한 번의 큰 피해로 여러 임계값을 동시에 넘을 수 있으므로 순차적으로 발행한다.
            while (_currentPhaseIndex < newPhase)
            {
                _currentPhaseIndex++;
                OnPhaseThresholdReached?.Invoke(_currentPhaseIndex);
            }
        }

        private void HandleDeath(EnemyHealth health)
        {
            OnDefeated?.Invoke(this);
        }

        /// <summary>
        /// 체력 비율(0~1)에 대한 페이즈 인덱스(0부터)를 계산한다. 비율이 임계값 이하로 내려간 임계값의 개수.
        /// 임계값은 내림차순을 가정하나, 개수 기반이라 순서가 흐트러져도 동작한다.
        /// </summary>
        internal static int ComputePhaseIndex(float healthFraction, IReadOnlyList<float> thresholds)
        {
            if (thresholds == null || thresholds.Count == 0)
            {
                return 0;
            }

            int phase = 0;
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (healthFraction <= thresholds[i] + Epsilon)
                {
                    phase++;
                }
            }

            return phase;
        }

        /// <summary>테스트 전용 — 인스펙터 대신 코드로 BossData를 주입한다.</summary>
        internal void SetBossDataForTests(BossData data)
        {
            _bossData = data;
        }
    }
}
