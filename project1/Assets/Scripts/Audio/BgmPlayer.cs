using UnityEngine;

namespace Mukseon.Audio
{
    /// <summary>
    /// 배경음악 재생과 트랙 간 크로스페이드(#38).
    ///
    /// <see cref="AudioSource"/> 두 개를 번갈아 쓴다 — 하나로는 새 곡을 시작하는 순간 이전 곡이
    /// 끊기므로 페이드가 성립하지 않는다. 진행도는 <c>unscaledDeltaTime</c>으로 갱신해야 한다.
    /// 레벨업·결과 화면에서 <c>Time.timeScale</c>이 0이 되는데 그때도 음악은 이어져야 하기 때문이다.
    /// </summary>
    public sealed class BgmPlayer
    {
        private const float CrossfadeSeconds = 0.8f;

        private readonly AudioSource _sourceA;
        private readonly AudioSource _sourceB;

        // 페이드 인 중(또는 재생 중)인 소스와, 페이드 아웃 중인 직전 소스.
        private AudioSource _active;
        private AudioSource _previous;

        // 트랙 자체의 기준 볼륨(믹스 볼륨과 곱해진다).
        private float _activeTrackVolume;
        private float _previousTrackVolume;

        private float _fadeProgress = 1f;

        public BgmPlayer(Transform parent)
        {
            _sourceA = CreateSource(parent, "BgmSourceA");
            _sourceB = CreateSource(parent, "BgmSourceB");
        }

        public BgmTrack CurrentTrack { get; private set; } = BgmTrack.None;

        public bool IsPlaying => _active != null && _active.isPlaying;

        /// <summary>
        /// 트랙을 전환한다. 이미 같은 트랙이면 아무것도 하지 않는다(같은 곡이 처음부터 다시 시작하면
        /// 씬을 오갈 때마다 음악이 끊긴 것처럼 들린다).
        /// </summary>
        public void Play(BgmTrack track, AudioClip clip, float trackVolume)
        {
            if (track == CurrentTrack)
            {
                return;
            }

            // 직전 페이드가 아직 안 끝났는데 또 바뀌면 소스가 모자란다. 남아 있던 쪽을 즉시 정리한다.
            StopSource(_previous);

            _previous = _active;
            _previousTrackVolume = _activeTrackVolume;

            if (clip != null)
            {
                _active = _previous == _sourceA ? _sourceB : _sourceA;
                _active.clip = clip;
                _active.loop = true;
                _active.volume = 0f;
                _active.Play();
                _activeTrackVolume = Mathf.Clamp01(trackVolume);
            }
            else
            {
                _active = null;
                _activeTrackVolume = 0f;
            }

            CurrentTrack = track;
            _fadeProgress = 0f;
        }

        /// <summary>페이드 아웃하며 정지한다.</summary>
        public void Stop()
        {
            Play(BgmTrack.None, null, 0f);
        }

        /// <summary>즉시 정지(페이드 없음).</summary>
        public void StopImmediate()
        {
            StopSource(_active);
            StopSource(_previous);
            _active = null;
            _previous = null;
            _activeTrackVolume = 0f;
            _previousTrackVolume = 0f;
            CurrentTrack = BgmTrack.None;
            _fadeProgress = 1f;
        }

        /// <param name="unscaledDeltaTime">정지 중에도 페이드가 진행돼야 하므로 스케일되지 않은 델타를 넘긴다.</param>
        /// <param name="mixVolume">마스터×BGM 합성 볼륨.</param>
        public void Update(float unscaledDeltaTime, float mixVolume)
        {
            if (_fadeProgress < 1f)
            {
                _fadeProgress = Mathf.Min(1f, _fadeProgress + unscaledDeltaTime / CrossfadeSeconds);
            }

            if (_active != null)
            {
                _active.volume = mixVolume * _activeTrackVolume * _fadeProgress;
            }

            if (_previous == null)
            {
                return;
            }

            _previous.volume = mixVolume * _previousTrackVolume * (1f - _fadeProgress);
            if (_fadeProgress >= 1f)
            {
                StopSource(_previous);
                _previous = null;
                _previousTrackVolume = 0f;
            }
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        private static AudioSource CreateSource(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
