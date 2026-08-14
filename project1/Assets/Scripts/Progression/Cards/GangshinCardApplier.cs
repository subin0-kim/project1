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
    /// 2. 빈 슬롯 있음 → 해당 슬롯에 추가
    /// 3. 슬롯 4개가 모두 참 → <see cref="OnReplaceRequested"/>로 교체 UI에 위임(#59)
    ///
    /// 3번을 처리할 구독자가 없으면 그 카드는 애초에 추첨 대상에서 빠진다
    /// (<see cref="IsCardEligible"/>). 선택했는데 아무 일도 일어나지 않는 "빈 선택"을 막기 위함이다.
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

            _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
            _playerLevelSystem.SetCardEligibilityFilter(IsCardEligible);
        }

        private void OnDisable()
        {
            if (_playerLevelSystem == null)
            {
                return;
            }

            _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
            _playerLevelSystem.ClearCardEligibilityFilter(IsCardEligible);
        }

        /// <summary>
        /// 교체 UI가 슬롯을 고른 뒤 호출한다. 성공 시 해당 슬롯의 강신이 교체된다.
        /// </summary>
        public bool ResolveReplacement(int slotIndex, GangshinAbilityBase ability, int level)
        {
            return _gangshinController != null && _gangshinController.TryReplaceAbility(slotIndex, ability, level);
        }

        /// <summary>
        /// 지금 선택해도 적용할 수 없는 강신 카드를 추첨 대상에서 제외한다.
        /// 강신 카드가 아니면 관여하지 않고 통과시킨다.
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
                return false;
            }

            // 보유 중이면 레벨업으로, 빈 슬롯이 있으면 추가로 적용할 수 있다.
            if (_gangshinController.FindSlotIndex(ability) >= 0 || _gangshinController.HasFreeSlot)
            {
                return true;
            }

            // 슬롯이 모두 찬 경우엔 교체를 처리할 구독자가 있을 때만 제시한다.
            return OnReplaceRequested != null;
        }

        private void HandleSkillEffectPending(SkillData card, int nextLevel)
        {
            if (card == null || card.Category != CardCategory.Gangshin)
            {
                return;
            }

            if (_gangshinController == null)
            {
                Debug.LogWarning($"[GangshinCardApplier] GangshinController 참조가 없어 '{card.SkillId}' 카드를 적용하지 못했습니다.", this);
                return;
            }

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

            // 2) 빈 슬롯 → 추가. 첫 강신이면 자동으로 장착 슬롯이 된다.
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
