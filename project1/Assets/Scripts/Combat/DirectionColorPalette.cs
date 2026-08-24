using Mukseon.Core;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 방향 속성(Up/Down/Left/Right)을 색상으로 매핑한다(#82, `combat_system.md` §3).
    /// 한국 전통 오방색에서 흑·백을 제외하고 재구성한 디폴트 값을 가지며,
    /// 에셋이 지정되지 않아도 동작하도록 정적 기본값 폴백을 제공한다.
    ///
    /// 유저 커스텀 매핑(#83)은 이 에셋을 건드리지 않는다 — 런타임 오버레이인
    /// <see cref="DirectionColorSettings"/>가 <see cref="Resolve"/>에서 우선 적용된다.
    /// SO 에셋을 런타임에 수정하면 에디터 세션에 값이 눌러앉아 기본값이 오염되기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "DirectionColorPalette", menuName = "Mukseon/Data/Direction Color Palette")]
    public class DirectionColorPalette : ScriptableObject
    {
        // 디폴트 색상 — 오방색 재구성 (청록 / 황금 / 적 / 녹)
        private static readonly Color DefaultUp = new Color(0.16f, 0.70f, 0.74f);    // 청(靑) — 청록색
        private static readonly Color DefaultDown = new Color(0.95f, 0.78f, 0.22f);  // 황(黃) — 황금색
        private static readonly Color DefaultLeft = new Color(0.85f, 0.27f, 0.27f);  // 적(赤) — 붉은색
        private static readonly Color DefaultRight = new Color(0.32f, 0.72f, 0.36f); // 녹(綠) — 초록색
        private static readonly Color DefaultNone = new Color(0.6f, 0.6f, 0.6f);     // 방향 없음 — 회색

        [SerializeField]
        private Color _up = DefaultUp;

        [SerializeField]
        private Color _down = DefaultDown;

        [SerializeField]
        private Color _left = DefaultLeft;

        [SerializeField]
        private Color _right = DefaultRight;

        [SerializeField]
        private Color _none = DefaultNone;

        /// <summary>지정된 방향의 색상을 반환한다.</summary>
        public Color GetColor(SwipeDirection direction)
        {
            switch (direction)
            {
                case SwipeDirection.Up:
                    return _up;
                case SwipeDirection.Down:
                    return _down;
                case SwipeDirection.Left:
                    return _left;
                case SwipeDirection.Right:
                    return _right;
                default:
                    return _none;
            }
        }

        /// <summary>
        /// 팔레트 에셋이 없을 때 사용하는 정적 디폴트 색상.
        /// 컴포넌트가 팔레트 참조 없이도 동작하도록 보장한다.
        /// </summary>
        public static Color DefaultColor(SwipeDirection direction)
        {
            switch (direction)
            {
                case SwipeDirection.Up:
                    return DefaultUp;
                case SwipeDirection.Down:
                    return DefaultDown;
                case SwipeDirection.Left:
                    return DefaultLeft;
                case SwipeDirection.Right:
                    return DefaultRight;
                default:
                    return DefaultNone;
            }
        }

        /// <summary>
        /// 방향의 최종 표시 색을 조회하는 <b>단일 진입점</b>. 우선순위는
        /// 유저 커스텀 매핑(#83) → 팔레트 에셋 → 정적 디폴트다.
        ///
        /// 적 외곽선 글로우 / HUD 색 오브 / 안내 카드 범례가 모두 이 경로를 쓰기 때문에,
        /// 유저가 매핑을 바꾸면 호출부 수정 없이 세 곳이 한 번에 따라간다.
        /// </summary>
        public static Color Resolve(DirectionColorPalette palette, SwipeDirection direction)
        {
            // 글로우가 매 프레임 이 경로를 타므로 델리게이트 폴백(ResolveColor)을 쓰지 않는다 —
            // 람다가 palette를 캡처해 호출마다 클로저를 할당하게 되기 때문이다.
            if (DirectionColorSettings.TryGetCustomColor(direction, out Color custom))
            {
                return custom;
            }

            return BaseColor(palette, direction);
        }

        /// <summary>커스텀 매핑을 무시한 기본 색(팔레트 에셋 → 정적 디폴트). 설정 UI의 "기본값" 표시에 쓴다.</summary>
        public static Color BaseColor(DirectionColorPalette palette, SwipeDirection direction)
        {
            return palette != null ? palette.GetColor(direction) : DefaultColor(direction);
        }
    }
}
