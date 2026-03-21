using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [CreateAssetMenu(fileName = "PlayerStatsDefinition", menuName = "Mukseon/Stats/Player Stats Definition")]
    public class PlayerStatsDefinition : ScriptableObject
    {
        [SerializeField]
        private List<StatValueDefinition> _stats = new List<StatValueDefinition>();

        public IReadOnlyList<StatValueDefinition> Stats => _stats;
    }
}
