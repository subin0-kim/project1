using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Audio
{
    /// <summary>
    /// 게임의 모든 오디오 데이터를 한 곳에 모은 ScriptableObject(#38).
    ///
    /// 큐마다 에셋을 따로 두지 않고 라이브러리 하나로 모은 이유: 지금 배선된 클립은 전부 임시라서
    /// 나중에 실제 클립으로 통째로 갈아끼워야 한다. 에셋이 흩어져 있으면 교체 때 빠뜨리기 쉽다.
    ///
    /// <see cref="AudioManager"/>가 씬 배치 없이 스스로 뜨므로 인스펙터 참조를 받을 수 없어
    /// <c>Resources</c>에서 이름으로 읽는다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Mukseon/Audio/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        /// <summary>Resources 기준 경로(확장자 없음). 이 경로가 바뀌면 오디오가 통째로 조용해진다.</summary>
        public const string ResourcePath = "Audio/AudioLibrary";

        [SerializeField]
        private List<AudioCueDefinition> _cues = new List<AudioCueDefinition>();

        [SerializeField]
        private List<BgmTrackDefinition> _tracks = new List<BgmTrackDefinition>();

        // 조회는 매 타격마다 일어나므로 리스트 선형 탐색을 매번 하지 않고 첫 조회 때 사전을 만든다.
        [NonSerialized] private Dictionary<AudioCue, AudioCueDefinition> _cueLookup;
        [NonSerialized] private Dictionary<BgmTrack, BgmTrackDefinition> _trackLookup;

        public IReadOnlyList<AudioCueDefinition> Cues => _cues;
        public IReadOnlyList<BgmTrackDefinition> Tracks => _tracks;

        /// <summary>Resources에서 라이브러리를 읽는다. 없으면 null(오디오만 비활성, 게임은 계속 동작).</summary>
        public static AudioLibrary Load()
        {
            return Resources.Load<AudioLibrary>(ResourcePath);
        }

        /// <summary>큐 정의를 찾는다. 미등록이면 null.</summary>
        public AudioCueDefinition FindCue(AudioCue cue)
        {
            if (cue == AudioCue.None)
            {
                return null;
            }

            EnsureCueLookup();
            return _cueLookup.TryGetValue(cue, out AudioCueDefinition definition) ? definition : null;
        }

        /// <summary>트랙 정의를 찾는다. 미등록이면 null.</summary>
        public BgmTrackDefinition FindTrack(BgmTrack track)
        {
            if (track == BgmTrack.None)
            {
                return null;
            }

            EnsureTrackLookup();
            return _trackLookup.TryGetValue(track, out BgmTrackDefinition definition) ? definition : null;
        }

        /// <summary>클립까지 실제로 배선돼 있는지. 정의만 있고 클립이 비어 있으면 false.</summary>
        public bool HasClip(AudioCue cue)
        {
            AudioCueDefinition definition = FindCue(cue);
            return definition != null && definition.Clip != null;
        }

        /// <summary>클립까지 실제로 배선돼 있는지. 정의만 있고 클립이 비어 있으면 false.</summary>
        public bool HasClip(BgmTrack track)
        {
            BgmTrackDefinition definition = FindTrack(track);
            return definition != null && definition.Clip != null;
        }

        /// <summary>에셋을 편집한 뒤 캐시된 사전을 버린다(에디터 툴에서 배선 직후 조회할 때 필요).</summary>
        public void InvalidateLookup()
        {
            _cueLookup = null;
            _trackLookup = null;
        }

        private void OnValidate()
        {
            InvalidateLookup();
        }

        // 같은 큐가 두 번 등록되면 먼저 온 것을 쓴다 — 조용히 덮어써서 어느 쪽이 이겼는지 모르게 되는 것보다 낫다.
        private void EnsureCueLookup()
        {
            if (_cueLookup != null)
            {
                return;
            }

            _cueLookup = new Dictionary<AudioCue, AudioCueDefinition>(_cues.Count);
            for (int i = 0; i < _cues.Count; i++)
            {
                AudioCueDefinition definition = _cues[i];
                if (definition == null || definition.Cue == AudioCue.None)
                {
                    continue;
                }

                if (!_cueLookup.ContainsKey(definition.Cue))
                {
                    _cueLookup.Add(definition.Cue, definition);
                }
            }
        }

        private void EnsureTrackLookup()
        {
            if (_trackLookup != null)
            {
                return;
            }

            _trackLookup = new Dictionary<BgmTrack, BgmTrackDefinition>(_tracks.Count);
            for (int i = 0; i < _tracks.Count; i++)
            {
                BgmTrackDefinition definition = _tracks[i];
                if (definition == null || definition.Track == BgmTrack.None)
                {
                    continue;
                }

                if (!_trackLookup.ContainsKey(definition.Track))
                {
                    _trackLookup.Add(definition.Track, definition);
                }
            }
        }
    }
}
