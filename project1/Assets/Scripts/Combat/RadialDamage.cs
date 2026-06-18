using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 원점 기준 반경 내의 살아있고 타격 가능한 적 전체에 데미지를 적용하는 공용 순수 로직.
    /// 항마의 결계(#73) 틱 데미지와 도깨비불 소환(#72) 자폭 폭발이 공통으로 사용한다.
    /// 물리 충돌 대신 거리 비교(sqrMagnitude)를 사용해 기존 타겟팅(SwipeAttackTargeting 등)과 일관성을 맞춘다.
    /// </summary>
    public static class RadialDamage
    {
        /// <summary>
        /// 원점(<paramref name="origin"/>) 기준 반경(<paramref name="radius"/>, 경계 포함) 내의 적 전체에
        /// <paramref name="damage"/>를 적용하고, 피해를 입힌 적 수를 반환한다(반경 밖 적은 영향 없음).
        /// </summary>
        public static int ApplyInRadius(
            Vector2 origin,
            float radius,
            float damage,
            IReadOnlyList<EnemyHealth> enemies,
            object source)
        {
            if (enemies == null || radius <= 0f || damage <= 0f || enemies.Count == 0)
            {
                return 0;
            }

            // ApplyDamage로 적이 사망하면 EnemyHealth.ActiveEnemies에서 즉시 제거되어
            // 순회 중 컬렉션이 변경(인덱스 밀림 → 다음 적 스킵)될 수 있다. 스냅샷을 떠서 순회한다.
            var targets = new List<EnemyHealth>(enemies);
            float sqrRadius = radius * radius;
            int hitCount = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                EnemyHealth enemy = targets[i];
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
