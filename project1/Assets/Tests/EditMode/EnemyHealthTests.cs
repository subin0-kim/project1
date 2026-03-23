using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class EnemyHealthTests
    {
        [Test]
        public void ApplyDamage_DecreasesHealth_And_TriggersDeath()
        {
            var go = new GameObject("EnemyHealthTest");

            try
            {
                var enemyHealth = go.AddComponent<EnemyHealth>();
                enemyHealth.ResetHealth();
                float initialHealth = enemyHealth.CurrentHealth;
                bool didDie = false;
                enemyHealth.OnDied += () => didDie = true;

                enemyHealth.ApplyDamage(initialHealth + 1f);

                Assert.That(enemyHealth.IsAlive, Is.False);
                Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(0f));
                Assert.That(go.activeSelf, Is.False);
                Assert.That(didDie, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyDamage_Ignores_NonPositiveDamage()
        {
            var go = new GameObject("EnemyHealthTest");

            try
            {
                var enemyHealth = go.AddComponent<EnemyHealth>();
                enemyHealth.ResetHealth();
                float initialHealth = enemyHealth.CurrentHealth;

                enemyHealth.ApplyDamage(0f);
                enemyHealth.ApplyDamage(-5f);

                Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(initialHealth));
                Assert.That(enemyHealth.IsAlive, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
