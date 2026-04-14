using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [CreateAssetMenu(fileName = "MonsterData", menuName = "Mukseon/Data/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        [SerializeField]
        private string _monsterId = "monster.default";

        [SerializeField]
        private string _displayName = "Monster";

        [SerializeField]
        private bool _isBoss;

        [SerializeField]
        private EnemyHealth _enemyPrefab;

        [SerializeField]
        private SwipeDirection[] _swipeDirectionSequence = new SwipeDirection[0];

        [SerializeField]
        private bool _randomizeSequence;

        [SerializeField, Min(1f)]
        private float _maxHealth = 10f;

        [SerializeField, Min(0f)]
        private float _moveSpeed = 1f;

        [SerializeField, Min(1)]
        private int _soulDropCount = 1;

        [SerializeField, Min(1)]
        private int _experiencePerOrb = 1;

        public string MonsterId => string.IsNullOrWhiteSpace(_monsterId) ? name : _monsterId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public bool IsBoss => _isBoss;
        public EnemyHealth EnemyPrefab => _enemyPrefab;
        public SwipeDirection[] SwipeDirectionSequence => _swipeDirectionSequence;
        public bool RandomizeSequence => _randomizeSequence;
        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
        public int SoulDropCount => Mathf.Max(1, _soulDropCount);
        public int ExperiencePerOrb => Mathf.Max(1, _experiencePerOrb);

        public bool IsValid(out string reason)
        {
            if (_enemyPrefab == null)
            {
                reason = "Enemy prefab is missing.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
