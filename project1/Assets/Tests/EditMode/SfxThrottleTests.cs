using Mukseon.Audio;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 같은 효과음 연타 억제 검증(#38).
    ///
    /// 핵심은 "같은 큐만 막고 다른 큐는 안 막는다"이다. 이게 깨지면 광역기 한 방에 적이 몰살될 때
    /// 처치음 수십 개가 겹쳐 찢어지거나(억제 실패), 반대로 그 사이 스와이프 소리까지 사라진다(과잉 억제).
    /// </summary>
    public class SfxThrottleTests
    {
        [Test]
        public void FirstPlay_IsAlwaysAllowed()
        {
            var throttle = new SfxThrottle();

            Assert.That(throttle.TryPlay(AudioCue.EnemyDeath, 0f, 0.05f), Is.True);
        }

        [Test]
        public void SameCueWithinInterval_IsBlocked()
        {
            var throttle = new SfxThrottle();
            throttle.TryPlay(AudioCue.EnemyDeath, 10f, 0.05f);

            Assert.That(throttle.TryPlay(AudioCue.EnemyDeath, 10.02f, 0.05f), Is.False);
        }

        [Test]
        public void SameCueAfterInterval_IsAllowed()
        {
            var throttle = new SfxThrottle();
            throttle.TryPlay(AudioCue.EnemyDeath, 10f, 0.05f);

            Assert.That(throttle.TryPlay(AudioCue.EnemyDeath, 10.05f, 0.05f), Is.True);
        }

        // 한 프레임에 적 여럿이 죽어도 처치음은 한 발만, 그리고 그 사이 스와이프는 그대로 울려야 한다.
        [Test]
        public void DifferentCues_DoNotBlockEachOther()
        {
            var throttle = new SfxThrottle();

            Assert.That(throttle.TryPlay(AudioCue.EnemyDeath, 5f, 0.05f), Is.True);
            Assert.That(throttle.TryPlay(AudioCue.EnemyDeath, 5f, 0.05f), Is.False);
            Assert.That(throttle.TryPlay(AudioCue.Swipe, 5f, 0.05f), Is.True);
        }

        [Test]
        public void BlockedPlay_DoesNotExtendTheWindow()
        {
            var throttle = new SfxThrottle();
            throttle.TryPlay(AudioCue.EnemyHit, 0f, 0.10f);

            // 0.09초 시점의 시도는 막히지만, 그 시도가 기준 시각을 밀어서는 안 된다.
            Assert.That(throttle.TryPlay(AudioCue.EnemyHit, 0.09f, 0.10f), Is.False);
            Assert.That(throttle.TryPlay(AudioCue.EnemyHit, 0.10f, 0.10f), Is.True);
        }

        [Test]
        public void ZeroInterval_NeverBlocks()
        {
            var throttle = new SfxThrottle();

            Assert.That(throttle.TryPlay(AudioCue.Swipe, 1f, 0f), Is.True);
            Assert.That(throttle.TryPlay(AudioCue.Swipe, 1f, 0f), Is.True);
        }

        [Test]
        public void NoneCue_IsRejected()
        {
            var throttle = new SfxThrottle();

            Assert.That(throttle.TryPlay(AudioCue.None, 0f, 0f), Is.False);
        }

        // 시계가 되감기면(플레이 재진입 등) 그 큐가 영영 조용해지면 안 된다.
        [Test]
        public void TimeGoingBackwards_DoesNotSilenceTheCue()
        {
            var throttle = new SfxThrottle();
            throttle.TryPlay(AudioCue.LevelUp, 100f, 1f);

            Assert.That(throttle.TryPlay(AudioCue.LevelUp, 0f, 1f), Is.True);
        }

        [Test]
        public void Clear_ForgetsHistory()
        {
            var throttle = new SfxThrottle();
            throttle.TryPlay(AudioCue.EnemyHit, 0f, 1f);

            throttle.Clear();

            Assert.That(throttle.TryPlay(AudioCue.EnemyHit, 0.1f, 1f), Is.True);
        }
    }
}
