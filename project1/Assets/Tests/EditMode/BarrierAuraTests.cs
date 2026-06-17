using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class BarrierTickDamageTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void Apply_DamagesEnemiesInRange_SkipsOutOfRange()
        {
            EnemyHealth near = CreateEnemy("Near", new Vector3(1f, 0f, 0f));   // 반경 2 안
            EnemyHealth edge = CreateEnemy("Edge", new Vector3(2f, 0f, 0f));   // 정확히 반경 = 포함
            EnemyHealth far = CreateEnemy("Far", new Vector3(5f, 0f, 0f));     // 반경 밖

            var enemies = new List<EnemyHealth> { near, edge, far };
            int hit = BarrierTickDamage.Apply(Vector2.zero, 2f, 5f, enemies, this);

            Assert.That(hit, Is.EqualTo(2));
            Assert.That(near.CurrentHealth, Is.EqualTo(95f).Within(1e-4f));
            Assert.That(edge.CurrentHealth, Is.EqualTo(95f).Within(1e-4f));
            Assert.That(far.CurrentHealth, Is.EqualTo(100f).Within(1e-4f)); // 영향 없음
        }

        [Test]
        public void Apply_SkipsNonTargetableEnemies()
        {
            EnemyHealth enemy = CreateEnemy("Untargetable", new Vector3(0.5f, 0f, 0f));
            enemy.IsTargetable = false;

            var enemies = new List<EnemyHealth> { enemy };
            int hit = BarrierTickDamage.Apply(Vector2.zero, 2f, 5f, enemies, this);

            Assert.That(hit, Is.EqualTo(0));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f).Within(1e-4f));
        }

        [Test]
        public void Apply_ZeroRadiusOrDamage_DoesNothing()
        {
            EnemyHealth enemy = CreateEnemy("In", new Vector3(0.5f, 0f, 0f));
            var enemies = new List<EnemyHealth> { enemy };

            Assert.That(BarrierTickDamage.Apply(Vector2.zero, 0f, 5f, enemies, this), Is.EqualTo(0));
            Assert.That(BarrierTickDamage.Apply(Vector2.zero, 2f, 0f, enemies, this), Is.EqualTo(0));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f).Within(1e-4f));
        }

        private EnemyHealth CreateEnemy(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            go.transform.position = position;

            var eh = go.AddComponent<EnemyHealth>();
            eh.SetSwipeDirection(SwipeDirection.Up);
            eh.SetMaxHealth(100f);
            eh.ResetHealth();
            return eh;
        }
    }

    public class BarrierAuraSkillTests
    {
        private GameObject _go;
        private BarrierAuraSkill _skill;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BarrierAuraSkill");
            _skill = _go.AddComponent<BarrierAuraSkill>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void NotOwned_HasNoRadiusOrDamage()
        {
            Assert.That(_skill.Level, Is.EqualTo(0));
            Assert.That(_skill.CurrentRadius, Is.EqualTo(0f));
            Assert.That(_skill.CurrentTickDamage, Is.EqualTo(0f));
        }

        [Test]
        public void Level1_UsesBaseRadiusAndBaseStats()
        {
            _skill.ApplyLevel(1);
            Assert.That(_skill.CurrentRadius, Is.EqualTo(3.0f).Within(1e-4f));   // base 3 * 1.0
            Assert.That(_skill.CurrentTickDamage, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(_skill.CurrentTickInterval, Is.EqualTo(1.0f).Within(1e-4f));
        }

        [Test]
        public void Level2And3_ScaleRadiusDamageInterval()
        {
            _skill.ApplyLevel(2);
            Assert.That(_skill.CurrentRadius, Is.EqualTo(3.6f).Within(1e-4f));   // 3 * 1.2
            Assert.That(_skill.CurrentTickDamage, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(_skill.CurrentTickInterval, Is.EqualTo(0.9f).Within(1e-4f));

            _skill.ApplyLevel(3);
            Assert.That(_skill.CurrentRadius, Is.EqualTo(4.2f).Within(1e-4f));   // 3 * 1.4
            Assert.That(_skill.CurrentTickDamage, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(_skill.CurrentTickInterval, Is.EqualTo(0.8f).Within(1e-4f));
        }

        [Test]
        public void ApplyLevel_ClampsToMaxLevel()
        {
            _skill.ApplyLevel(99);
            Assert.That(_skill.Level, Is.EqualTo(BarrierAuraSkill.MaxLevel));

            _skill.ApplyLevel(-3);
            Assert.That(_skill.Level, Is.EqualTo(0));
        }
    }
}
