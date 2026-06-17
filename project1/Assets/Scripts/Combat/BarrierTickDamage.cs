using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 항마의 결계(#73)의 틱 데미지 적용 — 순수 로직.
    /// 원점 기준 반경 내의 살아있고 타격 가능한 적 전체에 데미지를 적용한다(범위 밖 적은 영향 없음).
    /// 물리 충돌 대신 거리 비교(sqrMagnitude)를 사용해 기존 타겟팅(SwipeAttackTargeting 등)과 일관성을 맞춘다.
    /// </summary>
    public static class BarrierTickDamage
    {
        /// <summary>
        /// 반경 내 적 전체에 <paramref name="damage"/>를 적용하고, 피해를 입힌 적 수를 반환한다.
        /// </summary>
        public static int Apply(
            Vector2 origin,
            float radius,
            float damage,
            IReadOnlyList<EnemyHealth> enemies,
            object source)
        {
            if (enemies == null || radius <= 0f || damage <= 0f)
            {
                return 0;
            }

            float sqrRadius = radius * radius;
            int hitCount = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !enemy.IsTargetable)
                {
                    continue;
                }

                if (((Vector2)enemy.transform.position - origin).sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                enemy.ApplyDamage(damage, source);
                hitCount++;
            }

            return hitCount;
        }
    }
}
