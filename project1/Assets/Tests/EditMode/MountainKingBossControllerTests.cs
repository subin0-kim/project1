using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class MountainKingBossControllerTests
    {
        private static GameObject MakeBoss(out MountainKingBossController controller)
        {
            var go = new GameObject("Boss");
            go.AddComponent<EnemyHealth>();
            go.AddComponent<BossHealthComponent>();
            controller = go.AddComponent<MountainKingBossController>();
            return go;
        }

        [Test]
        public void ResolveCounterDirection_MapsByCounterType()
        {
            var charge = new BossPatternDefinition(BossPatternType.Charge, BossCounterType.BossDirection);
            var claw = new BossPatternDefinition(BossPatternType.ClawSwipe, BossCounterType.PatternDirection);
            var roar = new BossPatternDefinition(BossPatternType.Roar, BossCounterType.None);

            Assert.That(
                MountainKingBossController.ResolveCounterDirection(charge, SwipeDirection.Left, SwipeDirection.Up),
                Is.EqualTo(SwipeDirection.Left), "BossDirection은 보스 방향을 사용한다.");

            Assert.That(
                MountainKingBossController.ResolveCounterDirection(claw, SwipeDirection.Left, SwipeDirection.Up),
                Is.EqualTo(SwipeDirection.Up), "PatternDirection은 굴린 방향을 사용한다.");

            Assert.That(
                MountainKingBossController.ResolveCounterDirection(roar, SwipeDirection.Left, SwipeDirection.Up),
                Is.EqualTo(SwipeDirection.None), "None은 카운터 방향이 없다.");

            Assert.That(
                MountainKingBossController.ResolveCounterDirection(null, SwipeDirection.Left, SwipeDirection.Up),
                Is.EqualTo(SwipeDirection.None));
        }

        [Test]
        public void IsCounterMatch_RequiresNonNoneAndExactMatch()
        {
            Assert.That(MountainKingBossController.IsCounterMatch(SwipeDirection.Left, SwipeDirection.Left), Is.True);
            Assert.That(MountainKingBossController.IsCounterMatch(SwipeDirection.Left, SwipeDirection.Right), Is.False);
            Assert.That(MountainKingBossController.IsCounterMatch(SwipeDirection.None, SwipeDirection.None), Is.False);
        }

        [Test]
        public void ComputeCounterDamage_AppliesMultiplierClampedToOne()
        {
            var pattern = new BossPatternDefinition(
                BossPatternType.Charge, BossCounterType.BossDirection, counterBonusDamage: 20f);

            Assert.That(MountainKingBossController.ComputeCounterDamage(pattern, 1.5f), Is.EqualTo(30f));
            Assert.That(MountainKingBossController.ComputeCounterDamage(pattern, 0.5f), Is.EqualTo(20f), "배율은 1 미만으로 내려가지 않는다.");
            Assert.That(MountainKingBossController.ComputeCounterDamage(null, 1.5f), Is.EqualTo(0f));
        }

        [Test]
        public void MirrorOffsetForSide_FlipsXOnLeftKeepsY()
        {
            var offset = new Vector2(0.3f, 0.1f);

            Assert.That(
                MountainKingBossController.MirrorOffsetForSide(offset, true),
                Is.EqualTo(offset), "오른쪽 측면은 오프셋을 그대로 사용한다.");

            Assert.That(
                MountainKingBossController.MirrorOffsetForSide(offset, false),
                Is.EqualTo(new Vector2(-0.3f, 0.1f)), "왼쪽 측면은 x만 미러링하고 y는 유지한다.");
        }

        [Test]
        public void RollDirectionSequence_HasRequestedLengthAndNoImmediateRepeats()
        {
            var seq = new List<SwipeDirection>();

            for (int trial = 0; trial < 50; trial++)
            {
                MountainKingBossController.RollDirectionSequence(3, seq);
                Assert.That(seq.Count, Is.EqualTo(3));

                for (int i = 0; i < seq.Count; i++)
                {
                    Assert.That(seq[i], Is.Not.EqualTo(SwipeDirection.None));
                    if (i > 0)
                    {
                        Assert.That(seq[i], Is.Not.EqualTo(seq[i - 1]), "연속 단계는 같은 방향이 나오지 않는다.");
                    }
                }
            }

            MountainKingBossController.RollDirectionSequence(0, seq);
            Assert.That(seq.Count, Is.EqualTo(0), "길이 0이면 빈 시퀀스.");
        }

        [Test]
        public void SelectNextPatternIndex_AvoidsImmediateRepeat()
        {
            GameObject go = MakeBoss(out MountainKingBossController controller);

            try
            {
                Assert.That(controller.SelectNextPatternIndex(0, -1), Is.EqualTo(-1), "패턴이 없으면 -1.");
                Assert.That(controller.SelectNextPatternIndex(1, 0), Is.EqualTo(0), "패턴이 하나면 항상 0.");
                Assert.That(controller.SelectNextPatternIndex(1, -1), Is.EqualTo(0));

                for (int i = 0; i < 200; i++)
                {
                    int index = controller.SelectNextPatternIndex(3, 1);
                    Assert.That(index, Is.InRange(0, 2));
                    Assert.That(index, Is.Not.EqualTo(1), "직전 패턴은 연속 반복되지 않는다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PhaseTransition_IsDeferredWhilePatternActive_AppliedAfter()
        {
            GameObject go = MakeBoss(out MountainKingBossController controller);

            try
            {
                controller.ConfigureForTests(null, 0);

                int changedTo = -1;
                controller.OnPhaseChanged += phase => changedTo = phase;

                // 패턴 진행 중에는 임계값 도달이 들어와도 전환을 미룬다.
                controller.PatternActiveForTests = true;
                controller.RequestPhaseTransition(1);

                Assert.That(controller.TryApplyPendingPhase(), Is.False);
                Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(0));
                Assert.That(controller.PendingPhaseIndexForTests, Is.EqualTo(1));
                Assert.That(changedTo, Is.EqualTo(-1));

                // 패턴 종료 후 적용된다.
                controller.PatternActiveForTests = false;
                Assert.That(controller.TryApplyPendingPhase(), Is.True);
                Assert.That(controller.CurrentPhaseIndex, Is.EqualTo(1));
                Assert.That(controller.PendingPhaseIndexForTests, Is.EqualTo(-1));
                Assert.That(changedTo, Is.EqualTo(1));

                // 더 이상 대기 전환이 없으면 재적용되지 않는다.
                Assert.That(controller.TryApplyPendingPhase(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
