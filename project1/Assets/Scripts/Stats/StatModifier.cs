using System;

namespace Mukseon.Gameplay.Stats
{
    [Serializable]
    public readonly struct StatModifier
    {
        public StatModifier(float value, StatModifierType type, object source = null)
        {
            Value = value;
            Type = type;
            Source = source;
        }

        public float Value { get; }
        public StatModifierType Type { get; }
        public object Source { get; }
    }
}
