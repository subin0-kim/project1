using Mukseon.Gameplay.Progression.Cards;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 강화 카드 1장의 정의(#66, `card_system.md`). 스탯 / 스킬 / 강신 카드를 모두 이 에셋으로 표현하며,
    /// 카테고리는 <see cref="EffectType"/>에서 파생한다(별도 필드를 두면 두 값이 어긋날 수 있다).
    /// </summary>
    [CreateAssetMenu(fileName = "SkillData", menuName = "Mukseon/Data/Skill Data")]
    public class SkillData : ScriptableObject, ICardDefinition
    {
        [SerializeField]
        private string _skillId;

        [SerializeField]
        private string _displayName = "Skill";

        [SerializeField, TextArea]
        private string _description;

        [SerializeField]
        private LevelUpSkillEffectType _effectType;

        [SerializeField]
        private StatType _statType = StatType.AttackPower;

        [SerializeField, Min(0f)]
        private float _value = 1f;

        [SerializeField, Min(1)]
        private int _maxLevel = 5;

        [SerializeField]
        private Sprite _icon;

        [SerializeField, Tooltip("이 카드를 사용할 수 있는 캐릭터 ID(예: character.mudang). 비워두면 전 캐릭터 공용.")]
        private string _requiredCharacterId;

        [SerializeField, Tooltip("강신 강화 카드일 때 부여 / 레벨업할 강신 어빌리티 ID(GangshinAbilityData.AbilityId).")]
        private string _gangshinAbilityId;

        public string SkillId => string.IsNullOrWhiteSpace(_skillId) ? name : _skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public LevelUpSkillEffectType EffectType => _effectType;
        public StatType StatType => _statType;
        public float Value => _value;
        public int MaxLevel => Mathf.Max(1, _maxLevel);
        public Sprite Icon => _icon;

        /// <summary>강신 강화 카드가 대상으로 삼는 어빌리티 ID. 강신 카드가 아니면 비어 있다.</summary>
        public string GangshinAbilityId => _gangshinAbilityId;

        public string CardId => SkillId;
        public string RequiredCharacterId => _requiredCharacterId;
        public CardCategory Category => ResolveCategory(_effectType);

        /// <summary>
        /// 효과 타입에서 카드 카테고리를 결정한다(`card_system.md` — 카드 목록).
        /// 스탯 수치를 직접 올리는 효과는 스탯 카드, 강신 슬롯에 얹히는 효과는 강신 카드,
        /// 나머지(도깨비불 / 결계 / 재생 등)는 스킬 카드다.
        /// </summary>
        public static CardCategory ResolveCategory(LevelUpSkillEffectType effectType)
        {
            switch (effectType)
            {
                case LevelUpSkillEffectType.StatFlat:
                case LevelUpSkillEffectType.StatPercent:
                case LevelUpSkillEffectType.BonusTargets:
                case LevelUpSkillEffectType.PickupRadius:
                    return CardCategory.Stat;

                case LevelUpSkillEffectType.SalPulliKummuBuff:
                case LevelUpSkillEffectType.PaCheonJingBuff:
                    return CardCategory.Gangshin;

                default:
                    return CardCategory.Skill;
            }
        }

        public bool IsValid(out string reason)
        {
            if (MaxLevel <= 0)
            {
                reason = "Max level must be greater than zero.";
                return false;
            }

            if (Category == CardCategory.Gangshin && string.IsNullOrWhiteSpace(_gangshinAbilityId))
            {
                reason = "Gangshin card requires a gangshin ability id.";
                return false;
            }

            reason = null;
            return true;
        }

        internal void ConfigureForTests(
            string skillId,
            LevelUpSkillEffectType effectType,
            int maxLevel,
            string requiredCharacterId = null,
            string gangshinAbilityId = null)
        {
            _skillId = skillId;
            _effectType = effectType;
            _maxLevel = maxLevel;
            _requiredCharacterId = requiredCharacterId;
            _gangshinAbilityId = gangshinAbilityId;
        }
    }
}
