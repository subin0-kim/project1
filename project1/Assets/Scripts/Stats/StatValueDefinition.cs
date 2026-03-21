using System;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [Serializable]
    public struct StatValueDefinition
    {
        [SerializeField]
        private StatType _statType;

        [SerializeField]
        private float _baseValue;

        public StatValueDefinition(StatType statType, float baseValue)
        {
            _statType = statType;
            _baseValue = baseValue;
        }

        public StatType StatType => _statType;
        public float BaseValue => _baseValue;
    }
}
