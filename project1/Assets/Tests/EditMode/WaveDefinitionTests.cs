using System.Collections.Generic;
using System.Reflection;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    public class WaveDefinitionTests
    {
        [Test]
        public void GetTotalMinAliveCount_SumsOnlyValidEntriesAndClampsNegatives()
        {
            var wave = new WaveDefinition();
            var entryA = new WaveEnemySpawnEntry();
            var entryB = new WaveEnemySpawnEntry();

            SetPrivateField(entryA, "_minAliveCount", 3);
            SetPrivateField(entryB, "_minAliveCount", -7);

            SetPrivateField(
                wave,
                "_enemies",
                new List<WaveEnemySpawnEntry>
                {
                    entryA,
                    null,
                    entryB
                });

            // 음수는 0으로 클램프되고 null 항목은 무시되므로 3만 합산된다.
            Assert.That(wave.GetTotalMinAliveCount(), Is.EqualTo(3));
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field not found: {fieldName}");
            fieldInfo.SetValue(target, value);
        }
    }
}
