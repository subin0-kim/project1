using Mukseon.Gameplay.Progression;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    public class LevelProgressionModelTests
    {
        [Test]
        public void AddExperience_LevelsUp_WhenThresholdReached()
        {
            var model = new LevelProgressionModel(10f, 1.5f);

            bool leveled = model.AddExperience(10f);

            Assert.That(leveled, Is.True);
            Assert.That(model.Level, Is.EqualTo(2));
            Assert.That(model.CurrentExperience, Is.EqualTo(0f));
            Assert.That(model.PendingLevelUps, Is.EqualTo(1));
        }

        [Test]
        public void AddExperience_CarriesRemainder_AcrossLevelUp()
        {
            var model = new LevelProgressionModel(10f, 2f);

            model.AddExperience(15f);

            Assert.That(model.Level, Is.EqualTo(2));
            Assert.That(model.CurrentExperience, Is.EqualTo(5f));
            Assert.That(model.GetCurrentThreshold(), Is.EqualTo(20f));
        }

        [Test]
        public void TryConsumePendingLevelUp_DecreasesPendingCount()
        {
            var model = new LevelProgressionModel(5f, 1f);
            model.AddExperience(10f);

            bool first = model.TryConsumePendingLevelUp();
            bool second = model.TryConsumePendingLevelUp();
            bool third = model.TryConsumePendingLevelUp();

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(third, Is.False);
            Assert.That(model.PendingLevelUps, Is.EqualTo(0));
        }
    }
}
