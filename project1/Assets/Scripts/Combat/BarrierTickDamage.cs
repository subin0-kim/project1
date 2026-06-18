using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 항마의 결계(#73)의 틱 데미지 적용 — 결계 전용 진입점.
    /// 실제 반경 데미지 로직은 공용 <see cref="RadialDamage.ApplyInRadius"/>에 위임한다.
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
            return RadialDamage.ApplyInRadius(origin, radius, damage, enemies, source);
        }
    }
}
