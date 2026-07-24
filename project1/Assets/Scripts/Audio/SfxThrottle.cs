using System.Collections.Generic;

namespace Mukseon.Audio
{
    /// <summary>
    /// 같은 효과음이 너무 촘촘히 겹치는 것을 막는 순수 로직(#38).
    ///
    /// 필요한 이유: 광역 스킬 한 방에 적 수십 마리가 같은 프레임에 죽으면 처치음이 수십 개 동시에
    /// 시작한다. 위상이 완전히 같은 클립이 겹치면 진폭이 그대로 배가 돼 찢어지고, 재생 채널도
    /// 한 번에 고갈된다. 큐별로 최소 재발동 간격을 두어 첫 한 발만 통과시킨다.
    ///
    /// 시각(<c>now</c>)을 인자로 받아 <see cref="UnityEngine.Time"/>에 의존하지 않으므로 단위 테스트가 가능하다.
    /// </summary>
    public sealed class SfxThrottle
    {
        private readonly Dictionary<AudioCue, float> _lastPlayedAt = new Dictionary<AudioCue, float>();

        /// <summary>
        /// 지금 이 큐를 재생해도 되는지 판정하고, 허용될 때만 마지막 재생 시각을 갱신한다.
        /// </summary>
        /// <param name="now">단조 증가하는 현재 시각(초). 보통 <c>Time.unscaledTime</c>.</param>
        /// <param name="minInterval">이 큐의 최소 재발동 간격(초). 0 이하면 항상 통과한다.</param>
        public bool TryPlay(AudioCue cue, float now, float minInterval)
        {
            if (cue == AudioCue.None)
            {
                return false;
            }

            if (minInterval > 0f && _lastPlayedAt.TryGetValue(cue, out float last))
            {
                // now가 뒤로 간 경우(플레이 재진입 등 시계 리셋)는 막지 않고 통과시킨다 —
                // 막아 버리면 간격이 지날 때까지 그 큐가 영영 조용해진다.
                float elapsed = now - last;
                if (elapsed >= 0f && elapsed < minInterval)
                {
                    return false;
                }
            }

            _lastPlayedAt[cue] = now;
            return true;
        }

        /// <summary>모든 기록을 지운다(씬 전환 등).</summary>
        public void Clear()
        {
            _lastPlayedAt.Clear();
        }
    }
}
