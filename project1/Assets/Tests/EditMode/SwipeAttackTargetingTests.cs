using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class SwipeAttackTargetingTests
    {
        [Test]
        public void SelectNearestTargets_FiltersByDirection_AndReturnsClosestOne()
        {
            var root = new GameObject("Root");
            var output = new List<EnemyHealth>();

            try
            {
                EnemyHealth upNear = CreateEnemy("Dummy_Up_Near", new Vector3(0f, 1f, 0f), root.transform, SwipeDirection.Up);
                EnemyHealth upFar = CreateEnemy("Dummy_Up_Far", new Vector3(0f, 5f, 0f), root.transform, SwipeDirection.Up);
                EnemyHealth left = CreateEnemy("Dummy_Left", new Vector3(-0.5f, 0f, 0f), root.transform, SwipeDirection.Left);

                EnemyHealth[] enemies = { upNear, upFar, left };

                int selectedCount = SwipeAttackTargeting.SelectNearestTargets(
                    Vector2.zero,
                    SwipeDirection.Up,
                    enemies,
                    1,
                    output);

                Assert.That(selectedCount, Is.EqualTo(1));
                Assert.That(output[0], Is.EqualTo(upNear));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelectNearestTargets_ReturnsClosestNTargets_WithSameDirection()
        {
            var root = new GameObject("Root");
            var output = new List<EnemyHealth>();

            try
            {
                EnemyHealth upNear = CreateEnemy("Dummy_Up_1", new Vector3(0f, 1f, 0f), root.transform, SwipeDirection.Up);
                EnemyHealth upMid = CreateEnemy("Dummy_Up_2", new Vector3(0f, 2f, 0f), root.transform, SwipeDirection.Up);
                EnemyHealth upFar = CreateEnemy("Dummy_Up_3", new Vector3(0f, 3f, 0f), root.transform, SwipeDirection.Up);

                EnemyHealth[] enemies = { upFar, upMid, upNear };

                int selectedCount = SwipeAttackTargeting.SelectNearestTargets(
                    Vector2.zero,
                    SwipeDirection.Up,
                    enemies,
                    2,
                    output);

                Assert.That(selectedCount, Is.EqualTo(2));
                Assert.That(output[0], Is.EqualTo(upNear));
                Assert.That(output[1], Is.EqualTo(upMid));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static EnemyHealth CreateEnemy(string name, Vector3 position, Transform parent, SwipeDirection swipeDirection)
        {
            var enemyObject = new GameObject(name);
            enemyObject.transform.SetParent(parent);
            enemyObject.transform.position = position;

            var enemyHealth = enemyObject.AddComponent<EnemyHealth>();
            enemyHealth.SetSwipeDirection(swipeDirection);
            enemyHealth.ResetHealth();
            return enemyHealth;
        }
    }
}
