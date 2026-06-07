using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 방향 속성(Up/Down/Left/Right)을 색상으로 매핑한다(#82, `combat_system.md` §3).
    /// 한국 전통 오방색에서 흑·백을 제외하고 재구성한 디폴트 값을 가지며,
    /// 에셋이 지정되지 않아도 동작하도록 정적 기본값 폴백을 제공한다.
    /// 유저 커스텀 매핑/저장은 후행 이슈(#83)에서 다룬다.
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

        /// <summary>팔레트가 null이어도 안전하게 색을 조회하는 헬퍼.</summary>
        public static Color Resolve(DirectionColorPalette palette, SwipeDirection direction)
        {
            return palette != null ? palette.GetColor(direction) : DefaultColor(direction);
        }
    }
}
