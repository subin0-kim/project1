using UnityEngine;

namespace Mukseon.Audio
{
    /// <summary>
    /// 마스터·BGM·SFX 볼륨을 합성하는 순수 로직(#38). MonoBehaviour에 의존하지 않아 단위 테스트가 가능하다.
    ///
    /// 채널 볼륨을 <see cref="AudioSource.volume"/>에 직접 넣지 않고 여기서 한 번 합성하는 이유:
    /// 마스터를 내리면 BGM과 SFX가 함께 내려가야 하는데, 소스마다 각자 곱하면 어디선가 빠뜨린다.
    /// </summary>
    public sealed class AudioMixLevels
    {
        /// <summary>BGM이 SFX보다 낮게 시작하는 이유: 타격감(SFX)이 음악에 묻히면 안 된다.</summary>
        public const float DefaultMaster = 1f;
        public const float DefaultBgm = 0.55f;
        public const float DefaultSfx = 0.85f;

        private float _master = DefaultMaster;
        private float _bgm = DefaultBgm;
        private float _sfx = DefaultSfx;

        public float Master
        {
            get => _master;
            set => _master = Mathf.Clamp01(value);
        }

        public float Bgm
        {
            get => _bgm;
            set => _bgm = Mathf.Clamp01(value);
        }

        public float Sfx
        {
            get => _sfx;
            set => _sfx = Mathf.Clamp01(value);
        }

        /// <summary>전체 음소거. 채널 값을 건드리지 않으므로 해제하면 원래 볼륨으로 정확히 돌아온다.</summary>
        public bool Muted { get; set; }

        public float BgmVolume => Muted ? 0f : _master * _bgm;

        public float SfxVolume => Muted ? 0f : _master * _sfx;

        public void Reset()
        {
            _master = DefaultMaster;
            _bgm = DefaultBgm;
            _sfx = DefaultSfx;
            Muted = false;
        }
    }
}
