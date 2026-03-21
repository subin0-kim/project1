using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    public class RuntimeStat
    {
        private readonly float _baseValue;
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        public RuntimeStat(float baseValue)
        {
            _baseValue = baseValue;
        }

        public float BaseValue => _baseValue;
        public int ModifierCount => _modifiers.Count;
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public float Value => CalculateValue();

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
        }

        public bool RemoveModifier(StatModifier modifier)
        {
            return _modifiers.Remove(modifier);
        }

        public int RemoveAllModifiersFromSource(object source)
        {
            if (source == null)
            {
                return 0;
            }

            int previousCount = _modifiers.Count;
            _modifiers.RemoveAll(modifier => Equals(modifier.Source, source));
            return previousCount - _modifiers.Count;
        }

        private float CalculateValue()
        {
            float valueWithFlat = _baseValue;
            float percentSum = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier modifier = _modifiers[i];
                switch (modifier.Type)
                {
                    case StatModifierType.Flat:
                        valueWithFlat += modifier.Value;
                        break;
                    case StatModifierType.Percent:
                        percentSum += modifier.Value;
                        break;
                }
            }

            float finalValue = valueWithFlat * (1f + percentSum);
            return Mathf.Max(0f, finalValue);
        }
    }
}
