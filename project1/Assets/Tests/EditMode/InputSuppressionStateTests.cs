using Mukseon.Core.Input;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    public class InputSuppressionStateTests
    {
        [Test]
        public void Default_InputIsEnabled_WithNoReasons()
        {
            var state = new InputSuppressionState();

            Assert.That(state.IsInputEnabled, Is.True);
            Assert.That(state.ActiveReasons, Is.EqualTo(InputSuppressionReason.None));
        }

        [Test]
        public void SetReason_GameOver_DisablesInput_AndReportsChange()
        {
            var state = new InputSuppressionState();

            bool changed = state.SetReason(InputSuppressionReason.GameOver, true);

            Assert.That(changed, Is.True);
            Assert.That(state.IsInputEnabled, Is.False);
        }

        [Test]
        public void SetReason_LevelUpSelection_DisablesInput()
        {
            var state = new InputSuppressionState();

            state.SetReason(InputSuppressionReason.LevelUpSelection, true);

            Assert.That(state.IsInputEnabled, Is.False);
        }

        [Test]
        public void SetReason_SameReasonTwice_ReportsNoChangeOnSecondCall()
        {
            var state = new InputSuppressionState();

            state.SetReason(InputSuppressionReason.GameOver, true);
            bool changedAgain = state.SetReason(InputSuppressionReason.GameOver, true);

            Assert.That(changedAgain, Is.False);
            Assert.That(state.IsInputEnabled, Is.False);
        }

        [Test]
        public void ClearingOneOfTwoReasons_KeepsInputDisabled()
        {
            var state = new InputSuppressionState();
            state.SetReason(InputSuppressionReason.GameOver, true);
            state.SetReason(InputSuppressionReason.LevelUpSelection, true);

            // 레벨업 선택만 닫혀도 게임오버가 남아 있으므로 입력은 여전히 막혀야 한다.
            bool changed = state.SetReason(InputSuppressionReason.LevelUpSelection, false);

            Assert.That(changed, Is.False);
            Assert.That(state.IsInputEnabled, Is.False);
        }

        [Test]
        public void ClearingLastReason_ReEnablesInput_AndReportsChange()
        {
            var state = new InputSuppressionState();
            state.SetReason(InputSuppressionReason.LevelUpSelection, true);

            bool changed = state.SetReason(InputSuppressionReason.LevelUpSelection, false);

            Assert.That(changed, Is.True);
            Assert.That(state.IsInputEnabled, Is.True);
        }

        [Test]
        public void SetReason_None_IsNoOp()
        {
            var state = new InputSuppressionState();

            bool changed = state.SetReason(InputSuppressionReason.None, true);

            Assert.That(changed, Is.False);
            Assert.That(state.IsInputEnabled, Is.True);
        }

        [Test]
        public void Clear_RemovesAllReasons_AndReEnablesInput()
        {
            var state = new InputSuppressionState();
            state.SetReason(InputSuppressionReason.GameOver, true);
            state.SetReason(InputSuppressionReason.LevelUpSelection, true);

            bool changed = state.Clear();

            Assert.That(changed, Is.True);
            Assert.That(state.IsInputEnabled, Is.True);
            Assert.That(state.ActiveReasons, Is.EqualTo(InputSuppressionReason.None));
        }
    }
}
