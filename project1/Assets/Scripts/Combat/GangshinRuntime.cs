using System;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [Serializable]
    public sealed class GangshinRuntime
    {
        private readonly float _maxGauge;
        private readonly float _activeDuration;
        private readonly float _cooldownDuration;

        public GangshinRuntime(float maxGauge, float activeDuration, float cooldownDuration)
        {
            _maxGauge = Mathf.Max(1f, maxGauge);
            _activeDuration = Mathf.Max(0.1f, activeDuration);
            _cooldownDuration = Mathf.Max(0f, cooldownDuration);
            CurrentState = GangshinState.Idle;
        }

        public GangshinState CurrentState { get; private set; }
        public float CurrentGauge { get; private set; }
        public float MaxGauge => _maxGauge;
        public float NormalizedGauge => Mathf.Clamp01(CurrentGauge / _maxGauge);
        public float RemainingActiveTime { get; private set; }
        public float RemainingCooldownTime { get; private set; }

        public bool AddGauge(float amount)
        {
            if (amount <= 0f || CurrentState == GangshinState.Active)
            {
                return false;
            }

            float previousGauge = CurrentGauge;
            CurrentGauge = Mathf.Clamp(CurrentGauge + amount, 0f, _maxGauge);

            if (CurrentState == GangshinState.Idle && Mathf.Approximately(CurrentGauge, _maxGauge))
            {
                CurrentState = GangshinState.Ready;
            }

            return !Mathf.Approximately(previousGauge, CurrentGauge);
        }

        public bool TryActivate()
        {
            if (CurrentState != GangshinState.Ready || CurrentGauge < _maxGauge)
            {
                return false;
            }

            CurrentGauge = 0f;
            RemainingActiveTime = _activeDuration;
            RemainingCooldownTime = 0f;
            CurrentState = GangshinState.Active;
            return true;
        }

        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return false;
            }

            switch (CurrentState)
            {
                case GangshinState.Active:
                    RemainingActiveTime = Mathf.Max(0f, RemainingActiveTime - deltaTime);
                    if (RemainingActiveTime <= 0f)
                    {
                        RemainingCooldownTime = _cooldownDuration;
                        CurrentState = RemainingCooldownTime > 0f ? GangshinState.Cooldown : ResolveRestingState();
                        return true;
                    }
                    break;
                case GangshinState.Cooldown:
                    RemainingCooldownTime = Mathf.Max(0f, RemainingCooldownTime - deltaTime);
                    if (RemainingCooldownTime <= 0f)
                    {
                        CurrentState = ResolveRestingState();
                        return true;
                    }
                    break;
            }

            return false;
        }

        private GangshinState ResolveRestingState()
        {
            return CurrentGauge >= _maxGauge ? GangshinState.Ready : GangshinState.Idle;
        }
    }
}
