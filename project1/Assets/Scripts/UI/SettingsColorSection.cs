using System;
using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 환경설정 화면의 "방향 색상" 구역(#83): 방향 4행 × 색 스와치 8개.
    ///
    /// <see cref="SettingsOverlay"/>에서 분리한 이유는 순전히 크기다 — 스와치 격자 조립과
    /// 선택 상태 갱신이 오버레이 본체만큼 길어져, 한 파일에 두면 300줄 규칙을 넘긴다.
    /// </summary>
    internal sealed class SettingsColorSection
    {
        private const float LabelWidth = 90f;
        private const float CurrentChipDiameter = 30f;
        private const float SwatchDiameter = 38f;

        private readonly struct Row
        {
            public Row(SwipeDirection direction, VisualElement currentChip, Button[] swatches)
            {
                Direction = direction;
                CurrentChip = currentChip;
                Swatches = swatches;
            }

            public SwipeDirection Direction { get; }
            public VisualElement CurrentChip { get; }
            public Button[] Swatches { get; }
        }

        // 팔레트 에셋은 아직 없어 정적 디폴트가 기본 색이다. 에셋이 생기면 여기에 주입하면 된다.
        private readonly DirectionColorPalette _palette;

        // SetCustomColor가 매번 호출할 기본색 조회 콜백. 클릭마다 람다를 새로 만들지 않도록 한 번만 만든다.
        private readonly DirectionColorSource _baseColors;

        private readonly List<Row> _rows = new List<Row>(4);

        // 배정 직후 호출되는 영속화 콜백. 이 구역은 세이브 계층을 몰라야 하므로 오버레이가 주입한다.
        private readonly Action _onAssigned;

        public SettingsColorSection(VisualElement parent, DirectionColorPalette palette, Action onAssigned)
        {
            _palette = palette;
            _onAssigned = onAssigned;
            _baseColors = ResolveBaseColor;

            IReadOnlyList<SettingsScreenContent.DirectionRow> definitions = SettingsScreenContent.DirectionRows;
            for (int i = 0; i < definitions.Count; i++)
            {
                _rows.Add(BuildRow(parent, definitions[i]));
            }

            Refresh();
        }

        /// <summary>현재 매핑에 맞춰 각 행의 색 칩과 선택된 스와치 표시를 갱신한다.</summary>
        public void Refresh()
        {
            IReadOnlyList<SettingsScreenContent.ColorSwatch> swatches = SettingsScreenContent.Swatches;

            for (int r = 0; r < _rows.Count; r++)
            {
                Row row = _rows[r];
                Color current = DirectionColorPalette.Resolve(_palette, row.Direction);
                row.CurrentChip.style.backgroundColor = current;

                for (int s = 0; s < row.Swatches.Length; s++)
                {
                    // 이 스와치가 현재 이 방향에 배정된 색인지 — 흰 테두리로 선택 상태를 보여 준다.
                    bool selected = SameColor(swatches[s].Color, current);
                    ApplySelectionBorder(row.Swatches[s], selected);
                }
            }
        }

        private Row BuildRow(VisualElement parent, SettingsScreenContent.DirectionRow definition)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6f;
            row.style.marginBottom = 6f;
            parent.Add(row);

            var label = new Label(definition.Label);
            label.style.color = ScreenUiFactory.Ink;
            label.style.fontSize = 24;
            label.style.width = LabelWidth;
            row.Add(label);

            var currentChip = new VisualElement();
            currentChip.style.width = CurrentChipDiameter;
            currentChip.style.height = CurrentChipDiameter;
            currentChip.style.marginRight = 22f;
            ScreenUiFactory.SetBorderRadius(currentChip, CurrentChipDiameter * 0.5f);
            row.Add(currentChip);

            IReadOnlyList<SettingsScreenContent.ColorSwatch> swatches = SettingsScreenContent.Swatches;
            var buttons = new Button[swatches.Count];
            for (int i = 0; i < swatches.Count; i++)
            {
                buttons[i] = BuildSwatch(row, definition.Direction, swatches[i]);
            }

            return new Row(definition.Direction, currentChip, buttons);
        }

        private Button BuildSwatch(VisualElement parent, SwipeDirection direction, SettingsScreenContent.ColorSwatch swatch)
        {
            var button = new Button(() => Assign(direction, swatch.Color))
            {
                text = string.Empty,
                tooltip = swatch.Name,
            };

            button.style.width = SwatchDiameter;
            button.style.height = SwatchDiameter;
            button.style.marginLeft = 5f;
            button.style.marginRight = 5f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.backgroundColor = swatch.Color;
            ScreenUiFactory.SetBorderRadius(button, SwatchDiameter * 0.5f);
            parent.Add(button);
            return button;
        }

        // 배정은 설정 서비스가 처리한다(같은 색이면 다른 방향과 맞바꾼다).
        // 화면 갱신은 OnChanged를 구독한 오버레이가 하고, 저장은 주입받은 콜백이 담당한다.
        private void Assign(SwipeDirection direction, Color color)
        {
            DirectionColorSettings.SetCustomColor(direction, color, _baseColors);
            _onAssigned?.Invoke();
        }

        private Color ResolveBaseColor(SwipeDirection direction)
        {
            return DirectionColorPalette.BaseColor(_palette, direction);
        }

        private static void ApplySelectionBorder(VisualElement element, bool selected)
        {
            float width = selected ? 3f : 0f;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;

            Color color = new Color(1f, 1f, 1f, selected ? 0.95f : 0f);
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        // 저장이 RRGGBB 8비트라 비교도 같은 정밀도로 해야 스와치 선택 표시가 저장 전후로 흔들리지 않는다.
        private static bool SameColor(Color a, Color b)
        {
            return ColorUtility.ToHtmlStringRGB(a) == ColorUtility.ToHtmlStringRGB(b);
        }
    }
}
