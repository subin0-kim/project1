namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 강화 카드가 적용하는 효과 종류. <see cref="SkillData"/> 에셋에 정수로 직렬화되므로
    /// 기존 값의 번호를 바꾸지 말고 새 항목만 추가한다.
    /// 카드 카테고리(스탯 / 스킬 / 강신)는 이 값에서 파생된다 — <see cref="SkillData.ResolveCategory"/>.
    /// </summary>
    public enum LevelUpSkillEffectType
    {
        // 스탯 기반 (범용)
        StatFlat = 0,
        StatPercent = 1,
        BonusTargets = 2,
        PickupRadius = 3,         // 혼불 당기기 (자력) — docs 3.3 기준 공용 스킬 7종 중 하나

        // 공용 스킬 (6종) — PickupRadius 포함 시 7종, 클래스 전용 4종과 합산하면 총 11종
        SummonDokkaebiOrb = 10,   // 도깨비불 소환
        InkExplosionOnKill = 11,  // 먹물 폭발 (적 처치 시 광역)
        BarrierRadiusExpand = 12, // 결계 확장
        KnockbackShield = 13,     // 수호 장승의 진 (피격 시 넉백)
        HealthRegen = 14,         // 재생의 굿거리
        InkTrailSlow = 15,        // 끈적한 묵액 (궤적 둔화)

        // 클래스 전용 스킬 (4종)
        FanAttackBuff = 20,       // [무당 전용] 부채살 흩뿌리기
        SwordAttackBuff = 21,     // [박수 전용] 묵직한 신검
        SalPulliKummuBuff = 22,   // [무당 강신] 살풀이 검무
        PaCheonJingBuff = 23,     // [박수 강신] 파천의 징
    }
}
