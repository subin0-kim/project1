using System.Reflection;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class PlayerHealthTests
    {
        private GameObject _go;
        private PlayerHealth _playerHealth;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PlayerHealthTest");
            _go.AddComponent<PlayerStatSystem>();
            _playerHealth = _go.AddComponent<PlayerHealth>();

            // Ensure fallback max health is set (serialized defaults may not apply in EditMode tests)
            SetPrivateField(_playerHealth, "_fallbackMaxHealth", 100f);

            // Re-trigger initialization
            _playerHealth.ResetHealth();
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
        public void TakeDamage_DecreasesCurrentHealth()
        {
            float maxHealth = _playerHealth.MaxHealth;
            Assert.That(maxHealth, Is.GreaterThan(0f));

            _playerHealth.TakeDamage(10f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(maxHealth - 10f).Within(0.01f));
            Assert.That(_playerHealth.IsAlive, Is.True);
        }

        [Test]
        public void TakeDamage_IgnoresZeroAndNegative()
        {
            float before = _playerHealth.CurrentHealth;

            _playerHealth.TakeDamage(0f);
            _playerHealth.TakeDamage(-5f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(before));
        }

        [Test]
        public void TakeDamage_TriggersOnDied_WhenHealthReachesZero()
        {
            bool didDie = false;
            _playerHealth.OnDied += () => didDie = true;

            _playerHealth.TakeDamage(_playerHealth.MaxHealth + 100f);

            Assert.That(_playerHealth.IsAlive, Is.False);
            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(0f));
            Assert.That(didDie, Is.True);
        }

        [Test]
        public void TakeDamage_BlockedWhileInvincible()
        {
            float before = _playerHealth.CurrentHealth;
            _playerHealth.SetInvincible(true);

            _playerHealth.TakeDamage(50f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(before));
            Assert.That(_playerHealth.IsAlive, Is.True);
        }

        [Test]
        public void TakeDamage_IgnoredAfterDeath()
        {
            _playerHealth.TakeDamage(_playerHealth.MaxHealth + 1f);
            Assert.That(_playerHealth.IsAlive, Is.False);

            int diedCount = 0;
            _playerHealth.OnDied += () => diedCount++;

            _playerHealth.TakeDamage(10f);

            Assert.That(diedCount, Is.EqualTo(0));
        }

        [Test]
        public void Heal_IncreasesHealthUpToMax()
        {
            _playerHealth.TakeDamage(30f);
            float afterDamage = _playerHealth.CurrentHealth;

            _playerHealth.Heal(15f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(afterDamage + 15f).Within(0.01f));
        }

        [Test]
        public void Heal_DoesNotExceedMaxHealth()
        {
            _playerHealth.TakeDamage(10f);
            _playerHealth.Heal(9999f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(_playerHealth.MaxHealth).Within(0.01f));
        }

        [Test]
        public void Heal_IgnoredAfterDeath()
        {
            _playerHealth.TakeDamage(_playerHealth.MaxHealth + 1f);
            Assert.That(_playerHealth.IsAlive, Is.False);

            _playerHealth.Heal(50f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(0f));
        }

        [Test]
        public void ResetHealth_RestoresFullHealthAndAliveState()
        {
            _playerHealth.TakeDamage(_playerHealth.MaxHealth + 1f);
            Assert.That(_playerHealth.IsAlive, Is.False);

            _playerHealth.ResetHealth();

            Assert.That(_playerHealth.IsAlive, Is.True);
            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(_playerHealth.MaxHealth).Within(0.01f));
        }

        [Test]
        public void HealthNormalized_ReturnsCorrectRatio()
        {
            float max = _playerHealth.MaxHealth;

            Assert.That(_playerHealth.HealthNormalized, Is.EqualTo(1f).Within(0.01f));

            _playerHealth.TakeDamage(max * 0.5f);
            Assert.That(_playerHealth.HealthNormalized, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void OnDamaged_FiresWithCorrectAmount()
        {
            float reported = 0f;
            _playerHealth.OnDamaged += amount => reported = amount;

            _playerHealth.TakeDamage(25f);

            Assert.That(reported, Is.EqualTo(25f).Within(0.01f));
        }

        [Test]
        public void OnHealed_FiresWithCorrectAmount()
        {
            _playerHealth.TakeDamage(40f);

            float reported = 0f;
            _playerHealth.OnHealed += amount => reported = amount;

            _playerHealth.Heal(20f);

            Assert.That(reported, Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void OnHealthChanged_FiresOnDamageAndHeal()
        {
            int callCount = 0;
            _playerHealth.OnHealthChanged += (current, max) => callCount++;

            _playerHealth.TakeDamage(10f);
            _playerHealth.Heal(5f);

            Assert.That(callCount, Is.EqualTo(2));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
