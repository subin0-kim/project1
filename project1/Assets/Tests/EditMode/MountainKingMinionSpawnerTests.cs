using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class MountainKingMinionSpawnerTests
    {
        [Test]
        public void IsBossOnRight_ComparesAgainstCameraCenter()
        {
            Assert.That(MountainKingMinionSpawner.IsBossOnRight(5f, 0f), Is.True);
            Assert.That(MountainKingMinionSpawner.IsBossOnRight(-5f, 0f), Is.False);
            Assert.That(MountainKingMinionSpawner.IsBossOnRight(0f, 0f), Is.True, "중앙은 우측으로 취급.");
        }

        [Test]
        public void ResolveSpawnPointsForSide_ReturnsMatchingSideList()
        {
            var go = new GameObject("Spawner");
            var leftHolder = new GameObject("L");
            var rightHolder = new GameObject("R");

            try
            {
                var spawner = go.AddComponent<MountainKingMinionSpawner>();
                var left = new List<Transform> { leftHolder.transform };
                var right = new List<Transform> { rightHolder.transform };
                spawner.ConfigureForTests(left, right);

                Assert.That(spawner.ResolveSpawnPointsForSide(true), Is.SameAs(right));
                Assert.That(spawner.ResolveSpawnPointsForSide(false), Is.SameAs(left));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(leftHolder);
                Object.DestroyImmediate(rightHolder);
            }
        }

        [Test]
        public void PickRandomNonNull_HandlesEmptyNullAndValid()
        {
            Assert.That(MountainKingMinionSpawner.PickRandomNonNull(null), Is.Null);
            Assert.That(MountainKingMinionSpawner.PickRandomNonNull(new List<Transform>()), Is.Null);
            Assert.That(MountainKingMinionSpawner.PickRandomNonNull(new List<Transform> { null, null }), Is.Null);

            var only = new GameObject("only");
            try
            {
                // null 사이에 단 하나의 유효 포인트만 있어도 항상 그것을 반환한다.
                var points = new List<Transform> { null, only.transform, null };
                for (int i = 0; i < 20; i++)
                {
                    Assert.That(MountainKingMinionSpawner.PickRandomNonNull(points), Is.SameAs(only.transform));
                }
            }
            finally
            {
                Object.DestroyImmediate(only);
            }
        }

        [Test]
        public void AllMinionsCleared_FiresOnlyWhenLastTrackedMinionDies()
        {
            var spawnerGo = new GameObject("Spawner");
            var m1 = new GameObject("m1");
            var m2 = new GameObject("m2");

            try
            {
                var spawner = spawnerGo.AddComponent<MountainKingMinionSpawner>();
                var e1 = m1.AddComponent<EnemyHealth>();
                var e2 = m2.AddComponent<EnemyHealth>();

                // EditMode에서는 AddComponent 시 Awake가 실행되지 않아 IsAlive가 false다.
                // 런타임 스폰의 ResetHealth(PrepareForReuse) 대신 직접 호출해 살아있는 상태로 만든다.
                e1.ResetHealth();
                e2.ResetHealth();

                int clearedCount = 0;
                spawner.OnAllMinionsCleared += () => clearedCount++;

                spawner.Track(e1);
                spawner.Track(e2);
                Assert.That(spawner.AliveMinionCount, Is.EqualTo(2));
                Assert.That(spawner.HasActiveMinions, Is.True);

                e1.Kill();
                Assert.That(clearedCount, Is.EqualTo(0), "한 마리 남았으면 전멸이 아니다.");
                Assert.That(spawner.AliveMinionCount, Is.EqualTo(1));
                Assert.That(spawner.HasActiveMinions, Is.True);

                e2.Kill();
                Assert.That(clearedCount, Is.EqualTo(1), "마지막 부하 사망 시 전멸 1회 발행.");
                Assert.That(spawner.AliveMinionCount, Is.EqualTo(0));
                Assert.That(spawner.HasActiveMinions, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(spawnerGo);
                Object.DestroyImmediate(m1);
                Object.DestroyImmediate(m2);
            }
        }
    }
}
