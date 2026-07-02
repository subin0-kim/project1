using System;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GangshinInputDetector))]
    [RequireComponent(typeof(PlayerStatSystem))]
    public class GangshinController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private GangshinInputDetector _inputDetector;

        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [Header("Gauge")]
        [SerializeField, Min(1f)]
        private float _maxGauge = 100f;

        [SerializeField, Min(0.1f)]
        private float _gaugePerKill = 20f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)]
        private float _activeDuration = 6f;

        [SerializeField, Min(0f)]
        private float _cooldownDuration = 10f;

        [SerializeField, Range(0.05f, 1f)]
        private float _activeTimeScale = 0.7f;

        [Header("Ability")]
        [SerializeField, Tooltip("발동 시 실행할 강신 필살기(#30). 미지정 시 아래 레거시 펄스로 대체.")]
        private GangshinAbilityBase _equippedAbility;

        [SerializeField, Min(1), Tooltip("발동 레벨(1-based). 슬롯/강화 카드 시스템(#59, #66) 연동 전까지 임시로 사용.")]
        private int _abilityLevel = 1;

        [Header("Effects (Legacy Fallback)")]
        [SerializeField, Tooltip("장착 Ability가 없을 때만 사용하는 레거시 전체 펄스.")]
        private bool _dealActivationPulse = true;

        [SerializeField, Min(0f)]
        private float _activationPulseDamage = 999f;

        [SerializeField]
        private bool _buffAttackPowerWhileActive = true;

        [SerializeField, Min(0f)]
        private float _attackPowerBonusPercent = 1f;

        public event Action<GangshinState> OnStateChanged;
        public event Action<float, float> OnGaugeChanged;
        public event Action OnActivated;

        private GangshinRuntime _runtime;
        private GangshinState _lastState;
        private float _timeScaleBeforeActive = 1f;

        public GangshinState CurrentState => _runtime != null ? _runtime.CurrentState : GangshinState.Idle;
        public float CurrentGauge => _runtime != null ? _runtime.CurrentGauge : 0f;
        public float MaxGauge => _runtime != null ? _runtime.MaxGauge : Mathf.Max(1f, _maxGauge);
        public float GaugeNormalized => _runtime != null ? _runtime.NormalizedGauge : 0f;
        public float RemainingActiveTime => _runtime != null ? _runtime.RemainingActiveTime : 0f;
        public float RemainingCooldownTime => _runtime != null ? _runtime.RemainingCooldownTime : 0f;
        public bool IsReady => CurrentState == GangshinState.Ready;

        private void Awake()
        {
            if (_inputDetector == null)
            {
                _inputDetector = GetComponent<GangshinInputDetector>();
            }

            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }

            _runtime = new GangshinRuntime(_maxGauge, _activeDuration, _cooldownDuration);
            _lastState = _runtime.CurrentState;
            NotifyGaugeChanged();
        }

#if UNITY_EDITOR
        // 인스펙터에서 _abilityLevel을 장착 Ability의 레벨 테이블 범위 밖으로 설정하면 GetLevel이 조용히
        // 마지막 레벨로 클램프된다(예: Lv3 기대 → Lv1 수치). 실수를 조기에 발견하도록 경고만 남긴다(#59 전 임시 필드).
        private void OnValidate()
        {
            if (_equippedAbility != null && _equippedAbility.Data != null
                && _abilityLevel > _equippedAbility.Data.MaxLevel)
            {
                Debug.LogWarning(
                    $"[GangshinController] _abilityLevel({_abilityLevel})이 " +
                    $"{_equippedAbility.Data.name}의 MaxLevel({_equippedAbility.Data.MaxLevel})을 초과합니다. " +
                    "GetLevel이 최대 레벨로 클램프됩니다.",
                    this);
            }
        }
#endif

        private void OnEnable()
        {
            if (_inputDetector != null)
            {
                _inputDetector.OnActivationRequested += HandleActivationRequested;
            }

            EnemyHealth.AnyEnemyDied += HandleAnyEnemyDied;
        }

        private void OnDisable()
        {
            if (_inputDetector != null)
            {
                _inputDetector.OnActivationRequested -= HandleActivationRequested;
            }

            EnemyHealth.AnyEnemyDied -= HandleAnyEnemyDied;
            ExitActiveEffectsIfNeeded();
        }

        private void Update()
        {
            if (_runtime == null)
            {
                return;
            }

            if (_runtime.Tick(Time.unscaledDeltaTime))
            {
                HandleStateTransition();
            }
        }

        public bool TryActivate()
        {
            if (_runtime == null || !_runtime.TryActivate())
            {
                return false;
            }

            HandleStateTransition();
            NotifyGaugeChanged();
            OnActivated?.Invoke();
            return true;
        }

        public bool AddGauge(float amount)
        {
            if (_runtime == null || !_runtime.AddGauge(amount))
            {
                return false;
            }

            NotifyGaugeChanged();
            HandleStateTransition();
            return true;
        }

        private void HandleAnyEnemyDied(EnemyHealth enemyHealth)
        {
            // 강신 게이지 충전 배율(#40)을 적용한다. 스탯이 없으면 1배.
            float chargeRate = PlayerStatSystem.ResolveValueOrDefault(_playerStatSystem, StatType.GangshinGaugeChargeRate, 1f);
            AddGauge(_gaugePerKill * chargeRate);
        }

        private void HandleActivationRequested()
        {
            TryActivate();
        }

        private void HandleStateTransition()
        {
            GangshinState currentState = CurrentState;
            if (currentState == _lastState)
            {
                return;
            }

            if (_lastState == GangshinState.Active)
            {
                ExitActiveEffectsIfNeeded();
            }

            if (currentState == GangshinState.Active)
            {
                EnterActiveEffects();
            }

            _lastState = currentState;
            OnStateChanged?.Invoke(currentState);
        }

        private void EnterActiveEffects()
        {
            _timeScaleBeforeActive = Time.timeScale;
            Time.timeScale = Mathf.Clamp(_activeTimeScale, 0.05f, 1f);

            if (_buffAttackPowerWhileActive && _playerStatSystem != null && _attackPowerBonusPercent > 0f)
            {
                _playerStatSystem.AddModifier(
                    StatType.AttackPower,
                    new StatModifier(_attackPowerBonusPercent, StatModifierType.Percent, this));
            }

            ActivateEquippedAbility();
        }

        /// <summary>
        /// 장착된 강신 필살기(#30)를 발동한다. 미장착 시 레거시 전체 펄스로 대체한다.
        /// 대상 적 목록으로 현재 활성 적 전체를 전달한다.
        /// </summary>
        private void ActivateEquippedAbility()
        {
            if (_equippedAbility != null)
            {
                _equippedAbility.Activate(new GangshinSlotContext(
                    transform.position, _abilityLevel, this, EnemyHealth.ActiveEnemies));
                return;
            }

            if (_dealActivationPulse)
            {
                ApplyActivationPulse();
            }
        }

        private void ExitActiveEffectsIfNeeded()
        {
            if (_playerStatSystem != null)
            {
                _playerStatSystem.RemoveModifiersFromSource(StatType.AttackPower, this);
            }

            if (Mathf.Approximately(Time.timeScale, _activeTimeScale) || Time.timeScale < 1f)
            {
                Time.timeScale = _timeScaleBeforeActive <= 0f ? 1f : _timeScaleBeforeActive;
            }
        }

        private void ApplyActivationPulse()
        {
            if (_activationPulseDamage <= 0f)
            {
                return;
            }

            var activeEnemies = EnemyHealth.ActiveEnemies;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyHealth enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                enemy.ApplyDamage(_activationPulseDamage, this);
            }
        }

        private void NotifyGaugeChanged()
        {
            OnGaugeChanged?.Invoke(CurrentGauge, MaxGauge);
        }
    }
}
