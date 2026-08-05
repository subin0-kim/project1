using System.Collections.Generic;
using System.Reflection;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class WaveDefinitionTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

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

        // 종류 판별 키와 표시 이름은 같은 우선순위(MonsterData → EnemyPrefab → EnemyType)로 결정되어야 한다.
        // 둘이 어긋나면 '처음 보는 종류'로 판정한 적과 배너에 뜨는 이름이 서로 다른 적을 가리키게 된다(#70).
        [Test]
        public void SpeciesKeyAndDisplayName_PreferMonsterData()
        {
            var entry = new WaveEnemySpawnEntry();
            MonsterData species = CreateMonsterData("Sonakssi_MonsterData", "손각시");
            EnemyHealth prefab = CreatePrefab("Sonakssi_Prefab");

            SetPrivateField(entry, "_monsterData", species);
            SetPrivateField(entry, "_enemyPrefab", prefab);
            SetPrivateField(entry, "_enemyType", "Sonakssi");

            Assert.That(entry.SpeciesKey, Is.SameAs(species));
            Assert.That(entry.DisplayName, Is.EqualTo("손각시"), "MonsterData가 있으면 그 표시 이름을 쓴다.");
        }

        [Test]
        public void SpeciesKeyAndDisplayName_FallBackToPrefab_WhenMonsterDataMissing()
        {
            var entry = new WaveEnemySpawnEntry();
            EnemyHealth prefab = CreatePrefab("Dokkaebibul");

            SetPrivateField(entry, "_enemyPrefab", prefab);
            SetPrivateField(entry, "_enemyType", "Dokkaebi");

            // 인스턴스가 아니라 프리팹이 키다 — 풀에서 재사용된 개체가 이전 종류의 MonsterData를
            // 들고 있어도 이 엔트리로 스폰된 적은 항상 같은 종류로 묶인다.
            Assert.That(entry.SpeciesKey, Is.SameAs(prefab));
            Assert.That(entry.DisplayName, Is.EqualTo("Dokkaebibul"));
        }

        [Test]
        public void SpeciesKeyAndDisplayName_FallBackToEnemyType_WhenPrefabMissing()
        {
            var entry = new WaveEnemySpawnEntry();
            SetPrivateField(entry, "_enemyType", "Mangryang");

            Assert.That(entry.SpeciesKey, Is.EqualTo("Mangryang"));
            Assert.That(entry.DisplayName, Is.EqualTo("Mangryang"));
        }

        [Test]
        public void SpeciesKey_DiffersPerPrefab_WhenMonsterDataMissing()
        {
            var first = new WaveEnemySpawnEntry();
            var second = new WaveEnemySpawnEntry();
            SetPrivateField(first, "_enemyPrefab", CreatePrefab("Dokkaebibul"));
            SetPrivateField(second, "_enemyPrefab", CreatePrefab("Mokgwi"));

            Assert.That(first.SpeciesKey, Is.Not.EqualTo(second.SpeciesKey), "프리팹이 다르면 다른 종류다.");
        }

        private MonsterData CreateMonsterData(string assetName, string displayName)
        {
            MonsterData species = ScriptableObject.CreateInstance<MonsterData>();
            species.name = assetName;
            SetPrivateField(species, "_displayName", displayName);
            _created.Add(species);
            return species;
        }

        private EnemyHealth CreatePrefab(string objectName)
        {
            var go = new GameObject(objectName);
            _created.Add(go);
            return go.AddComponent<EnemyHealth>();
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field not found: {fieldName}");
            fieldInfo.SetValue(target, value);
        }
    }
}
