using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [CreateAssetMenu(fileName = "SkillData", menuName = "Mukseon/Data/Skill Data")]
    public class SkillData : ScriptableObject
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

        public string SkillId => string.IsNullOrWhiteSpace(_skillId) ? name : _skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public LevelUpSkillEffectType EffectType => _effectType;
        public StatType StatType => _statType;
        public float Value => _value;
        public int MaxLevel => Mathf.Max(1, _maxLevel);

        public bool IsValid(out string reason)
        {
            if (MaxLevel <= 0)
            {
                reason = "Max level must be greater than zero.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
