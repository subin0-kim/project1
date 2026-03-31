using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [DisallowMultipleComponent]
    public class PlayerStatSystem : MonoBehaviour
    {
        [SerializeField]
        private CharacterData _characterData;

        [SerializeField]
        private PlayerStatsDefinition _initialStats;

        private readonly Dictionary<StatType, RuntimeStat> _runtimeStats = new Dictionary<StatType, RuntimeStat>();

        public event Action<StatType, float> OnStatChanged;
        public CharacterData CharacterData => _characterData;

        private void Awake()
        {
            InitializeFromDefinition();
        }

        public void InitializeFromDefinition()
        {
            _runtimeStats.Clear();

            PlayerStatsDefinition sourceDefinition = ResolveInitialStatsDefinition();
            if (sourceDefinition == null)
            {
                Debug.LogWarning("[PlayerStatSystem] Initial stat definition is missing.");
                return;
            }

            for (int i = 0; i < sourceDefinition.Stats.Count; i++)
            {
                StatValueDefinition definition = sourceDefinition.Stats[i];
                _runtimeStats[definition.StatType] = new RuntimeStat(definition.BaseValue);
            }
        }

        private PlayerStatsDefinition ResolveInitialStatsDefinition()
        {
            if (_characterData != null)
            {
                if (_characterData.InitialStats == null)
                {
                    Debug.LogWarning($"[PlayerStatSystem] CharacterData '{_characterData.name}' is missing InitialStats.");
                }
                else
                {
                    return _characterData.InitialStats;
                }
            }

            return _initialStats;
        }

        internal bool TryGetRuntimeStat(StatType statType, out RuntimeStat runtimeStat)
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
