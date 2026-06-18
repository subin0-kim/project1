using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 도깨비불 소환(#72)의 적 탐색 — 순수 로직.
    /// 원점 기준 탐지 범위 내에서 가장 가까운, 살아있고 타격 가능한 적을 찾는다.
    /// 물리 OverlapCircle 대신 거리 비교(sqrMagnitude)를 사용해 기존 타겟팅과 일관성을 맞추고
    /// EditMode 단위 테스트가 가능하도록 한다.
    /// </summary>
    public static class DokkaebiOrbTargeting
    {
        /// <summary>
        /// <paramref name="origin"/> 기준 <paramref name="range"/>(경계 포함) 내에서 가장 가까운 타격 가능 적을 반환한다.
        /// 범위 내에 적이 없으면 null.
        /// </summary>
        public static EnemyHealth FindNearestTarget(
            Vector2 origin,
            float range,
            IReadOnlyList<EnemyHealth> enemies)
        {
            if (enemies == null || range <= 0f || enemies.Count == 0)
            {
                return null;
            }

            float sqrRange = range * range;
            EnemyHealth nearest = null;
            float nearestSqr = float.PositiveInfinity;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !enemy.IsTargetable)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRange || sqrDistance >= nearestSqr)
                {
                    continue;
                }

                nearest = enemy;
                nearestSqr = sqrDistance;
            }

            return nearest;
        }
    }
}
