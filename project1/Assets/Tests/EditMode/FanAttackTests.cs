using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class FanAttackTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
        }

        // ---- FanAttackPattern (코어 메커니즘) ----

        [Test]
        public void Opposite_MapsCardinalDirections()
        {
            Assert.That(FanAttackPattern.Opposite(SwipeDirection.Up), Is.EqualTo(SwipeDirection.Down));
            Assert.That(FanAttackPattern.Opposite(SwipeDirection.Down), Is.EqualTo(SwipeDirection.Up));
            Assert.That(FanAttackPattern.Opposite(SwipeDirection.Left), Is.EqualTo(SwipeDirection.Right));
            Assert.That(FanAttackPattern.Opposite(SwipeDirection.Right), Is.EqualTo(SwipeDirection.Left));
            Assert.That(FanAttackPattern.Opposite(SwipeDirection.None), Is.EqualTo(SwipeDirection.None));
        }

        [Test]
        public void Perpendiculars_AreAxisSwapped()
        {
            Assert.That(FanAttackPattern.TryGetPerpendiculars(SwipeDirection.Up, out var a1, out var b1), Is.True);
            Assert.That(new[] { a1, b1 }, Is.EquivalentTo(new[] { SwipeDirection.Left, SwipeDirection.Right }));

            Assert.That(FanAttackPattern.TryGetPerpendiculars(SwipeDirection.Left, out var a2, out var b2), Is.True);
            Assert.That(new[] { a2, b2 }, Is.EquivalentTo(new[] { SwipeDirection.Up, SwipeDirection.Down }));

            Assert.That(FanAttackPattern.TryGetPerpendiculars(SwipeDirection.None, out _, out _), Is.False);
        }

        [Test]
        public void BuildBranches_Level1_HitsSwipeAndTwoPerpendiculars_NoOpposite()
        {
            var branches = new List<FanAttackPattern.FanBranch>();
            int count = FanAttackPattern.BuildBranches(SwipeDirection.Up, 1, branches);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(Directions(branches), Is.EquivalentTo(new[]
            {
                SwipeDirection.Up, SwipeDirection.Left, SwipeDirection.Right
            }));
            Assert.That(branches, Has.None.Matches<FanAttackPattern.FanBranch>(b => b.Direction == SwipeDirection.Down));
            // Lv1은 추가 타깃 없음.
            Assert.That(TotalBonusTargets(branches), Is.EqualTo(0));
        }

        [Test]
        public void BuildBranches_Level2_HitsAllFourDirections_NoBonus()
        {
            var branches = new List<FanAttackPattern.FanBranch>();
            int count = FanAttackPattern.BuildBranches(SwipeDirection.Left, 2, branches);

            Assert.That(count, Is.EqualTo(4));
            Assert.That(Directions(branches), Is.EquivalentTo(new[]
            {
                SwipeDirection.Left, SwipeDirection.Right, SwipeDirection.Up, SwipeDirection.Down
            }));
            Assert.That(TotalBonusTargets(branches), Is.EqualTo(0));
        }

        [Test]
        public void BuildBranches_Level3_AddsOneBonusTargetToSwipeDirection()
        {
            var branches = new List<FanAttackPattern.FanBranch>();
            int count = FanAttackPattern.BuildBranches(SwipeDirection.Right, 3, branches);

            Assert.That(count, Is.EqualTo(4));
            Assert.That(Directions(branches), Is.EquivalentTo(new[]
            {
                SwipeDirection.Right, SwipeDirection.Up, SwipeDirection.Down, SwipeDirection.Left
            }));

            // 5번째 갈래 = 스와이프 방향(Right)에 1타 추가.
            FanAttackPattern.FanBranch swipeBranch = branches.Find(b => b.Direction == SwipeDirection.Right);
            Assert.That(swipeBranch.BonusTargets, Is.EqualTo(1));
            Assert.That(TotalBonusTargets(branches), Is.EqualTo(1));
        }

        [Test]
        public void BuildBranches_ClampsLevelOutOfRange()
        {
            var low = new List<FanAttackPattern.FanBranch>();
            var high = new List<FanAttackPattern.FanBranch>();

            // 0 이하는 Lv1로, 4 이상은 Lv3로 클램프.
            FanAttackPattern.BuildBranches(SwipeDirection.Up, 0, low);
            FanAttackPattern.BuildBranches(SwipeDirection.Up, 99, high);

            Assert.That(low.Count, Is.EqualTo(3));
            Assert.That(high.Count, Is.EqualTo(4));
            Assert.That(TotalBonusTargets(high), Is.EqualTo(1));
        }

        [Test]
        public void BuildBranches_NoneDirection_ReturnsZero()
        {
            var branches = new List<FanAttackPattern.FanBranch>();
            int count = FanAttackPattern.BuildBranches(SwipeDirection.None, 3, branches);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(branches, Is.Empty);
        }

        // ---- FanAttackSkill (확률 게이팅 / 레벨 추적) ----

        [Test]
        public void Skill_NotOwned_DoesNotTrigger()
        {
            FanAttackSkill skill = CreateSkill();
            var branches = new List<FanAttackPattern.FanBranch>();

            // 레벨 0 = 미보유. roll 0(무조건 발동 시도)에도 발동하지 않아야 한다.
            bool triggered = skill.TryBuildFanBranches(SwipeDirection.Up, 0f, branches);

            Assert.That(skill.Level, Is.EqualTo(0));
            Assert.That(triggered, Is.False);
            Assert.That(branches, Is.Empty);
        }

        [Test]
        public void Skill_Level1_TriggersWhenRollBelowChance()
        {
            FanAttackSkill skill = CreateSkill();
            skill.ApplyLevel(1);
            var branches = new List<FanAttackPattern.FanBranch>();

            Assert.That(skill.CurrentTriggerChance, Is.GreaterThan(0f));

            bool triggered = skill.TryBuildFanBranches(SwipeDirection.Up, 0f, branches);

            Assert.That(triggered, Is.True);
            Assert.That(branches.Count, Is.EqualTo(3));
        }

        [Test]
        public void Skill_Level1_DoesNotTriggerWhenRollAtOrAboveChance()
        {
            FanAttackSkill skill = CreateSkill();
            skill.ApplyLevel(1);
            var branches = new List<FanAttackPattern.FanBranch>();

            // roll 0.99는 어떤 레벨 확률(<1)보다도 크므로 미발동.
            bool triggered = skill.TryBuildFanBranches(SwipeDirection.Up, 0.99f, branches);

            Assert.That(triggered, Is.False);
            Assert.That(branches, Is.Empty);
        }

        [Test]
        public void Skill_ApplyLevel_ClampsToMaxLevel()
        {
            FanAttackSkill skill = CreateSkill();
            skill.ApplyLevel(99);

            Assert.That(skill.Level, Is.EqualTo(FanAttackPattern.MaxLevel));

            var branches = new List<FanAttackPattern.FanBranch>();
            skill.TryBuildFanBranches(SwipeDirection.Up, 0f, branches);
            // Lv3 = 4갈래 + 스와이프 방향 보너스 1.
            Assert.That(branches.Count, Is.EqualTo(4));
        }

        // ---- helpers ----

        private FanAttackSkill CreateSkill()
        {
            var go = new GameObject("FanAttackSkill");
            _spawned.Add(go);
            return go.AddComponent<FanAttackSkill>();
        }

        private static IEnumerable<SwipeDirection> Directions(IEnumerable<FanAttackPattern.FanBranch> branches)
        {
            var list = new List<SwipeDirection>();
            foreach (FanAttackPattern.FanBranch b in branches)
            {
                list.Add(b.Direction);
            }

            return list;
        }

        private static int TotalBonusTargets(IEnumerable<FanAttackPattern.FanBranch> branches)
        {
            int total = 0;
            foreach (FanAttackPattern.FanBranch b in branches)
            {
                total += b.BonusTargets;
            }

            return total;
        }
    }
}
