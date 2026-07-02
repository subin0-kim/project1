using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 발동 시 Ability에 전달되는 컨텍스트(#30). 슬롯 시스템(#59)이 장착 슬롯 정보를 채워 전달한다.
    /// 효과 대상 적 목록(Enemies)을 명시적으로 받아, EditMode 테스트에서 임의의 목록을 주입할 수 있게 한다.
    /// </summary>
    public readonly struct GangshinSlotContext
    {
        /// <summary>발동 원점(플레이어 위치). 파동 / 범위 계산의 중심.</summary>
        public readonly Vector2 Origin;

        /// <summary>발동 레벨(1-based). 강화 카드(#66)로 상승.</summary>
        public readonly int Level;

        /// <summary>데미지 출처(귀속 처리용).</summary>
        public readonly object Source;

        /// <summary>효과 대상 적 목록. 보통 <see cref="EnemyHealth.ActiveEnemies"/>.</summary>
        public readonly IReadOnlyList<EnemyHealth> Enemies;

        public GangshinSlotContext(Vector2 origin, int level, object source, IReadOnlyList<EnemyHealth> enemies)
        {
            Origin = origin;
            Level = Mathf.Max(1, level);
            Source = source;
            Enemies = enemies;
        }
    }
}
