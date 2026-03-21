using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [DisallowMultipleComponent]
    public class PlayerStatSystem : MonoBehaviour
    {
        [SerializeField]
        private PlayerStatsDefinition _initialStats;

        private readonly Dictionary<StatType, RuntimeStat> _runtimeStats = new Dictionary<StatType, RuntimeStat>();

        public event Action<StatType, float> OnStatChanged;

        private void Awake()
        {
            InitializeFromDefinition();
        }

        public void InitializeFromDefinition()
        {
            _runtimeStats.Clear();

            if (_initialStats == null)
            {
                Debug.LogWarning("[PlayerStatSystem] Initial stat definition is missing.");
                return;
            }

            for (int i = 0; i < _initialStats.Stats.Count; i++)
            {
                StatValueDefinition definition = _initialStats.Stats[i];
                _runtimeStats[definition.StatType] = new RuntimeStat(definition.BaseValue);
            }
        }

        public bool TryGetRuntimeStat(StatType statType, out RuntimeStat runtimeStat)
        {
            return _runtimeStats.TryGetValue(statType, out runtimeStat);
        }

        public float GetValue(StatType statType)
        {
            return _runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat) ? runtimeStat.Value : 0f;
        }

        public bool AddModifier(StatType statType, StatModifier modifier)
        {
            if (!_runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat))
            {
                return false;
            }

            runtimeStat.AddModifier(modifier);
            OnStatChanged?.Invoke(statType, runtimeStat.Value);
            return true;
        }

        public bool RemoveModifier(StatType statType, StatModifier modifier)
        {
            if (!_runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat))
            {
                return false;
            }

            bool removed = runtimeStat.RemoveModifier(modifier);
            if (removed)
            {
                OnStatChanged?.Invoke(statType, runtimeStat.Value);
            }

            return removed;
        }

        public int RemoveModifiersFromSource(StatType statType, object source)
        {
            if (!_runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat))
            {
                return 0;
            }

            int removed = runtimeStat.RemoveAllModifiersFromSource(source);
            if (removed > 0)
            {
                OnStatChanged?.Invoke(statType, runtimeStat.Value);
            }

            return removed;
        }
    }
}
