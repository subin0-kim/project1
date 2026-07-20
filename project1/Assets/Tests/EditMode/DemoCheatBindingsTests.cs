using System;
using System.Collections.Generic;
using Mukseon.Dev;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>시연용 치트 키 매핑 검증(#111).</summary>
    public class DemoCheatBindingsTests
    {
        [Test]
        public void Bindings_HaveNoDuplicateKeys()
        {
            var seen = new HashSet<KeyCode>();

            foreach (DemoCheatBindings.Binding binding in DemoCheatBindings.All)
            {
                Assert.That(seen.Add(binding.Key), Is.True, $"키 {binding.Key}가 중복 매핑되었습니다.");
            }
        }

        [Test]
        public void Bindings_HaveNoDuplicateActions()
        {
            var seen = new HashSet<DemoCheatAction>();

            foreach (DemoCheatBindings.Binding binding in DemoCheatBindings.All)
            {
                Assert.That(seen.Add(binding.Action), Is.True, $"치트 {binding.Action}가 중복 매핑되었습니다.");
            }
        }

        // None은 '매핑 없음'을 뜻하는 센티널이므로 바인딩에 들어가면 Resolve 결과와 구분할 수 없게 된다.
        [Test]
        public void Bindings_DoNotContainNoneAction()
        {
            foreach (DemoCheatBindings.Binding binding in DemoCheatBindings.All)
            {
                Assert.That(binding.Action, Is.Not.EqualTo(DemoCheatAction.None));
            }
        }

        [Test]
        public void Bindings_CoverEveryDeclaredAction()
        {
            foreach (DemoCheatAction action in Enum.GetValues(typeof(DemoCheatAction)))
            {
                if (action == DemoCheatAction.None)
                {
                    continue;
                }

                Assert.That(
                    DemoCheatBindings.ResolveKey(action),
                    Is.Not.EqualTo(KeyCode.None),
                    $"치트 {action}에 키가 매핑되지 않았습니다.");
            }
        }

        [Test]
        public void Bindings_HaveLabels()
        {
            foreach (DemoCheatBindings.Binding binding in DemoCheatBindings.All)
            {
                Assert.That(string.IsNullOrWhiteSpace(binding.Label), Is.False, $"{binding.Action}에 라벨이 없습니다.");
            }
        }

        [Test]
        public void Resolve_ReturnsMappedAction()
        {
            KeyCode key = DemoCheatBindings.ResolveKey(DemoCheatAction.SkipToBoss);

            Assert.That(DemoCheatBindings.Resolve(key), Is.EqualTo(DemoCheatAction.SkipToBoss));
        }

        [Test]
        public void Resolve_UnmappedKey_ReturnsNone()
        {
            Assert.That(DemoCheatBindings.Resolve(KeyCode.Escape), Is.EqualTo(DemoCheatAction.None));
        }

        [Test]
        public void DisplayText_ContainsKeyAndLabel()
        {
            DemoCheatBindings.Binding binding = DemoCheatBindings.All[0];

            Assert.That(binding.DisplayText, Does.Contain(binding.KeyText));
            Assert.That(binding.DisplayText, Does.Contain(binding.Label));
        }
    }
}
