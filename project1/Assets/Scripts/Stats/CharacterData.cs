using System.Collections.Generic;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Mukseon/Data/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [SerializeField]
        private string _characterId = "character.default";

        [SerializeField]
        private string _displayName = "Character";

        [SerializeField]
        private PlayerStatsDefinition _initialStats;

        [SerializeField, Min(0f)]
        private float _baseAttackDamage = 1f;

        [SerializeField, Min(1)]
        private int _targetsPerAttack = 1;

        [SerializeField]
        private List<SkillData> _levelUpSkills = new List<SkillData>();

        public string CharacterId => string.IsNullOrWhiteSpace(_characterId) ? name : _characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public PlayerStatsDefinition InitialStats => _initialStats;
        public float BaseAttackDamage => Mathf.Max(0f, _baseAttackDamage);
        public int TargetsPerAttack => Mathf.Max(1, _targetsPerAttack);
        public IReadOnlyList<SkillData> LevelUpSkills => _levelUpSkills;

        public bool IsValid(out string reason)
        {
            if (_initialStats == null)
            {
                reason = "Initial stats definition is missing.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
