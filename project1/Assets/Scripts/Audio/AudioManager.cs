using Mukseon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mukseon.Audio
{
    /// <summary>
    /// 오디오 재생의 단일 진입점(#38). CLAUDE.md가 싱글턴을 허용하는 몇 안 되는 대상 중 하나다.
    ///
    /// 씬에 배치하지 않고 스스로 떠서 <c>DontDestroyOnLoad</c>로 살아남는다 — 이 프로젝트에서
    /// 확립된 패턴이며(<c>ScreenFlow</c>, <c>GameplayInputGate</c>), 세 개 씬 모두에 오디오 오브젝트를
    /// 심어 두지 않아도 되고 씬 전환 중에도 음악이 끊기지 않는다.
    ///
    /// 게임 이벤트와의 연결은 여기 두지 않고 <see cref="GameAudioBinder"/>가 담당한다 —
    /// 재생 장치가 전투·성장 시스템을 알게 되면 오디오만 따로 테스트하거나 교체할 수 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string RootObjectName = "AudioRuntime";

        private static AudioManager _instance;
        private static readonly AudioMixLevels MixLevels = new AudioMixLevels();

        private readonly SfxThrottle _throttle = new SfxThrottle();

        private AudioLibrary _library;
        private SfxPlayer _sfxPlayer;
        private BgmPlayer _bgmPlayer;
        private AudioListener _ownListener;

        /// <summary>볼륨 설정. 인스턴스가 아직 없어도 읽고 쓸 수 있다(설정 화면이 순서를 신경 쓰지 않도록).</summary>
        public static AudioMixLevels Levels => MixLevels;

        public static BgmTrack CurrentBgm => _instance != null ? _instance._bgmPlayer.CurrentTrack : BgmTrack.None;

        /// <summary>라이브러리 에셋이 실제로 로드됐는지. 없으면 전체가 조용히 무음이 된다.</summary>
        public static bool HasLibrary => _instance != null && _instance._library != null;

        // 도메인 리로드를 끄고 플레이하는 경우를 대비해 명시적으로 초기화한다(TimeScaleService와 같은 이유).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            _instance = null;
            MixLevels.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureManager()
        {
            if (SceneObjectFinder.Find<AudioManager>() != null)
            {
                return;
            }

            var root = new GameObject(RootObjectName);
            root.AddComponent<AudioManager>();
        }

        /// <summary>효과음을 재생한다. 큐가 미등록이거나 클립이 비어 있으면 조용히 무시한다.</summary>
        public static void PlaySfx(AudioCue cue)
        {
            if (_instance != null)
            {
                _instance.PlayCueInternal(cue);
            }
        }

        /// <summary>배경음악을 전환한다(크로스페이드). 같은 트랙이면 아무 일도 일어나지 않는다.</summary>
        public static void PlayBgm(BgmTrack track)
        {
            if (_instance != null)
            {
                _instance.PlayTrackInternal(track);
            }
        }

        public static void StopBgm()
        {
            if (_instance != null)
            {
                _instance._bgmPlayer.Stop();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _library = AudioLibrary.Load();
            if (_library == null)
            {
                Debug.LogWarning(
                    $"[AudioManager] Resources/{AudioLibrary.ResourcePath} 에셋을 찾지 못해 오디오가 비활성화됩니다.");
            }

            _sfxPlayer = new SfxPlayer(transform);
            _bgmPlayer = new BgmPlayer(transform);

            // 타이틀·캐릭터선택 씬에는 카메라만 있고 AudioListener가 없다. 리스너가 없으면 아무 소리도
            // 나지 않으므로 매니저가 예비 리스너를 들고 다니다가, 씬이 제 것을 갖고 있으면 비켜 준다.
            _ownListener = gameObject.AddComponent<AudioListener>();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveListener();
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _sfxPlayer?.StopAll();
            _bgmPlayer?.StopImmediate();
            _instance = null;
        }

        private void Update()
        {
            _sfxPlayer.Update();

            // 정지(timeScale 0) 중에도 음악과 페이드는 계속돼야 하므로 스케일되지 않은 델타를 쓴다.
            _bgmPlayer.Update(Time.unscaledDeltaTime, MixLevels.BgmVolume);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 씬이 바뀌면 이전 씬 기준의 연타 억제 기록은 의미가 없다.
            _throttle.Clear();
            ResolveListener();
        }

        private void PlayCueInternal(AudioCue cue)
        {
            if (_library == null)
            {
                return;
            }

            AudioCueDefinition definition = _library.FindCue(cue);
            if (definition == null || definition.Clip == null)
            {
                return;
            }

            // 정지 중에도 UI 소리는 나야 하므로 unscaledTime을 기준으로 간격을 잰다.
            if (!_throttle.TryPlay(cue, Time.unscaledTime, definition.MinRetriggerSeconds))
            {
                return;
            }

            _sfxPlayer.Play(definition.Clip, definition.Volume * MixLevels.SfxVolume, definition.RandomPitch());
        }

        private void PlayTrackInternal(BgmTrack track)
        {
            if (track == BgmTrack.None)
            {
                _bgmPlayer.Stop();
                return;
            }

            BgmTrackDefinition definition = _library != null ? _library.FindTrack(track) : null;
            if (definition == null || definition.Clip == null)
            {
                return;
            }

            _bgmPlayer.Play(track, definition.Clip, definition.Volume);
        }

        // 씬이 제 리스너를 갖고 있으면 우리 것을 끈다. 리스너가 둘이면 Unity가 경고를 뱉고 한쪽만 동작한다.
        private void ResolveListener()
        {
            if (_ownListener == null)
            {
                return;
            }

            _ownListener.enabled = !HasOtherEnabledListener();
        }

        private bool HasOtherEnabledListener()
        {
            AudioListener[] listeners =
                FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i] != _ownListener && listeners[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
