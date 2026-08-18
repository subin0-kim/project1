using System;
using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 강신 강화 카드를 실제 강신 슬롯에 반영한다(#66, `card_system.md` — 강신 강화 카드).
    /// 카드는 <see cref="SkillData.GangshinAbilityId"/>로 씬의 강신 어빌리티와 연결된다.
    ///
    /// 적용 규칙(우선순위 순):
    /// 1. 이미 보유 중 → 레벨업(게이지 보존)
    /// 2. 빈 슬롯 있음 → 해당 슬롯에 추가(장착 슬롯이 placeholder면 그 자리를 대체해 즉시 장착)
    /// 3. 슬롯 4개가 모두 참 → <see cref="OnReplaceRequested"/>로 교체 UI에 위임(#59)
    ///
    /// 판정 기준은 "슬롯이 남았는가"가 아니라 "이 카드를 고르면 플레이어가 변화를 느끼는가"다.
    /// 지금 적용해도 아무 변화가 없는 카드는 애초에 추첨 대상에서 뺀다(<see cref="IsCardEligible"/>).
    /// 선택했는데 아무 일도 일어나지 않는 "빈 선택"으로 선택권 1회가 날아가는 것을 막기 위함이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GangshinCardApplier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField]
        private GangshinController _gangshinController;

        [Header("Abilities")]
        [SerializeField, Tooltip("강화 카드로 획득할 수 있는 강신 어빌리티. 카드의 Gangshin Ability Id와 GangshinAbilityData.AbilityId가 일치해야 연결된다.")]
        private List<GangshinAbilityBase> _availableAbilities = new List<GangshinAbilityBase>();

        /// <summary>
        /// 슬롯이 모두 찬 상태에서 새 강신 카드가 선택되었을 때 발생한다.
        /// 교체 UI가 구독해 슬롯을 고른 뒤 <see cref="ResolveReplacement"/>를 호출해야 한다(#59).
        /// </summary>
        public event Action<SkillData, GangshinAbilityBase, int> OnReplaceRequested;

        /// <summary>이 컴포넌트가 처리하는 효과 타입(= <see cref="CardCategory.Gangshin"/>으로 파생되는 타입).</summary>
        private static readonly LevelUpSkillEffectType[] HandledEffectTypes =
        {
            LevelUpSkillEffectType.SalPulliKummuBuff,
            LevelUpSkillEffectType.PaCheonJingBuff,
        };

        /// <summary>
        /// 장착 슬롯 전환 UI(#59 후속)가 준비되었는지. 비장착 슬롯의 강신은 패시브도 게이지 충전도
        /// 받지 못하므로, 전환 수단이 없는 동안에는 "빈 슬롯에 추가만 되는" 강신 카드를 추첨에서 제외한다.
        /// 전환 UI가 <see cref="GangshinController.TryEquipSlot"/>을 호출할 수 있게 되는 시점에 true로
        /// 설정하면 해당 카드들이 자동으로 후보에 복귀한다(<see cref="OnReplaceRequested"/> 구독과 같은 원리).
        /// </summary>
        public bool SlotSwitchAvailable { get; set; }

        // 이미 경고한 카드 ID(추첨은 레벨업마다 반복되므로 같은 설정 오류를 한 번만 알린다).
        private readonly HashSet<string> _warnedUnresolvableCards = new HashSet<string>(StringComparer.Ordinal);

        // 등록 여부를 참조 상태와 분리해 기억한다. 런 도중 컨트롤러가 파괴되어도 OnDisable이
        // 등록을 그대로 남기지 않도록(조건이 남으면 강신 카드가 영영 제외된다) 하기 위함이다.
        private bool _registered;

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponentInParent<PlayerLevelSystem>();
            }

            if (_gangshinController == null)
            {
                _gangshinController = GetComponentInParent<GangshinController>();
            }
        }

        private void OnEnable()
        {
            if (_playerLevelSystem == null)
            {
                Debug.LogWarning("[GangshinCardApplier] PlayerLevelSystem 참조가 없어 강신 카드가 적용되지 않습니다.", this);
                return;
            }

            // 컨트롤러가 없으면 강신 카드를 적용할 수단이 없다. 이때는 추첨 조건도 걸지 않고 처리자로도
            // 등록하지 않는다 — 조건만 걸면 강신 카드가 조용히 후보에서 사라져 원인이 드러나지 않는다.
            // 등록을 모두 건너뛰면 카드가 그대로 제시되고, 선택 시 PlayerLevelSystem이 경고한다.
            if (_gangshinController == null)
            {
                Debug.LogWarning("[GangshinCardApplier] GangshinController 참조가 없어 강신 카드가 적용되지 않습니다.", this);
                return;
            }

            _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
            _playerLevelSystem.AddCardEligibilityFilter(IsCardEligible);

            for (int i = 0; i < HandledEffectTypes.Length; i++)
            {
                _playerLevelSystem.RegisterEffectHandler(HandledEffectTypes[i]);
            }

            _registered = true;
        }

        private void OnDisable()
        {
            if (!_registered || _playerLevelSystem == null)
            {
                return;
            }

            _registered = false;
            _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
            _playerLevelSystem.RemoveCardEligibilityFilter(IsCardEligible);

            for (int i = 0; i < HandledEffectTypes.Length; i++)
            {
                _playerLevelSystem.UnregisterEffectHandler(HandledEffectTypes[i]);
            }
        }

        /// <summary>
        /// 교체 UI가 슬롯을 고른 뒤 호출한다. 성공 시 해당 슬롯의 강신이 교체된다.
        /// </summary>
        public bool ResolveReplacement(int slotIndex, GangshinAbilityBase ability, int level)
        {
            return _gangshinController != null && _gangshinController.TryReplaceAbility(slotIndex, ability, level);
        }

        /// <summary>
        /// 지금 선택해도 플레이어가 변화를 느끼지 못하는 강신 카드를 추첨 대상에서 제외한다.
        /// 강신 카드가 아니면 관여하지 않고 통과시킨다.
        ///
        /// 제외 사유는 성격이 둘로 갈린다. 기능 미구현(#59 전환 / 교체 UI)으로 인한 제외는 의도된
        /// 것이라 조용히 빼지만, 설정 오류(어빌리티 ID 오타 / Available Abilities 누락)는 경고한다
        /// — 그러지 않으면 "카드가 영영 안 뜬다"로만 나타나 원인을 추적할 단서가 없다.
        /// </summary>
        private bool IsCardEligible(SkillData card)
        {
            if (card == null || card.Category != CardCategory.Gangshin)
            {
                return true;
            }

            GangshinAbilityBase ability = FindAbility(card.GangshinAbilityId);
            if (ability == null || _gangshinController == null)
            {
                WarnUnresolvableCardOnce(card);
                return false;
            }

            // 1) 이미 보유 중 → 레벨업. 항상 체감된다.
            if (_gangshinController.FindSlotIndex(ability) >= 0)
            {
                return true;
            }

            // 2) 장착 슬롯이 어빌리티 없는 placeholder면 새 강신이 그 자리를 대체해 즉시 장착된다.
            if (_gangshinController.CanPromoteAddedAbility)
            {
                return true;
            }

            // 3) 빈 슬롯에 "추가만" 되는 경우: 비장착 슬롯은 패시브도 게이지 충전도 받지 못하므로,
            //    플레이어가 장착을 바꿀 수 있을 때(전환 UI, #59)만 제시한다.
            if (_gangshinController.HasFreeSlot)
            {
                return SlotSwitchAvailable;
            }

            // 4) 슬롯이 모두 찬 경우엔 교체를 처리할 구독자가 있을 때만 제시한다.
            return OnReplaceRequested != null;
        }

        private void HandleSkillEffectPending(SkillData card, int nextLevel)
        {
            if (card == null || card.Category != CardCategory.Gangshin)
            {
                return;
            }

            // 컨트롤러가 없으면 OnEnable에서 구독 자체를 하지 않으므로, 여기 오는 경우는 런 도중
            // 컨트롤러가 파괴된 상황뿐이다(그대로 두면 아래에서 MissingReferenceException).
            if (_gangshinController == null)
            {
                Debug.LogWarning($"[GangshinCardApplier] GangshinController 참조가 없어 '{card.SkillId}' 카드를 적용하지 못했습니다.", this);
                return;
            }

            // 추첨으로 온 카드는 IsCardEligible이 이미 걸러내지만, CharacterData.StartingSkills로 부여되는
            // 강신 카드는 카드 풀을 거치지 않으므로(GrantStartingSkills) 이 경로로 들어올 수 있다.
            GangshinAbilityBase ability = FindAbility(card.GangshinAbilityId);
            if (ability == null)
            {
                Debug.LogWarning($"[GangshinCardApplier] 카드 '{card.SkillId}'의 강신 어빌리티 ID '{card.GangshinAbilityId}'에 해당하는 어빌리티를 찾지 못했습니다.", this);
                return;
            }

            // 1) 이미 보유 중 → 레벨업(게이지 보존).
            if (_gangshinController.TryUpgradeAbility(ability, nextLevel))
            {
                return;
            }

            // 2) 빈 슬롯 → 추가. 장착 슬롯이 placeholder면 그 자리를 대체해 곧바로 장착된다.
            if (_gangshinController.TryAddAbility(ability, nextLevel) >= 0)
            {
                return;
            }

            // 3) 슬롯이 모두 참 → 교체 UI에 위임.
            if (OnReplaceRequested != null)
            {
                OnReplaceRequested.Invoke(card, ability, nextLevel);
                return;
            }

            Debug.LogWarning($"[GangshinCardApplier] 슬롯이 모두 차 있고 교체 UI 구독자가 없어 '{card.SkillId}' 카드를 적용하지 못했습니다.", this);
        }

        /// <summary>
        /// 어빌리티를 찾지 못해 제외되는 강신 카드를 카드당 한 번만 경고한다.
        /// 추첨은 레벨업마다 반복되므로 매번 찍으면 로그가 묻힌다.
        /// </summary>
        private void WarnUnresolvableCardOnce(SkillData card)
        {
            if (!_warnedUnresolvableCards.Add(card.SkillId))
            {
                return;
            }

            Debug.LogWarning(
                $"[GangshinCardApplier] 강신 카드 '{card.SkillId}'의 어빌리티 ID '{card.GangshinAbilityId}'에 해당하는 " +
                "어빌리티가 없어 추첨에서 제외됩니다. Available Abilities 배선과 GangshinAbilityData.AbilityId를 확인하세요.",
                this);
        }

        private GangshinAbilityBase FindAbility(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            for (int i = 0; i < _availableAbilities.Count; i++)
            {
                GangshinAbilityBase ability = _availableAbilities[i];
                if (ability == null || ability.Data == null)
                {
                    continue;
                }

                if (string.Equals(ability.Data.AbilityId, abilityId, StringComparison.Ordinal))
                {
                    return ability;
                }
            }

            return null;
        }
    }
}
