using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    public class RuntimeStat
    {
        private readonly float _baseValue;
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private bool _isDirty = true;
        private float _cachedValue;

        public RuntimeStat(float baseValue)
        {
            _baseValue = baseValue;
        }

        public float BaseValue => _baseValue;
        public int ModifierCount => _modifiers.Count;
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public float Value
        {
            get
            {
                if (_isDirty)
                {
                    _cachedValue = CalculateValue();
                    _isDirty = false;
                }

                return _cachedValue;
            }
        }

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            MarkDirty();
        }

        public bool RemoveModifier(StatModifier modifier)
        {
            bool removed = _modifiers.Remove(modifier);
            if (removed)
            {
                MarkDirty();
            }

            return removed;
        }

        public int RemoveAllModifiersFromSource(object source)
        {
            if (source == null)
            {
                return 0;
            }

            int previousCount = _modifiers.Count;
            _modifiers.RemoveAll(modifier => Equals(modifier.Source, source));
            int removedCount = previousCount - _modifiers.Count;
            if (removedCount > 0)
            {
                MarkDirty();
            }

            return removedCount;
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

        private void MarkDirty()
        {
            _isDirty = true;
        }
    }
}
