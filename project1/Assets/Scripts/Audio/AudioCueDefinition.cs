using System;
using UnityEngine;

namespace Mukseon.Audio
{
    /// <summary>
    /// 효과음 한 종류의 데이터(#38). 클립·볼륨·피치 범위를 담는다.
    ///
    /// 임시 클립을 실제 클립으로 바꾸는 작업은 <c>AudioLibrary</c> 에셋에서 이 항목의
    /// Clip 슬롯만 교체하면 끝난다 — 코드 변경이 필요 없도록 의도적으로 데이터로 뺐다.
    /// </summary>
    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField]
        private AudioCue _cue = AudioCue.None;

        [SerializeField]
        private AudioClip _clip;

        [SerializeField, Range(0f, 1f)]
        private float _volume = 1f;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("재생마다 이 범위에서 피치를 무작위로 뽑는다. 같은 소리가 반복될 때 기계적으로 들리는 걸 막는다.")]
        private float _pitchMin = 1f;

        [SerializeField, Range(0.1f, 3f)]
        private float _pitchMax = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("같은 큐를 다시 재생하기까지의 최소 간격(초). 적이 한 프레임에 여럿 죽을 때 같은 클립이 겹쳐 찢어지는 걸 막는다.")]
        private float _minRetriggerSeconds = 0.04f;

        public AudioCue Cue => _cue;
        public AudioClip Clip => _clip;
        public float Volume => Mathf.Clamp01(_volume);
        public float MinRetriggerSeconds => Mathf.Max(0f, _minRetriggerSeconds);

        /// <summary>인스펙터에서 min/max가 뒤집혀 입력돼도 안전하도록 정렬해서 읽는다.</summary>
        public float PitchMin => Mathf.Min(_pitchMin, _pitchMax);
        public float PitchMax => Mathf.Max(_pitchMin, _pitchMax);

        public float RandomPitch()
        {
            return UnityEngine.Random.Range(PitchMin, PitchMax);
        }
    }
}
