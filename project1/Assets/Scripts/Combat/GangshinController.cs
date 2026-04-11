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

        [Header("Effects")]
        [SerializeField]
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

        private void OnEnable()
        {
            if (_inputDetector != null)
            {
                _inputDetector.OnActivationRequested += HandleActivationRequested;
            }

            EnemyBase.AnyEnemyDied += HandleAnyEnemyDied;
        }

        private void OnDisable()
        {
            if (_inputDetector != null)
            {
                _inputDetector.OnActivationRequested -= HandleActivationRequested;
            }

            EnemyBase.AnyEnemyDied -= HandleAnyEnemyDied;
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

        private void HandleAnyEnemyDied(EnemyBase enemyHealth)
        {
            AddGauge(_gaugePerKill);
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

            var activeEnemies = EnemyBase.ActiveEnemies;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyBase enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                enemy.TakeDamage(_activationPulseDamage);
            }
        }

        private void NotifyGaugeChanged()
        {
            OnGaugeChanged?.Invoke(CurrentGauge, MaxGauge);
        }
    }
}
