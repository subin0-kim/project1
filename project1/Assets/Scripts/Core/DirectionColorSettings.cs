using System;
using Mukseon.Core.Input;
using Mukseon.Core.Persistence;
using UnityEngine;

namespace Mukseon.Core
{
    /// <summary>
    /// 커스텀 지정이 없을 때 쓸 기본 색을 알려주는 콜백(#83).
    /// <c>DirectionColorPalette</c>(Gameplay.Combat)를 Core에서 직접 참조하지 않기 위한 seam이다.
    /// </summary>
    public delegate Color DirectionColorSource(SwipeDirection direction);

    /// <summary>
    /// 방향 색상 표시의 유저 설정(#83, `combat_system.md` §3/§8): 표시 방식, 화살표 병행 표시,
    /// 방향↔색상 커스텀 매핑을 런타임 단일 소유자로 들고 있다.
    ///
    /// <b>순수 정적 상태 + 변경 이벤트만</b> 담당하고 파일 IO는 하지 않는다. 저장/로드는
    /// <see cref="ApplyFrom"/> / <see cref="WriteTo"/>로 <see cref="SaveData"/>와 주고받으며,
    /// 실제 영속화는 설정 UI가 <see cref="SaveGateway"/>를 통해 수행한다
    /// (<see cref="SaveService"/>의 "각 소유 시스템이 Current를 수정한 뒤 Save()" 규약).
    /// 덕분에 EditMode 테스트가 실제 세이브 파일을 건드리지 않고 전 로직을 검증할 수 있다.
    ///
    /// 소비자(적 글로우 / HUD 색 오브 / 안내 카드 범례)는 <see cref="OnChanged"/>를 구독해
    /// 인게임 중 설정 변경을 즉시 반영한다.
    /// </summary>
    public static class DirectionColorSettings
    {
        public const DirectionDisplayMode DefaultDisplayMode = DirectionDisplayMode.Both;
        public const bool DefaultArrowAssist = false;

        // SwipeDirection의 최대 값(Right = 4) + 1. 방향을 배열 인덱스로 바로 쓰기 위한 크기다.
        private const int DirectionSlotCount = 5;

        private static DirectionDisplayMode _displayMode = DefaultDisplayMode;
        private static bool _arrowAssist = DefaultArrowAssist;

        // 인덱스 = (int)SwipeDirection. null이면 "유저 지정 없음"이라 팔레트/정적 디폴트가 쓰인다.
        private static readonly Color?[] CustomColors = new Color?[DirectionSlotCount];

        /// <summary>설정이 실제로 바뀐 순간에만 발행된다. 같은 값을 다시 넣으면 발행하지 않는다.</summary>
        public static event Action OnChanged;

        public static DirectionDisplayMode DisplayMode => _displayMode;

        /// <summary>색맹·색약용 방향 화살표 병행 표시 여부. 색상 표시 방식과 완전히 독립적이다.</summary>
        public static bool ArrowAssistEnabled => _arrowAssist;

        /// <summary>적 외곽선 글로우를 그릴지. 표시 방식이 '오브 전용'일 때만 꺼진다.</summary>
        public static bool GlowEnabled => _displayMode != DirectionDisplayMode.Orb;

        /// <summary>적 머리 위 색 오브를 그릴지. 표시 방식이 '글로우 전용'일 때만 꺼진다.</summary>
        public static bool OrbEnabled => _displayMode != DirectionDisplayMode.Glow;

        /// <summary>유저 지정 색이 있으면 반환한다. 없으면 false — 호출자가 팔레트/정적 디폴트로 폴백한다.</summary>
        public static bool TryGetCustomColor(SwipeDirection direction, out Color color)
        {
            Color? stored = IsValidIndex(direction) ? CustomColors[(int)direction] : null;
            if (stored.HasValue)
            {
                color = stored.Value;
                return true;
            }

            color = default;
            return false;
        }

        public static void SetDisplayMode(DirectionDisplayMode mode)
        {
            if (_displayMode == mode)
            {
                return;
            }

            _displayMode = mode;
            OnChanged?.Invoke();
        }

        public static void SetArrowAssist(bool enabled)
        {
            if (_arrowAssist == enabled)
            {
                return;
            }

            _arrowAssist = enabled;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 방향에 유저 색을 지정한다. 그 색을 이미 다른 방향이 쓰고 있으면 <b>두 방향의 색을 맞바꾼다</b>.
        ///
        /// 두 방향이 같은 색이 되면 "색 = 베어야 할 방향"이라는 규칙 자체가 무너져 그 적을 벨 방향을
        /// 판별할 수 없게 된다. 선택을 거부하는 대신 교환하면 4색이 항상 서로 다른 상태로 유지되면서,
        /// 유저는 스와치 한 번 탭으로 원하는 배치에 도달할 수 있다.
        /// </summary>
        public static void SetCustomColor(SwipeDirection direction, Color color, DirectionColorSource baseColors)
        {
            if (direction == SwipeDirection.None || !IsValidIndex(direction))
            {
                return;
            }

            color.a = 1f;
            Color previous = ResolveColor(direction, baseColors);
            if (ColorsEqual(previous, color))
            {
                return;
            }

            // 같은 색을 이미 쓰고 있는 다른 방향에 이 방향의 원래 색을 넘겨 충돌을 없앤다.
            for (int i = 1; i < DirectionSlotCount; i++)
            {
                var other = (SwipeDirection)i;
                if (other == direction)
                {
                    continue;
                }

                if (ColorsEqual(ResolveColor(other, baseColors), color))
                {
                    CustomColors[i] = previous;
                }
            }

            CustomColors[(int)direction] = color;
            OnChanged?.Invoke();
        }

        /// <summary>커스텀 매핑만 지우고 표시 방식·화살표 설정은 유지한다.</summary>
        public static void ClearCustomColors()
        {
            bool changed = false;
            for (int i = 0; i < DirectionSlotCount; i++)
            {
                if (CustomColors[i].HasValue)
                {
                    CustomColors[i] = null;
                    changed = true;
                }
            }

            if (changed)
            {
                OnChanged?.Invoke();
            }
        }

        /// <summary>
        /// 구독자를 모두 끊는다. Domain Reload를 끈 에디터에서 이전 플레이 세션의 파기된 오브젝트가
        /// 정적 이벤트에 남아 다음 세션의 첫 변경에서 예외를 던지는 것을 막는다.
        /// 플레이 진입 시 1회만 호출한다(<see cref="DirectionColorSettingsBootstrap"/>).
        /// </summary>
        public static void ClearSubscribers()
        {
            OnChanged = null;
        }

        /// <summary>모든 방향 색상 설정을 공장 초기값으로 되돌린다.</summary>
        public static void ResetToDefaults()
        {
            bool changed = _displayMode != DefaultDisplayMode || _arrowAssist != DefaultArrowAssist;
            _displayMode = DefaultDisplayMode;
            _arrowAssist = DefaultArrowAssist;

            for (int i = 0; i < DirectionSlotCount; i++)
            {
                changed |= CustomColors[i].HasValue;
                CustomColors[i] = null;
            }

            if (changed)
            {
                OnChanged?.Invoke();
            }
        }

        /// <summary>세이브 데이터의 값을 런타임 상태로 반영한다. 변경이 있으면 <see cref="OnChanged"/>를 1회 발행한다.</summary>
        public static void ApplyFrom(SaveData data)
        {
            if (data == null)
            {
                ResetToDefaults();
                return;
            }

            DirectionDisplayMode mode = ToDisplayMode(data.DirectionDisplayMode);
            bool changed = _displayMode != mode || _arrowAssist != data.DirectionArrowAssist;
            _displayMode = mode;
            _arrowAssist = data.DirectionArrowAssist;

            DirectionColorOverrides overrides = data.DirectionColors;
            for (int i = 0; i < DirectionSlotCount; i++)
            {
                Color? loaded = null;
                if (overrides != null && overrides.TryGetColor((SwipeDirection)i, out Color color))
                {
                    loaded = color;
                }

                changed |= !NullableColorsEqual(CustomColors[i], loaded);
                CustomColors[i] = loaded;
            }

            if (changed)
            {
                OnChanged?.Invoke();
            }
        }

        /// <summary>런타임 상태를 세이브 데이터에 기록한다. 파일 쓰기는 호출자가 <see cref="SaveService.Save"/>로 수행한다.</summary>
        public static void WriteTo(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            data.DirectionDisplayMode = (int)_displayMode;
            data.DirectionArrowAssist = _arrowAssist;

            if (data.DirectionColors == null)
            {
                data.DirectionColors = new DirectionColorOverrides();
            }

            // 지정이 사라진 방향이 세이브에 남지 않도록 전체를 다시 쓴다(항목 4개라 비용이 무의미하다).
            data.DirectionColors.Clear();
            for (int i = 1; i < DirectionSlotCount; i++)
            {
                if (CustomColors[i].HasValue)
                {
                    data.DirectionColors.SetColor((SwipeDirection)i, CustomColors[i].Value);
                }
            }
        }

        /// <summary>
        /// 지정 방향의 최종 표시 색: 유저 커스텀 → 기본 팔레트 순으로 해석한다.
        /// <paramref name="baseColors"/>가 null이면 회색으로 폴백한다(호출자가 팔레트를 넘기는 것이 정상 경로).
        /// </summary>
        public static Color ResolveColor(SwipeDirection direction, DirectionColorSource baseColors)
        {
            if (TryGetCustomColor(direction, out Color custom))
            {
                return custom;
            }

            return baseColors != null ? baseColors(direction) : Color.gray;
        }

        /// <summary>알 수 없는 저장 값(구버전/손상)은 기본 표시 방식으로 폴백한다.</summary>
        private static DirectionDisplayMode ToDisplayMode(int stored)
        {
            switch (stored)
            {
                case (int)DirectionDisplayMode.Glow:
                    return DirectionDisplayMode.Glow;
                case (int)DirectionDisplayMode.Orb:
                    return DirectionDisplayMode.Orb;
                case (int)DirectionDisplayMode.Both:
                    return DirectionDisplayMode.Both;
                default:
                    return DefaultDisplayMode;
            }
        }

        private static bool IsValidIndex(SwipeDirection direction)
        {
            int index = (int)direction;
            return index >= 0 && index < DirectionSlotCount;
        }

        // 색은 RRGGBB 8비트로 저장되므로, 같은 색 판정도 8비트 기준으로 해야 저장 전후가 일치한다.
        private static bool ColorsEqual(Color a, Color b)
        {
            return ColorUtility.ToHtmlStringRGB(a) == ColorUtility.ToHtmlStringRGB(b);
        }

        private static bool NullableColorsEqual(Color? a, Color? b)
        {
            if (!a.HasValue || !b.HasValue)
            {
                return a.HasValue == b.HasValue;
            }

            return ColorsEqual(a.Value, b.Value);
        }
    }
}
