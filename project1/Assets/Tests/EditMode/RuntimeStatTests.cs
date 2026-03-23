using Mukseon.Gameplay.Stats;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    public class RuntimeStatTests
    {
        [Test]
        public void Value_Includes_Flat_And_Percent_Modifiers()
        {
            var runtimeStat = new RuntimeStat(100f);
            runtimeStat.AddModifier(new StatModifier(20f, StatModifierType.Flat));
            runtimeStat.AddModifier(new StatModifier(0.5f, StatModifierType.Percent));

            Assert.That(runtimeStat.Value, Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void RemoveAllModifiersFromSource_Removes_Only_Matching_Source()
        {
            var sourceA = new object();
            var sourceB = new object();
            var runtimeStat = new RuntimeStat(100f);

            runtimeStat.AddModifier(new StatModifier(10f, StatModifierType.Flat, sourceA));
            runtimeStat.AddModifier(new StatModifier(0.25f, StatModifierType.Percent, sourceA));
            runtimeStat.AddModifier(new StatModifier(5f, StatModifierType.Flat, sourceB));

            int removedCount = runtimeStat.RemoveAllModifiersFromSource(sourceA);

            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(runtimeStat.ModifierCount, Is.EqualTo(1));
            Assert.That(runtimeStat.Value, Is.EqualTo(105f).Within(0.001f));
        }

        [Test]
        public void Value_Is_Clamped_To_Zero_When_Result_Is_Negative()
        {
            var runtimeStat = new RuntimeStat(50f);
            runtimeStat.AddModifier(new StatModifier(-100f, StatModifierType.Flat));

            Assert.That(runtimeStat.Value, Is.EqualTo(0f));
        }
    }
}
