using System;
using System.Collections.Generic;
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
        [SerializeField, Tooltip("캐릭터 기본 강신(슬롯 1에 시드). 미지정 시 아래 레거시 펄스로 대체.")]
        private GangshinAbilityBase _equippedAbility;

        [SerializeField, Min(1), Tooltip("기본 강신 발동 레벨(1-based). 강화 카드 시스템(#66) 연동 전까지 임시로 사용.")]
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

        /// <summary>보유 슬롯 구성이 바뀔 때(추가/교체) 발행. HUD 슬롯 표시(후속 PR)가 구독한다.</summary>
        public event Action OnSlotsChanged;

        /// <summary>장착 슬롯이 바뀔 때 발행(인자: 새 장착 인덱스). HUD 강조 갱신(후속 PR)이 구독한다.</summary>
        public event Action<int> OnActiveSlotChanged;

        private GangshinSlotState _slotState;
        private GangshinPassiveApplier _passiveApplier;
        private GangshinActivationEffects _activationEffects;
        private GangshinState _lastState;

        public GangshinState CurrentState => _slotState != null ? _slotState.CurrentState : GangshinState.Idle;
        public float CurrentGauge => _slotState != null ? _slotState.ActiveGauge : 0f;

        // HUD가 게이지 라벨/비율 분모로 사용한다. 패시브 전용(필요치 0) 슬롯은 0 나눗셈을 피해 _maxGauge로 폴백.
        public float MaxGauge
        {
            get
            {
                float required = _slotState != null ? _slotState.ActiveRequiredGauge : 0f;
                return required > 0f ? required : Mathf.Max(1f, _maxGauge);
            }
        }

        public float GaugeNormalized => _slotState != null ? _slotState.ActiveGaugeNormalized : 0f;
        public float RemainingActiveTime => _slotState != null ? _slotState.RemainingActiveTime : 0f;
        public float RemainingCooldownTime => _slotState != null ? _slotState.RemainingCooldownTime : 0f;
        public bool IsReady => CurrentState == GangshinState.Ready;

        /// <summary>보유 강신 슬롯 목록(최대 4). HUD 슬롯 표시(후속 PR)가 읽는다.</summary>
        public IReadOnlyList<GangshinSlot> Slots => _slotState?.Slots;

        /// <summary>현재 장착 슬롯 인덱스(보유 강신이 없으면 -1).</summary>
        public int ActiveSlotIndex => _slotState != null ? _slotState.ActiveIndex : -1;

        /// <summary>남은 빈 슬롯이 있는지(레벨업 강신 추가 가능 여부, 후속 PR에서 사용).</summary>
        public bool HasFreeSlot => _slotState != null && _slotState.HasFreeSlot;

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

            _slotState = new GangshinSlotState(_maxGauge, _activeDuration, _cooldownDuration, GangshinSlotState.DefaultCapacity);
            _passiveApplier = new GangshinPassiveApplier(_playerStatSystem);
            _activationEffects = new GangshinActivationEffects(
                _playerStatSystem, this, _buffAttackPowerWhileActive, _attackPowerBonusPercent,
                _activeTimeScale, _dealActivationPulse, _activationPulseDamage);

            // 캐릭터 기본 강신을 슬롯 1에 시드한다. 어빌리티가 없어도 슬롯을 채워(필요치 = _maxGauge)
            // 레거시 전체 펄스 동작을 보존한다.
            _slotState.AddSlot(_equippedAbility, ResolveRequiredGauge(_equippedAbility, _abilityLevel));
            SyncActivePassives();

            _lastState = _slotState.CurrentState;
            NotifyGaugeChanged();
        }

#if UNITY_EDITOR
        // 인스펙터에서 _abilityLevel을 기본 강신의 레벨 테이블 범위 밖으로 설정하면 GetLevel이 조용히
        // 마지막 레벨로 클램프된다(예: Lv3 기대 → Lv1 수치). 실수를 조기에 발견하도록 경고만 남긴다.
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
            _activationEffects?.Exit();
            _passiveApplier?.Clear();
        }

        private void Update()
        {
            if (_slotState == null)
            {
                return;
            }

            if (_slotState.Tick(Time.unscaledDeltaTime))
            {
                HandleStateTransition();
            }
        }

        public bool TryActivate()
        {
            if (_slotState == null || !_slotState.TryActivate())
            {
                return false;
            }

            HandleStateTransition();
            NotifyGaugeChanged();
            OnActivated?.Invoke();
            return true;
        }

        /// <summary>장착 슬롯 게이지를 충전한다(외부 호환용 — 내부는 처치 이벤트로 자동 충전).</summary>
        public bool AddGauge(float amount)
        {
            if (_slotState == null || !_slotState.AddGaugeToActive(amount))
            {
                return false;
            }

            NotifyGaugeChanged();
            HandleStateTransition();
            return true;
        }

        /// <summary>
        /// 빈 슬롯에 강신을 추가한다(레벨업 연동 — 후속 PR에서 호출). 성공 시 슬롯 인덱스,
        /// 슬롯이 모두 차 있으면 -1을 반환한다(호출자가 교체 UI를 띄운다).
        /// </summary>
        public int TryAddAbility(GangshinAbilityBase ability, int level = 1)
        {
            if (_slotState == null)
            {
                return -1;
            }

            int index = _slotState.AddSlot(ability, ResolveRequiredGauge(ability, level));
            if (index < 0)
            {
                return -1;
            }

            // 첫 강신이 이 호출로 장착되었다면 패시브를 반영한다.
            SyncActivePassives();
            OnSlotsChanged?.Invoke();
            NotifyGaugeChanged();
            return index;
        }

        /// <summary>슬롯이 모두 찼을 때 지정 슬롯의 강신을 교체한다(레벨업 연동 — 후속 PR에서 호출).</summary>
        public bool TryReplaceAbility(int slotIndex, GangshinAbilityBase ability, int level = 1)
        {
            if (_slotState == null || !_slotState.ReplaceSlot(slotIndex, ability, ResolveRequiredGauge(ability, level)))
            {
                return false;
            }

            // 장착 슬롯을 교체했다면 패시브가 바뀔 수 있으므로 재동기화한다.
            SyncActivePassives();
            OnSlotsChanged?.Invoke();
            NotifyGaugeChanged();
            HandleStateTransition();
            return true;
        }

        /// <summary>장착 슬롯을 교체한다(전투 중 자유 교체 — 후속 PR의 UI 탭에서 호출).</summary>
        public bool TryEquipSlot(int slotIndex)
        {
            if (_slotState == null || !_slotState.SetActive(slotIndex))
            {
                return false;
            }

            // 교체 시 이전 강신 패시브 즉시 해제 + 새 강신 패시브 활성화.
            SyncActivePassives();
            OnActiveSlotChanged?.Invoke(_slotState.ActiveIndex);
            NotifyGaugeChanged();
            HandleStateTransition();
            return true;
        }

        private void HandleAnyEnemyDied(EnemyHealth enemyHealth)
        {
            // 강신 게이지 충전량 = 기본 충전량 × 게이지 충전 배율(#40 스탯) × 강신별 충전 배율(#59 데이터).
            float chargeRate = PlayerStatSystem.ResolveValueOrDefault(_playerStatSystem, StatType.GangshinGaugeChargeRate, 1f);
            float abilityMultiplier = ResolveActiveChargeMultiplier();
            AddGauge(_gaugePerKill * chargeRate * abilityMultiplier);
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
                _activationEffects?.Exit();
            }

            if (currentState == GangshinState.Active)
            {
                // 발동 원점은 플레이어(중앙) 위치. 대상 적/레거시 펄스는 헬퍼가 처리한다.
                _activationEffects?.Enter(_slotState?.ActiveSlot?.Ability, transform.position, _abilityLevel);
            }

            _lastState = currentState;
            OnStateChanged?.Invoke(currentState);
        }

        /// <summary>장착 슬롯 어빌리티의 패시브를 실제 적용 상태와 일치시킨다(장착/교체/추가 후 호출).</summary>
        private void SyncActivePassives()
        {
            _passiveApplier?.Sync(_slotState?.ActiveSlot?.Ability);
        }

        /// <summary>발동에 필요한 게이지 = 필요 게이지 비율(0~1) × 최대 게이지. 0이면 패시브 전용.</summary>
        private float ResolveRequiredGauge(GangshinAbilityBase ability, int level)
        {
            float normalized = ability != null ? ability.GetRequiredGaugeNormalized(level) : 1f;
            return Mathf.Clamp01(normalized) * Mathf.Max(1f, _maxGauge);
        }

        private float ResolveActiveChargeMultiplier()
        {
            GangshinAbilityData data = _slotState?.ActiveSlot?.Ability != null
                ? _slotState.ActiveSlot.Ability.Data
                : null;
            return data != null ? data.GaugeChargeMultiplier : 1f;
        }

        private void NotifyGaugeChanged()
        {
            OnGaugeChanged?.Invoke(CurrentGauge, MaxGauge);
        }
    }
}
