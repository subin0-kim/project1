using Mukseon.Audio;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 볼륨 합성 로직 검증(#38).
    ///
    /// 마스터를 내리면 두 채널이 함께 내려가고, 음소거를 풀면 원래 값으로 정확히 돌아와야 한다 —
    /// 이 둘이 깨지면 설정 화면에서 볼륨이 조용히 어긋난다.
    /// </summary>
    public class AudioMixLevelsTests
    {
        [Test]
        public void Defaults_BgmSitsBelowSfx()
        {
            var levels = new AudioMixLevels();

            Assert.That(levels.BgmVolume, Is.LessThan(levels.SfxVolume),
                "타격감이 음악에 묻히지 않도록 BGM 기본값이 더 낮아야 한다.");
        }

        [Test]
        public void Master_ScalesBothChannels()
        {
            var levels = new AudioMixLevels { Master = 0.5f };

            Assert.That(levels.BgmVolume, Is.EqualTo(0.5f * AudioMixLevels.DefaultBgm).Within(1e-5f));
            Assert.That(levels.SfxVolume, Is.EqualTo(0.5f * AudioMixLevels.DefaultSfx).Within(1e-5f));
        }

        [Test]
        public void Channels_AreClampedToUnitRange()
        {
            var levels = new AudioMixLevels { Master = 3f, Bgm = -2f, Sfx = 1.5f };

            Assert.That(levels.Master, Is.EqualTo(1f));
            Assert.That(levels.Bgm, Is.EqualTo(0f));
            Assert.That(levels.Sfx, Is.EqualTo(1f));
        }

        // 음소거는 채널 값을 건드리지 않아야 한다. 0으로 덮어쓰면 해제해도 원래 볼륨을 잃는다.
        [Test]
        public void Mute_SilencesWithoutLosingChannelValues()
        {
            var levels = new AudioMixLevels { Bgm = 0.4f, Sfx = 0.6f, Muted = true };

            Assert.That(levels.BgmVolume, Is.EqualTo(0f));
            Assert.That(levels.SfxVolume, Is.EqualTo(0f));

            levels.Muted = false;

            Assert.That(levels.BgmVolume, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(levels.SfxVolume, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void Reset_RestoresDefaults()
        {
            var levels = new AudioMixLevels { Master = 0.1f, Bgm = 0.2f, Sfx = 0.3f, Muted = true };

            levels.Reset();

            Assert.That(levels.Master, Is.EqualTo(AudioMixLevels.DefaultMaster));
            Assert.That(levels.Bgm, Is.EqualTo(AudioMixLevels.DefaultBgm));
            Assert.That(levels.Sfx, Is.EqualTo(AudioMixLevels.DefaultSfx));
            Assert.That(levels.Muted, Is.False);
        }
    }
}
