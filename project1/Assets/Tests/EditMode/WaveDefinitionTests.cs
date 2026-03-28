using System.Collections.Generic;
using System.Reflection;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    public class WaveDefinitionTests
    {
        [Test]
        public void GetTotalSpawnCount_SumsOnlyValidCounts()
        {
            var wave = new WaveDefinition();
            var entryA = new WaveEnemySpawnEntry();
            var entryB = new WaveEnemySpawnEntry();

            SetPrivateField(entryA, "_count", 3);
            SetPrivateField(entryB, "_count", -7);

            SetPrivateField(
                wave,
                "_enemies",
                new List<WaveEnemySpawnEntry>
                {
                    entryA,
                    null,
                    entryB
                });

            Assert.That(wave.GetTotalSpawnCount(), Is.EqualTo(3));
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field not found: {fieldName}");
            fieldInfo.SetValue(target, value);
        }
    }
}
