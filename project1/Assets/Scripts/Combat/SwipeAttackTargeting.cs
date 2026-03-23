using System.Collections.Generic;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    public static class SwipeAttackTargeting
    {
        private struct Candidate
        {
            public EnemyHealth Enemy;
            public float SqrDistance;
        }

        public static int SelectNearestTargets(
            Vector2 origin,
            SwipeDirection swipeDirection,
            EnemyHealth[] enemies,
            int maxTargets,
            List<EnemyHealth> output)
        {
            output.Clear();

            if (enemies == null || enemies.Length == 0 || maxTargets <= 0 || swipeDirection == SwipeDirection.None)
            {
                return 0;
            }

            var candidates = new List<Candidate>(enemies.Length);

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || enemy.SwipeDirection != swipeDirection)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                candidates.Add(new Candidate
                {
                    Enemy = enemy,
                    SqrDistance = sqrDistance
                });
            }

            candidates.Sort((left, right) => left.SqrDistance.CompareTo(right.SqrDistance));

            int targetCount = Mathf.Min(maxTargets, candidates.Count);
            for (int i = 0; i < targetCount; i++)
            {
                output.Add(candidates[i].Enemy);
            }

            return output.Count;
        }
    }
}
