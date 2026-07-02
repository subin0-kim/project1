using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 발동 효과의 공용 순수 로직(#30). 데미지 적용과 기절 부여를 담당한다.
    /// MonoBehaviour에 의존하지 않아 EditMode 테스트에서 임의의 적 목록으로 검증할 수 있다.
    /// </summary>
    public static class GangshinAbilityEffects
    {
        /// <summary>
        /// 화면 전체(목록 내 살아있고 타격 가능한 모든 적)에 데미지를 적용한다(살풀이 검무).
        /// stunDuration &gt; 0이면 데미지 후 생존한 적에게 기절도 부여한다. 피해를 입힌 적 수를 반환한다.
        /// </summary>
        public static int ApplyToAll(
            IReadOnlyList<EnemyHealth> enemies,
            float damage,
            float stunDuration,
            object source)
        {
            if (enemies == null || damage <= 0f || enemies.Count == 0)
            {
                return 0;
            }

            // ApplyDamage로 적이 사망하면 ActiveEnemies에서 즉시 제거되어 순회 중 컬렉션이 변경될 수 있다.
            // 역순으로 순회하면 사망한 적(현재 인덱스)이 제거돼도 앞쪽 인덱스가 밀리지 않아,
            // 리스트 복사(GC 할당) 없이 안전하다. 화면 전체 타격은 순서가 무관하다.
            int hitCount = 0;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (TryHit(enemies[i], damage, stunDuration, source))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        /// <summary>
        /// 원점 기준 반경(outerRadius, 경계 포함) 내이면서 아직 맞지 않은(alreadyHit 미포함) 적에게
        /// 데미지·기절을 적용한다(파천의 징 파동). 파동이 확장하며 매 스텝 호출되어, 파면이 지나간 적을
        /// 한 번씩만 타격하도록 alreadyHit 집합으로 중복을 방지한다. 이번 스텝에 새로 맞은 적 수를 반환한다.
        /// </summary>
        public static int ApplyExpandingRing(
            Vector2 origin,
            float outerRadius,
            float damage,
            float stunDuration,
            IReadOnlyList<EnemyHealth> enemies,
            HashSet<EnemyHealth> alreadyHit,
            object source)
        {
            if (enemies == null || alreadyHit == null || outerRadius <= 0f || damage <= 0f || enemies.Count == 0)
            {
                return 0;
            }

            // 파동은 확장하는 동안 매 프레임 호출된다. 리스트 복사 대신 역순 순회로 GC 할당을 제거한다
            // (사망한 적은 현재 인덱스에서 제거되므로 앞쪽 인덱스가 밀리지 않아 안전하다).
            float sqrRadius = outerRadius * outerRadius;
            int hitCount = 0;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || alreadyHit.Contains(enemy))
                {
                    continue;
                }

                // 파면(원점~outerRadius) 밖의 적은 아직 파동이 도달하지 않았다.
                if (((Vector2)enemy.transform.position - origin).sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                if (TryHit(enemy, damage, stunDuration, source))
                {
                    // 실제로 타격한 적만 중복 방지 집합에 등록한다(사망/무적 적은 다음 스텝에 재평가돼도 무해).
                    alreadyHit.Add(enemy);
                    hitCount++;
                }
            }

            return hitCount;
        }

        private static bool TryHit(EnemyHealth enemy, float damage, float stunDuration, object source)
        {
            if (enemy == null || !enemy.IsAlive || !enemy.IsTargetable)
            {
                return false;
            }

            enemy.ApplyDamage(damage, source);

            // 데미지로 사망했으면 기절은 의미 없다. 생존 + 이동 AI 보유 시에만 기절을 부여한다.
            if (stunDuration > 0f && enemy.IsAlive && enemy.Mover != null)
            {
                enemy.Mover.ApplyStun(stunDuration);
            }

            return true;
        }
    }
}
