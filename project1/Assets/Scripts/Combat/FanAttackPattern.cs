using System.Collections.Generic;
using Mukseon.Core.Input;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 부채살 흩뿌리기(#76, [무당 전용])의 코어 메커니즘 — 순수 로직.
    /// 발동 시 스와이프 방향을 기준으로 부채꼴로 '여러 방향'을 동시에 타격한다.
    /// 타격은 방향 속성 일치로만 결정되고 방향이 4방위뿐이므로(combat_system.md §1~2),
    /// "갈래"는 기하학적 선이 아니라 '타격 방향'으로 매핑한다.
    ///
    /// 레벨별 갈래(skill_balance_mvp.md §5):
    ///   Lv1 (3갈래) : 스와이프 방향 + 좌우 인접(수직) 2방향
    ///   Lv2 (4갈래) : 전 방향(상하좌우)
    ///   Lv3 (5갈래) : 전 방향 + 스와이프 방향에 1타 추가 (5번째 갈래)
    ///
    /// 각 방향은 정확히 한 번만 질의되므로(스와이프 방향의 추가 타격은 BonusTargets로 표현),
    /// 적은 자신의 단일 방향 속성으로 한 브랜치에만 매칭되어 중복 타격이 발생하지 않는다.
    /// </summary>
    public static class FanAttackPattern
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 3;

        /// <summary>한 부채 갈래: 타격할 방향과, 해당 방향에 더할 추가 타깃 수.</summary>
        public readonly struct FanBranch
        {
            public readonly SwipeDirection Direction;

            /// <summary>이 방향에 기본 타깃 수 위로 더 얹을 추가 타깃 수(Lv3 스와이프 방향 = 1).</summary>
            public readonly int BonusTargets;

            public FanBranch(SwipeDirection direction, int bonusTargets)
            {
                Direction = direction;
                BonusTargets = bonusTargets;
            }
        }

        /// <summary>지정 방향의 반대 방향을 반환한다. None은 None.</summary>
        public static SwipeDirection Opposite(SwipeDirection direction)
        {
            switch (direction)
            {
                case SwipeDirection.Up: return SwipeDirection.Down;
                case SwipeDirection.Down: return SwipeDirection.Up;
                case SwipeDirection.Left: return SwipeDirection.Right;
                case SwipeDirection.Right: return SwipeDirection.Left;
                default: return SwipeDirection.None;
            }
        }

        /// <summary>지정 방향에 수직인 두 방향을 반환한다(상/하 → 좌·우, 좌/우 → 상·하).</summary>
        public static bool TryGetPerpendiculars(SwipeDirection direction, out SwipeDirection a, out SwipeDirection b)
        {
            switch (direction)
            {
                case SwipeDirection.Up:
                case SwipeDirection.Down:
                    a = SwipeDirection.Left;
                    b = SwipeDirection.Right;
                    return true;
                case SwipeDirection.Left:
                case SwipeDirection.Right:
                    a = SwipeDirection.Up;
                    b = SwipeDirection.Down;
                    return true;
                default:
                    a = SwipeDirection.None;
                    b = SwipeDirection.None;
                    return false;
            }
        }

        /// <summary>
        /// 스와이프 방향과 레벨로 부채 갈래 목록을 만든다. <paramref name="output"/>는 초기화 후 채워진다.
        /// 반환값은 채워진 갈래 수. 유효하지 않은 입력(방향 None 등)이면 0.
        /// </summary>
        public static int BuildBranches(SwipeDirection swipeDirection, int level, List<FanBranch> output)
        {
            if (output == null)
            {
                return 0;
            }

            output.Clear();

            if (swipeDirection == SwipeDirection.None)
            {
                return 0;
            }

            if (!TryGetPerpendiculars(swipeDirection, out SwipeDirection perpA, out SwipeDirection perpB))
            {
                return 0;
            }

            int clampedLevel = level < MinLevel ? MinLevel : (level > MaxLevel ? MaxLevel : level);

            // Lv3은 스와이프 방향에 1타를 더 얹는다(5번째 갈래).
            int swipeBonusTargets = clampedLevel >= 3 ? 1 : 0;
            output.Add(new FanBranch(swipeDirection, swipeBonusTargets));

            // Lv1부터 좌우 인접(수직) 2방향.
            output.Add(new FanBranch(perpA, 0));
            output.Add(new FanBranch(perpB, 0));

            // Lv2부터 반대 방향까지 전 방향.
            if (clampedLevel >= 2)
            {
                output.Add(new FanBranch(Opposite(swipeDirection), 0));
            }

            return output.Count;
        }
    }
}
