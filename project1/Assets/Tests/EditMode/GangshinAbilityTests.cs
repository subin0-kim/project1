using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>강신 필살기 효과(#30) — 데이터/순수 로직/Ability 동작 검증.</summary>
    public class GangshinAbilityTests
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

        // ----- GangshinAbilityData -----

        [Test]
        public void GetLevel_ClampsToTableRange()
        {
            GangshinAbilityData data = ScriptableObject.CreateInstance<GangshinAbilityData>();
            try
            {
                data.ConfigureForTests(
                    new GangshinAbilityLevel(500f, 100f, 0f, false),
                    new GangshinAbilityLevel(700f, 100f, 0f, false),
                    new GangshinAbilityLevel(1000f, 90f, 1.5f, true));

                Assert.That(data.MaxLevel, Is.EqualTo(3));
                Assert.That(data.GetLevel(1).Damage, Is.EqualTo(500f));
                Assert.That(data.GetLevel(3).Damage, Is.EqualTo(1000f));
                Assert.That(data.GetLevel(3).RequiredGaugeNormalized, Is.EqualTo(0.9f).Within(1e-4f));

                // 범위 밖은 최소/최대로 클램프.
                Assert.That(data.GetLevel(0).Damage, Is.EqualTo(500f));
                Assert.That(data.GetLevel(99).Damage, Is.EqualTo(1000f));
                Assert.That(data.GetLevel(99).DoubleWave, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // ----- GangshinAbilityEffects.ApplyToAll -----

        [Test]
        public void ApplyToAll_DamagesEveryAliveTargetableEnemy_RegardlessOfDistance()
        {
            EnemyHealth near = CreateEnemy("Near", new Vector3(0.5f, 0f, 0f), 1000f);
            EnemyHealth far = CreateEnemy("Far", new Vector3(50f, 0f, 0f), 1000f);

            var enemies = new List<EnemyHealth> { near, far };
            int hit = GangshinAbilityEffects.ApplyToAll(enemies, 500f, 0f, this);

            Assert.That(hit, Is.EqualTo(2));
            Assert.That(near.CurrentHealth, Is.EqualTo(500f).Within(1e-4f));
            Assert.That(far.CurrentHealth, Is.EqualTo(500f).Within(1e-4f)); // 거리 무관 — 화면 전체
        }

        [Test]
        public void ApplyToAll_SkipsDeadAndUntargetableEnemies()
        {
            EnemyHealth dead = CreateEnemy("Dead", Vector3.zero, 100f);
            dead.ApplyDamage(100f, this);
            EnemyHealth untargetable = CreateEnemy("Untargetable", Vector3.zero, 100f);
            untargetable.IsTargetable = false;

            var enemies = new List<EnemyHealth> { dead, untargetable };
            int hit = GangshinAbilityEffects.ApplyToAll(enemies, 500f, 0f, this);

            Assert.That(hit, Is.EqualTo(0));
            Assert.That(untargetable.CurrentHealth, Is.EqualTo(100f).Within(1e-4f));
        }

        [Test]
        public void ApplyToAll_AppliesStun_ToSurvivingEnemiesWithMover()
        {
            EnemyHealth survivor = CreateEnemy("Survivor", Vector3.zero, 1000f);
            EnemyMover mover = survivor.gameObject.AddComponent<EnemyMover>();

            GangshinAbilityEffects.ApplyToAll(new List<EnemyHealth> { survivor }, 100f, 2f, this);

            Assert.That(mover.IsStunned, Is.True);
        }

        // ----- GangshinAbilityEffects.ApplyExpandingRing -----

        [Test]
        public void ApplyExpandingRing_HitsOnlyEnemiesWithinRadius_AndNotAlreadyHit()
        {
            EnemyHealth inside = CreateEnemy("Inside", new Vector3(2f, 0f, 0f), 1000f);
            EnemyHealth outside = CreateEnemy("Outside", new Vector3(8f, 0f, 0f), 1000f);
            var enemies = new List<EnemyHealth> { inside, outside };
            var alreadyHit = new HashSet<EnemyHealth>();

            // 반경 3: inside만 파면 안.
            int firstStep = GangshinAbilityEffects.ApplyExpandingRing(
                Vector2.zero, 3f, 300f, 0f, enemies, alreadyHit, this);
            Assert.That(firstStep, Is.EqualTo(1));
            Assert.That(inside.CurrentHealth, Is.EqualTo(700f).Within(1e-4f));
            Assert.That(outside.CurrentHealth, Is.EqualTo(1000f).Within(1e-4f));

            // 반경 10: outside 새로 진입, inside는 이미 맞았으므로 중복 타격 없음.
            int secondStep = GangshinAbilityEffects.ApplyExpandingRing(
                Vector2.zero, 10f, 300f, 0f, enemies, alreadyHit, this);
            Assert.That(secondStep, Is.EqualTo(1));
            Assert.That(inside.CurrentHealth, Is.EqualTo(700f).Within(1e-4f)); // 변화 없음
            Assert.That(outside.CurrentHealth, Is.EqualTo(700f).Within(1e-4f));
        }

        // ----- GangshinAbilityMudang (살풀이 검무) -----

        [Test]
        public void Mudang_Activate_DamagesAllEnemies()
        {
            var go = new GameObject("Mudang");
            go.transform.SetParent(_root.transform);
            var ability = go.AddComponent<GangshinAbilityMudang>();
            GangshinAbilityData data = ScriptableObject.CreateInstance<GangshinAbilityData>();

            try
            {
                data.ConfigureForTests(new GangshinAbilityLevel(500f, 100f, 0f, false));
                ability.SetDataForTests(data);

                EnemyHealth a = CreateEnemy("A", Vector3.zero, 1000f);
                EnemyHealth b = CreateEnemy("B", new Vector3(30f, 0f, 0f), 1000f);
                var enemies = new List<EnemyHealth> { a, b };

                ability.Activate(new GangshinSlotContext(Vector2.zero, 1, this, enemies));

                Assert.That(a.CurrentHealth, Is.EqualTo(500f).Within(1e-4f));
                Assert.That(b.CurrentHealth, Is.EqualTo(500f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Mudang_Activate_Level3_StunsSurvivingEnemies()
        {
            var go = new GameObject("Mudang");
            go.transform.SetParent(_root.transform);
            var ability = go.AddComponent<GangshinAbilityMudang>();
            GangshinAbilityData data = ScriptableObject.CreateInstance<GangshinAbilityData>();

            try
            {
                data.ConfigureForTests(
                    new GangshinAbilityLevel(500f, 100f, 0f, false),
                    new GangshinAbilityLevel(700f, 100f, 0f, false),
                    new GangshinAbilityLevel(1000f, 90f, 1.5f, false)); // Lv3 기절
                ability.SetDataForTests(data);

                EnemyHealth enemy = CreateEnemy("Tank", Vector3.zero, 5000f);
                EnemyMover mover = enemy.gameObject.AddComponent<EnemyMover>();

                ability.Activate(new GangshinSlotContext(Vector2.zero, 3, this, new List<EnemyHealth> { enemy }));

                Assert.That(enemy.CurrentHealth, Is.EqualTo(4000f).Within(1e-4f));
                Assert.That(mover.IsStunned, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // ----- GangshinAbilityBaksoo (파천의 징) -----

        [Test]
        public void Baksoo_Activate_WaveExpands_AndDamagesEnemy()
        {
            var go = new GameObject("Baksoo");
            go.transform.SetParent(_root.transform);
            var ability = go.AddComponent<GangshinAbilityBaksoo>();
            GangshinAbilityData data = ScriptableObject.CreateInstance<GangshinAbilityData>();

            try
            {
                data.ConfigureForTests(new GangshinAbilityLevel(300f, 100f, 1.5f, false));
                ability.SetDataForTests(data);

                EnemyHealth enemy = CreateEnemy("E", new Vector3(1f, 0f, 0f), 1000f);
                EnemyMover mover = enemy.gameObject.AddComponent<EnemyMover>();

                ability.Activate(new GangshinSlotContext(Vector2.zero, 1, this, new List<EnemyHealth> { enemy }));
                Assert.That(ability.IsWaveActive, Is.True);

                // 파동이 최대 반경(기본 12)까지 확장되도록 충분히 Tick.
                for (int i = 0; i < 20; i++)
                {
                    ability.Tick(0.05f);
                }

                Assert.That(enemy.CurrentHealth, Is.EqualTo(700f).Within(1e-4f));
                Assert.That(mover.IsStunned, Is.True);
                Assert.That(ability.IsWaveActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Baksoo_Level3_DoubleWave_HitsEnemyTwice()
        {
            var go = new GameObject("Baksoo");
            go.transform.SetParent(_root.transform);
            var ability = go.AddComponent<GangshinAbilityBaksoo>();
            GangshinAbilityData data = ScriptableObject.CreateInstance<GangshinAbilityData>();

            try
            {
                data.ConfigureForTests(new GangshinAbilityLevel(600f, 90f, 2.5f, true)); // DoubleWave
                ability.SetDataForTests(data);

                EnemyHealth enemy = CreateEnemy("E", new Vector3(1f, 0f, 0f), 5000f);

                ability.Activate(new GangshinSlotContext(Vector2.zero, 1, this, new List<EnemyHealth> { enemy }));

                // 넉넉히 Tick하여 1파 + 딜레이 + 2파를 모두 소화.
                for (int i = 0; i < 60; i++)
                {
                    ability.Tick(0.05f);
                }

                // 파동 2회 적중 → 600 * 2 = 1200 피해.
                Assert.That(enemy.CurrentHealth, Is.EqualTo(3800f).Within(1e-4f));
                Assert.That(ability.IsWaveActive, Is.False);
                Assert.That(ability.WavesRemaining, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        private EnemyHealth CreateEnemy(string name, Vector3 position, float maxHealth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            go.transform.position = position;

            var eh = go.AddComponent<EnemyHealth>();
            eh.SetSwipeDirection(SwipeDirection.Up);
            eh.SetMaxHealth(maxHealth);
            eh.ResetHealth();
            return eh;
        }
    }
}
