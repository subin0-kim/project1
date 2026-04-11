using Mukseon.Core.Input;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Data/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField]
        private string _monsterId = "monster.default";

        [SerializeField]
        private string _displayName = "Monster";

        [SerializeField]
        private EnemyBase _enemyPrefab;

        [SerializeField]
        private SwipeDirection _swipeDirection = SwipeDirection.None;

        [SerializeField, Min(1f)]
        private float _maxHealth = 10f;

        [SerializeField, Min(0f)]
        private float _moveSpeed = 1f;

        [SerializeField, Min(1)]
        private int _soulDropCount = 1;

        [SerializeField, Min(1)]
        private int _experiencePerOrb = 1;

        [SerializeField]
        private SoulOrb _soulOrbPrefab;

        [SerializeField, Min(0f)]
        private float _dropRadius = 0.3f;

        public string MonsterId => string.IsNullOrWhiteSpace(_monsterId) ? name : _monsterId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public EnemyBase EnemyPrefab => _enemyPrefab;
        public SwipeDirection SwipeDirection => _swipeDirection;
        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
        public int SoulDropCount => Mathf.Max(1, _soulDropCount);
        public int ExperiencePerOrb => Mathf.Max(1, _experiencePerOrb);
        public SoulOrb SoulOrbPrefab => _soulOrbPrefab;
        public float DropRadius => Mathf.Max(0f, _dropRadius);

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
