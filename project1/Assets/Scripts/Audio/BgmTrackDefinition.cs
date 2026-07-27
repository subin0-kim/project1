using System;
using UnityEngine;

namespace Mukseon.Audio
{
    /// <summary>
    /// 배경음악 한 트랙의 데이터(#38). BGM은 항상 루프이므로 루프 여부는 두지 않는다.
    /// </summary>
    [Serializable]
    public sealed class BgmTrackDefinition
    {
        [SerializeField]
        private BgmTrack _track = BgmTrack.None;

        [SerializeField]
        private AudioClip _clip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("트랙별 기준 볼륨. 곡마다 녹음 레벨이 달라 여기서 맞춘다.")]
        private float _volume = 1f;

        public BgmTrack Track => _track;
        public AudioClip Clip => _clip;
        public float Volume => Mathf.Clamp01(_volume);
    }
}
