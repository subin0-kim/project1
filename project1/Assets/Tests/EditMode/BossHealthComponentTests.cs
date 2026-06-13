using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class BossHealthComponentTests
    {
        [Test]
        public void ComputePhaseIndex_DescendingThresholds_ReturnsCrossedCount()
        {
            var thresholds = new List<float> { 0.66f, 0.33f };

            Assert.That(BossHealthComponent.ComputePhaseIndex(1.0f, thresholds), Is.EqualTo(0));
            Assert.That(BossHealthComponent.ComputePhaseIndex(0.66f, thresholds), Is.EqualTo(1));
            Assert.That(BossHealthComponent.ComputePhaseIndex(0.5f, thresholds), Is.EqualTo(1));
            Assert.That(BossHealthComponent.ComputePhaseIndex(0.33f, thresholds), Is.EqualTo(2));
            Assert.That(BossHealthComponent.ComputePhaseIndex(0.1f, thresholds), Is.EqualTo(2));
        }

        [Test]
        public void ComputePhaseIndex_NoThresholds_AlwaysZero()
        {
            Assert.That(BossHealthComponent.ComputePhaseIndex(1f, null), Is.EqualTo(0));
            Assert.That(BossHealthComponent.ComputePhaseIndex(0.1f, new List<float>()), Is.EqualTo(0));
        }

        [Test]
        public void Initialize_OverwritesEnemyHealthMaxHealth_WithBossTotalHealth()
        {
            var go = new GameObject("Boss");
            BossData data = null;

            try
            {
                var enemyHealth = go.AddComponent<EnemyHealth>();
                var bossHealth = go.AddComponent<BossHealthComponent>();

                data = ScriptableObject.CreateInstance<BossData>();
                data.ConfigureForTests(1234f, 0.5f);
                bossHealth.SetBossDataForTests(data);

                bossHealth.Initialize();

                Assert.That(enemyHealth.MaxHealth, Is.EqualTo(1234f));
                Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(1234f));
                Assert.That(bossHealth.CurrentPhaseIndex, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (data != null)
                {
                    Object.DestroyImmediate(data);
                }
            }
        }

        [Test]
        public void Damage_CrossingThreshold_FiresPhaseThresholdReached_Once()
        {
            var go = new GameObject("Boss");
            BossData data = null;

            try
            {
                var enemyHealth = go.AddComponent<EnemyHealth>();
                var bossHealth = go.AddComponent<BossHealthComponent>();

                data = ScriptableObject.CreateInstance<BossData>();
                data.ConfigureForTests(100f, 0.5f);
                bossHealth.SetBossDataForTests(data);
                bossHealth.Initialize();

                int firedPhase = -1;
                int fireCount = 0;
                bossHealth.OnPhaseThresholdReached += phase =>
                {
                    firedPhase = phase;
                    fireCount++;
                };

                enemyHealth.ApplyDamage(40f); // 60% — 임계값 미도달
                Assert.That(fireCount, Is.EqualTo(0));

                enemyHealth.ApplyDamage(20f); // 40% — 50% 교차
                Assert.That(firedPhase, Is.EqualTo(1));
                Assert.That(fireCount, Is.EqualTo(1));
                Assert.That(bossHealth.CurrentPhaseIndex, Is.EqualTo(1));

                enemyHealth.ApplyDamage(10f); // 30% — 같은 페이즈, 재발행 없음
                Assert.That(fireCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (data != null)
                {
                    Object.DestroyImmediate(data);
                }
            }
        }

        [Test]
        public void SetInvincible_TogglesEnemyHealthTargetable()
        {
            var go = new GameObject("Boss");

            try
            {
                var enemyHealth = go.AddComponent<EnemyHealth>();
                var bossHealth = go.AddComponent<BossHealthComponent>();

                bossHealth.SetInvincible(true);
                Assert.That(enemyHealth.IsTargetable, Is.False);
                Assert.That(bossHealth.IsInvincible, Is.True);

                bossHealth.SetInvincible(false);
                Assert.That(enemyHealth.IsTargetable, Is.True);
                Assert.That(bossHealth.IsInvincible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Death_FiresOnDefeated()
        {
            var go = new GameObject("Boss");
            BossData data = null;

            try
            {
                var enemyHealth = go.AddComponent<EnemyHealth>();
                var bossHealth = go.AddComponent<BossHealthComponent>();

                data = ScriptableObject.CreateInstance<BossData>();
                data.ConfigureForTests(50f, 0.5f);
                bossHealth.SetBossDataForTests(data);
                bossHealth.Initialize();

                bool defeated = false;
                bossHealth.OnDefeated += _ => defeated = true;

                enemyHealth.ApplyDamage(1000f);

                Assert.That(enemyHealth.IsAlive, Is.False);
                Assert.That(defeated, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (data != null)
                {
                    Object.DestroyImmediate(data);
                }
            }
        }
    }
}
