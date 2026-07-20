using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Dev
{
    /// <summary>
    /// 시연용 치트의 키 매핑과 표시 라벨(#111).
    ///
    /// MonoBehaviour에 의존하지 않는 순수 자료구조로 분리했다. 실행부(<c>DemoCheatController</c>)는
    /// 조건부 컴파일로 출시 빌드에서 통째로 빠지지만, 매핑 자체는 테스트에서 중복/누락을 검증할 수 있어야 한다.
    /// </summary>
    public static class DemoCheatBindings
    {
        /// <summary>키 하나에 대응하는 치트 한 개.</summary>
        public readonly struct Binding
        {
            public readonly KeyCode Key;
            public readonly DemoCheatAction Action;

            /// <summary>화면 오버레이에 표시할 설명.</summary>
            public readonly string Label;

            public Binding(KeyCode key, DemoCheatAction action, string label)
            {
                Key = key;
                Action = action;
                Label = label;
            }

            /// <summary>오버레이 한 줄 표기(예: "F1  무적").</summary>
            public string DisplayText => $"{KeyText}  {Label}";

            /// <summary>KeyCode의 기본 ToString은 "F1"처럼 그대로 쓸 만하므로 그대로 사용한다.</summary>
            public string KeyText => Key.ToString();
        }

        // 기능 키를 쓰는 이유: 시연은 PC 키보드 기준이고, 숫자/문자 키는 향후 다른 디버그 입력과
        // 충돌하기 쉽다. F1~F7은 게임 입력(스와이프/더블탭)과 겹치지 않는다.
        private static readonly Binding[] _bindings =
        {
            new Binding(KeyCode.F1, DemoCheatAction.ToggleInvincible, "무적 토글"),
            new Binding(KeyCode.F2, DemoCheatAction.LevelUp, "즉시 레벨업"),
            new Binding(KeyCode.F3, DemoCheatAction.KillEnemies, "적 일괄 제거"),
            new Binding(KeyCode.F4, DemoCheatAction.ActivateGangshin, "강신 즉시 발동"),
            new Binding(KeyCode.F5, DemoCheatAction.GrantGangshinSlot, "강신 슬롯 지급"),
            new Binding(KeyCode.F6, DemoCheatAction.SkipToBoss, "보스 구간 점프"),
            new Binding(KeyCode.F7, DemoCheatAction.ToggleOverlay, "안내 표시 토글"),
        };

        public static IReadOnlyList<Binding> All => _bindings;

        /// <summary>지정 키에 매핑된 치트를 찾는다. 없으면 <see cref="DemoCheatAction.None"/>.</summary>
        public static DemoCheatAction Resolve(KeyCode key)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].Key == key)
                {
                    return _bindings[i].Action;
                }
            }

            return DemoCheatAction.None;
        }

        /// <summary>지정 치트의 키를 찾는다. 없으면 <see cref="KeyCode.None"/>.</summary>
        public static KeyCode ResolveKey(DemoCheatAction action)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].Action == action)
                {
                    return _bindings[i].Key;
                }
            }

            return KeyCode.None;
        }
    }
}
