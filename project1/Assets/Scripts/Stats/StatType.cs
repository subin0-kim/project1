namespace Mukseon.Gameplay.Stats
{
    /// <summary>
    /// 플레이어 스탯 종류(#40, `player_stats.md`). 직렬화 데이터(PlayerStatsDefinition 에셋)에
    /// 정수로 저장되므로, 기존 값의 번호를 바꾸지 말고 새 항목은 끝에 추가한다.
    /// </summary>
    public enum StatType
    {
        MaxHealth = 0,
        AttackPower = 1,
        AttackSpeed = 2,                 // 레거시(미사용). 쿨타임 기반 공격 속도는 AttackCooldown 사용.

        Defense = 3,                     // 방어력: 피격 데미지 감소율(%). 상한 70%.
        AttackCooldown = 4,              // 공격 속도: 스와이프 공격 후 다음 공격까지의 쿨타임(초). 낮을수록 빠름.
        MultiHit = 5,                    // 다중 히트: 스와이프 1회 동시 타격 가능한 적의 수.
        GangshinGaugeChargeRate = 6,     // 강신 게이지 충전 배율: 적 처치 시 충전량 배율.
        MagnetRadius = 7,                // 자력 반경: 플레이어 주변 혼불 자동 흡수 범위.
        SwipeEndpointPullRadius = 8,     // 스와이프 끝점 당기기 반경.
        HonbulMoveDistance = 9,          // 당겨지는 혼불이 한 번에 중앙으로 이동하는 거리.
        HonbulAcquireMultiplier = 10,    // 혼불 1개당 획득 경험치 배율.
        GoldGain = 11,                   // 골드 획득 배율(골드 시스템 도입 시 연동).
        ExperienceGain = 12,             // 혼불 배율과 별개로 적용되는 경험치 보너스 배율.

        // 스와이프 최종 데미지 배율(기본 1.0). AttackPower와 나누어 둔 이유:
        // AttackPower는 소비자(SwipeAttackEventListener.ResolveDamage)가 기본 데미지에 '더하는' 가산항이라,
        // 여기에 Percent 보정을 걸면 총 데미지가 아니라 가산항만 커진다(+30% 표기 → 실제 +15%, 캐릭터마다 다름).
        // 이 스탯은 (기본 데미지 + AttackPower) 전체에 곱해지므로 표기한 비율이 그대로 체감된다.
        SwipeDamageMultiplier = 13,
    }
}
