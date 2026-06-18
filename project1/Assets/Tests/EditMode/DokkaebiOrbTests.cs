using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class RadialDamageTests
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
        public void ApplyInRadius_DamagesEnemiesInRange_SkipsOutOfRange()
        {
            EnemyHealth near = CreateEnemy("Near", new Vector3(1f, 0f, 0f));   // 반경 2 안
            EnemyHealth edge = CreateEnemy("Edge", new Vector3(2f, 0f, 0f));   // 정확히 반경 = 포함
            EnemyHealth far = CreateEnemy("Far", new Vector3(5f, 0f, 0f));     // 반경 밖

            var enemies = new List<EnemyHealth> { near, edge, far };
            int hit = RadialDamage.ApplyInRadius(Vector2.zero, 2f, 5f, enemies, this);

            Assert.That(hit, Is.EqualTo(2));
            Assert.That(near.CurrentHealth, Is.EqualTo(95f).Within(1e-4f));
            Assert.That(edge.CurrentHealth, Is.EqualTo(95f).Within(1e-4f));
            Assert.That(far.CurrentHealth, Is.EqualTo(100f).Within(1e-4f)); // 영향 없음
        }

        [Test]
        public void ApplyInRadius_SkipsNonTargetableAndDead()
        {
            EnemyHealth untargetable = CreateEnemy("Untargetable", new Vector3(0.5f, 0f, 0f));
            untargetable.IsTargetable = false;

            EnemyHealth dead = CreateEnemy("Dead", new Vector3(0.5f, 0f, 0f));
            dead.ApplyDamage(100f, this);
            Assert.That(dead.IsAlive, Is.False);

            var enemies = new List<EnemyHealth> { untargetable, dead };
            int hit = RadialDamage.ApplyInRadius(Vector2.zero, 2f, 5f, enemies, this);

            Assert.That(hit, Is.EqualTo(0));
            Assert.That(untargetable.CurrentHealth, Is.EqualTo(100f).Within(1e-4f));
        }

        [Test]
        public void ApplyInRadius_ZeroRadiusOrDamage_DoesNothing()
        {
            EnemyHealth enemy = CreateEnemy("In", new Vector3(0.5f, 0f, 0f));
            var enemies = new List<EnemyHealth> { enemy };

            Assert.That(RadialDamage.ApplyInRadius(Vector2.zero, 0f, 5f, enemies, this), Is.EqualTo(0));
            Assert.That(RadialDamage.ApplyInRadius(Vector2.zero, 2f, 0f, enemies, this), Is.EqualTo(0));
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

    public class DokkaebiOrbTargetingTests
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
        public void FindNearestTarget_ReturnsClosestInRange()
        {
            EnemyHealth far = CreateEnemy("Far", new Vector3(3f, 0f, 0f));
            EnemyHealth near = CreateEnemy("Near", new Vector3(1f, 0f, 0f));
            EnemyHealth mid = CreateEnemy("Mid", new Vector3(2f, 0f, 0f));

            var enemies = new List<EnemyHealth> { far, near, mid };
            EnemyHealth result = DokkaebiOrbTargeting.FindNearestTarget(Vector2.zero, 5f, enemies);

            Assert.That(result, Is.SameAs(near));
        }

        [Test]
        public void FindNearestTarget_IgnoresOutOfRange()
        {
            EnemyHealth outside = CreateEnemy("Outside", new Vector3(10f, 0f, 0f));
            var enemies = new List<EnemyHealth> { outside };

            Assert.That(DokkaebiOrbTargeting.FindNearestTarget(Vector2.zero, 4f, enemies), Is.Null);
        }

        [Test]
        public void FindNearestTarget_SkipsDeadAndUntargetable_PicksNextValid()
        {
            EnemyHealth dead = CreateEnemy("Dead", new Vector3(0.5f, 0f, 0f));
            dead.ApplyDamage(100f, this);

            EnemyHealth untargetable = CreateEnemy("Untargetable", new Vector3(0.8f, 0f, 0f));
            untargetable.IsTargetable = false;

            EnemyHealth valid = CreateEnemy("Valid", new Vector3(1.5f, 0f, 0f));

            var enemies = new List<EnemyHealth> { dead, untargetable, valid };
            EnemyHealth result = DokkaebiOrbTargeting.FindNearestTarget(Vector2.zero, 5f, enemies);

            Assert.That(result, Is.SameAs(valid));
        }

        [Test]
        public void FindNearestTarget_NullOrEmpty_ReturnsNull()
        {
            Assert.That(DokkaebiOrbTargeting.FindNearestTarget(Vector2.zero, 5f, null), Is.Null);
            Assert.That(DokkaebiOrbTargeting.FindNearestTarget(Vector2.zero, 5f, new List<EnemyHealth>()), Is.Null);
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

    public class DokkaebiOrbSkillTests
    {
        private GameObject _go;
        private DokkaebiOrbSkill _skill;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DokkaebiOrbSkill");
            _skill = _go.AddComponent<DokkaebiOrbSkill>();
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
        public void NotOwned_HasNoDronesOrStats()
        {
            Assert.That(_skill.Level, Is.EqualTo(0));
            Assert.That(_skill.CurrentDroneCount, Is.EqualTo(0));
            Assert.That(_skill.CurrentDetectRange, Is.EqualTo(0f));
            Assert.That(_skill.CurrentExplosionDamage, Is.EqualTo(0f));
        }

        // skill_balance_mvp.md §1: 드론 수 / 탐지 배수 / 폭발 데미지 / 재소환 쿨타임 (기본 탐지 반경 4).
        [TestCase(1, 1, 4.0f, 50f, 5.0f)]
        [TestCase(2, 1, 4.8f, 65f, 4.5f)]
        [TestCase(3, 2, 4.8f, 65f, 4.5f)]
        [TestCase(4, 2, 5.2f, 80f, 4.0f)]
        [TestCase(5, 3, 5.2f, 100f, 3.5f)]
        public void PerLevel_ResolvesBalanceValues(
            int level, int droneCount, float detectRange, float explosionDamage, float resummonCooldown)
        {
            _skill.ApplyLevel(level);

            Assert.That(_skill.CurrentDroneCount, Is.EqualTo(droneCount));
            Assert.That(_skill.CurrentDetectRange, Is.EqualTo(detectRange).Within(1e-4f));
            Assert.That(_skill.CurrentExplosionDamage, Is.EqualTo(explosionDamage).Within(1e-4f));
            Assert.That(_skill.CurrentResummonCooldown, Is.EqualTo(resummonCooldown).Within(1e-4f));
        }

        [Test]
        public void ApplyLevel_ClampsToRange()
        {
            _skill.ApplyLevel(99);
            Assert.That(_skill.Level, Is.EqualTo(DokkaebiOrbSkill.MaxLevel));

            _skill.ApplyLevel(-3);
            Assert.That(_skill.Level, Is.EqualTo(0));
        }
    }

    public class DokkaebiOrbResummonClockTests
    {
        [Test]
        public void NoCharging_NeverFires_StaysStopped()
        {
            var clock = new DokkaebiOrbResummonClock();
            for (int i = 0; i < 10; i++)
            {
                Assert.That(clock.Tick(1f, anyDroneCharging: false, cooldown: 3f), Is.False);
            }

            Assert.That(clock.IsRunning, Is.False);
        }

        [Test]
        public void StartsOnCharge_FiresAfterCooldown_ThenStopsWhenNoneCharging()
        {
            var clock = new DokkaebiOrbResummonClock();

            // 돌진 시작 → 쿨타임 시작(시작 프레임은 차감하지 않음).
            Assert.That(clock.Tick(1f, anyDroneCharging: true, cooldown: 3f), Is.False);
            Assert.That(clock.IsRunning, Is.True);

            // 드론이 자폭해 더는 돌진하지 않음(소비됨). 쿨타임 경과까지 카운트다운.
            Assert.That(clock.Tick(1f, false, 3f), Is.False); // 3 → 2
            Assert.That(clock.Tick(1f, false, 3f), Is.False); // 2 → 1
            Assert.That(clock.Tick(1f, false, 3f), Is.True);  // 1 → 0 : 일괄 재소환 신호

            // 돌진 중인 드론이 없으므로 클럭은 멈춘다.
            Assert.That(clock.IsRunning, Is.False);
            Assert.That(clock.Tick(1f, false, 3f), Is.False);
        }

        [Test]
        public void KeepsCadence_WhileDronesStillCharging()
        {
            var clock = new DokkaebiOrbResummonClock();

            clock.Tick(1f, true, 3f);                          // start (timer=3)
            Assert.That(clock.Tick(1f, true, 3f), Is.False);   // 3 → 2
            Assert.That(clock.Tick(1f, true, 3f), Is.False);   // 2 → 1
            Assert.That(clock.Tick(1f, true, 3f), Is.True);    // 1 → 0 : fire, restart (still charging)
            Assert.That(clock.IsRunning, Is.True);

            Assert.That(clock.Tick(1f, true, 3f), Is.False);   // 3 → 2
            Assert.That(clock.Tick(1f, true, 3f), Is.False);   // 2 → 1
            Assert.That(clock.Tick(1f, true, 3f), Is.True);    // fires again
        }

        [Test]
        public void Reset_StopsClock()
        {
            var clock = new DokkaebiOrbResummonClock();
            clock.Tick(1f, true, 3f);
            Assert.That(clock.IsRunning, Is.True);

            clock.Reset();
            Assert.That(clock.IsRunning, Is.False);
            Assert.That(clock.Remaining, Is.EqualTo(0f));
            Assert.That(clock.Tick(1f, false, 3f), Is.False);
        }
    }
}
