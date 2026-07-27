using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Mukseon.Audio
{
    /// <summary>
    /// 효과음 재생용 <see cref="AudioSource"/> 풀(#38).
    ///
    /// 재생마다 소스를 만들고 버리면 초당 수십 번의 <c>Instantiate</c>/<c>Destroy</c>가 발생하므로
    /// 프로젝트 규칙대로 <see cref="ObjectPool{T}"/>를 쓴다. 클립이 끝났는지 알려주는 콜백이 없어서
    /// 매 프레임 <see cref="AudioSource.isPlaying"/>을 훑어 반납한다.
    /// </summary>
    public sealed class SfxPlayer
    {
        private const int InitialCapacity = 8;
        private const int PoolMaxSize = 32;

        /// <summary>동시 재생 상한. 넘치면 새 재생을 버린다 — 어차피 겹쳐서 들리지도 않고 채널만 먹는다.</summary>
        private const int MaxVoices = 24;

        private readonly Transform _parent;
        private readonly ObjectPool<AudioSource> _pool;
        private readonly List<AudioSource> _active = new List<AudioSource>(MaxVoices);

        private int _createdCount;

        public SfxPlayer(Transform parent)
        {
            _parent = parent;
            _pool = new ObjectPool<AudioSource>(
                createFunc: CreateSource,
                actionOnGet: source => source.gameObject.SetActive(true),
                actionOnRelease: source =>
                {
                    source.Stop();
                    source.clip = null;
                    source.gameObject.SetActive(false);
                },
                actionOnDestroy: source =>
                {
                    if (source != null)
                    {
                        Object.Destroy(source.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: InitialCapacity,
                maxSize: PoolMaxSize);
        }

        public int ActiveVoiceCount => _active.Count;

        /// <summary>클립을 즉시 재생한다. 클립이 없거나 채널이 가득 차면 조용히 무시한다.</summary>
        public void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || volume <= 0f || _active.Count >= MaxVoices)
            {
                return;
            }

            AudioSource source = _pool.Get();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            source.Play();
            _active.Add(source);
        }

        /// <summary>재생이 끝난 소스를 풀에 돌려준다. 매 프레임 호출한다.</summary>
        public void Update()
        {
            // 역순 순회: 제거해도 남은 인덱스가 밀리지 않는다.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                AudioSource source = _active[i];
                if (source == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                if (!source.isPlaying)
                {
                    _active.RemoveAt(i);
                    _pool.Release(source);
                }
            }
        }

        public void StopAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                AudioSource source = _active[i];
                if (source != null)
                {
                    _pool.Release(source);
                }
            }

            _active.Clear();
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject($"SfxVoice_{_createdCount++}");
            go.transform.SetParent(_parent, false);
            go.SetActive(false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            // 플레이어가 화면 중앙에 고정된 2D 게임이라 거리 감쇠·패닝이 필요 없다.
            source.spatialBlend = 0f;
            return source;
        }
    }
}
