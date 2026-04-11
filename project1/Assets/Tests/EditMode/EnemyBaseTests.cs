using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;
using Mukseon.Core.Input;

namespace Mukseon.Tests.EditMode
{
    public class EnemyBaseTests
    {
        private class TestEnemy : EnemyBase
        {
            protected override void UpdateMovement() { }
            protected override void OnTriggerAction() { }
        }

        private GameObject _go;
        private TestEnemy _enemy;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestEnemy");
            _go.AddComponent<Rigidbody2D>();
            _enemy = _go.AddComponent<TestEnemy>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void TakeDamage_DecreasesHealth_And_TriggersDeath()
        {
            bool didDie = false;
            _enemy.OnDeath += _ => didDie = true;

            _enemy.TakeDamage(_enemy.IsAlive ? 9999f : 0f);

            Assert.That(_enemy.IsAlive, Is.False);
            Assert.That(_go.activeSelf, Is.False);
            Assert.That(didDie, Is.True);
        }

        [Test]
        public void TakeDamage_Ignores_NonPositiveDamage()
        {
            _enemy.TakeDamage(0f);
            _enemy.TakeDamage(-5f);

            Assert.That(_enemy.IsAlive, Is.True);
        }

        [Test]
        public void Kill_WithCountAsKill_False_DoesNotFireAnyEnemyDied()
        {
            bool anyDied = false;
            EnemyBase.AnyEnemyDied += _ => anyDied = true;

            try
            {
                _enemy.Kill(countAsKill: false);
                Assert.That(anyDied, Is.False);
                Assert.That(_enemy.IsAlive, Is.False);
            }
            finally
            {
                EnemyBase.AnyEnemyDied -= _ => anyDied = true;
            }
        }

        [Test]
        public void SwipeDirection_ReturnsNone_WhenDataIsNull()
        {
            Assert.That(_enemy.SwipeDirection, Is.EqualTo(SwipeDirection.None));
        }
    }
}
