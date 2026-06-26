using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 게임오버 또는 레벨업 스킬 선택으로 게임이 일시정지(Time.timeScale = 0)되는 동안,
    /// unscaledTime 기반으로 계속 동작하는 입력 감지기(스와이프/강신)를 일괄 비활성화한다(#42).
    ///
    /// 입력 감지기(<see cref="SwipeInputDetector"/>, <see cref="GangshinInputDetector"/>)는
    /// Mukseon.Core.Input 계층이라 게임오버/레벨업 같은 게임플레이 상태를 알지 못한다. 이 게이트가
    /// 그 사이를 잇는 어댑터로서, 여러 억제 원인을 <see cref="InputSuppressionState"/>로 합산해 반영한다.
    ///
    /// GameplayHudBootstrapper와 동일하게 RuntimeInitializeOnLoadMethod로 자가 생성되어
    /// 씬 편집 없이 동작하며, 씬이 바뀌면 참조를 다시 해석한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayInputGate : MonoBehaviour
    {
        private const string RootObjectName = "GameplayInputGateRuntime";
        private const float ResolveRetryInterval = 0.5f;

        // 비게임플레이 씬(메뉴/로비/로딩)에는 GameOverHandler·PlayerLevelSystem이 없어 NeedsResolve가
        // 영구히 true가 된다. 그 경우 매 프레임 FindFirstObjectByType를 반복하지 않도록, 일정 횟수
        // (≈ MaxResolveRetries × ResolveRetryInterval 초) 시도 후 폴링을 멈춘다. 씬이 새로 로드되면
        // ResolveAndReset에서 다시 초기화되어 게임플레이 씬에서 정상적으로 재해석된다.
        private const int MaxResolveRetries = 6;

        private static GameplayInputGate _instance;

        private readonly InputSuppressionState _suppression = new InputSuppressionState();

        private SwipeInputDetector _swipeInputDetector;
        private GangshinInputDetector _gangshinInputDetector;
        private GameOverHandler _gameOverHandler;
        private PlayerLevelSystem _playerLevelSystem;

        private float _resolveRetryTimer;
        private int _resolveRetryCount;
        private bool _stopPolling;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGate()
        {
            if (FindExisting() != null)
            {
                return;
            }

            GameObject root = new GameObject(RootObjectName);
            root.AddComponent<GameplayInputGate>();
        }

        private static GameplayInputGate FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<GameplayInputGate>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<GameplayInputGate>();
#endif
        }

        private void Awake()
        {
            // 수동 씬 배치나 예기치 못한 경로로 인스턴스가 중복 생성되면 이벤트 구독이 중복되므로,
            // 기존 인스턴스가 있으면 새로 생긴 쪽을 파괴한다(EnsureGate의 1차 방어에 대한 안전망).
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveAndReset();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeAll();
        }

        private void Update()
        {
            // 모든 참조가 해석됐거나(NeedsResolve == false) 재시도 한도에 도달하면(_stopPolling)
            // 매 프레임 비용을 들이지 않는다.
            if (_stopPolling || !NeedsResolve())
            {
                return;
            }

            _resolveRetryTimer -= Time.unscaledDeltaTime;
            if (_resolveRetryTimer <= 0f)
            {
                _resolveRetryTimer = ResolveRetryInterval;
                ResolveSources();
                ApplyToDetectors();

                _resolveRetryCount++;
                if (_resolveRetryCount >= MaxResolveRetries)
                {
                    _stopPolling = true;
                }
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveAndReset();
        }

        // 씬이 새로 로드되면 이전 씬의 구독을 끊고 억제 상태를 초기화한 뒤 다시 해석한다.
        private void ResolveAndReset()
        {
            UnsubscribeAll();
            _swipeInputDetector = null;
            _gangshinInputDetector = null;
            _gameOverHandler = null;
            _playerLevelSystem = null;
            _suppression.Clear();
            _resolveRetryTimer = 0f;
            _resolveRetryCount = 0;
            _stopPolling = false;
            ResolveSources();
            ApplyToDetectors();
        }

        private void ResolveSources()
        {
            if (_swipeInputDetector == null)
            {
                _swipeInputDetector = FindSceneObject<SwipeInputDetector>();
            }

            if (_gangshinInputDetector == null)
            {
                _gangshinInputDetector = FindSceneObject<GangshinInputDetector>();
            }

            if (_gameOverHandler == null)
            {
                _gameOverHandler = FindSceneObject<GameOverHandler>();
                if (_gameOverHandler != null)
                {
                    _gameOverHandler.OnGameOver += HandleGameOver;
                    // 이미 게임오버 상태인 씬에 뒤늦게 붙은 경우를 반영한다.
                    if (_gameOverHandler.IsGameOver)
                    {
                        _suppression.SetReason(InputSuppressionReason.GameOver, true);
                    }
                }
            }

            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = FindSceneObject<PlayerLevelSystem>();
                if (_playerLevelSystem != null)
                {
                    _playerLevelSystem.OnLevelSelectionOpened += HandleSelectionOpened;
                    _playerLevelSystem.OnLevelSelectionClosed += HandleSelectionClosed;
                    // 이미 선택지가 열린 상태에 뒤늦게 붙은 경우를 반영한다.
                    if (_playerLevelSystem.IsSelectionOpen)
                    {
                        _suppression.SetReason(InputSuppressionReason.LevelUpSelection, true);
                    }
                }
            }
        }

        private bool NeedsResolve()
        {
            return _swipeInputDetector == null ||
                   _gangshinInputDetector == null ||
                   _gameOverHandler == null ||
                   _playerLevelSystem == null;
        }

        private void UnsubscribeAll()
        {
            if (_gameOverHandler != null)
            {
                _gameOverHandler.OnGameOver -= HandleGameOver;
            }

            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnLevelSelectionOpened -= HandleSelectionOpened;
                _playerLevelSystem.OnLevelSelectionClosed -= HandleSelectionClosed;
            }
        }

        private void HandleGameOver()
        {
            if (_suppression.SetReason(InputSuppressionReason.GameOver, true))
            {
                ApplyToDetectors();
            }
        }

        private void HandleSelectionOpened(int level, IReadOnlyList<SkillData> choices)
        {
            if (_suppression.SetReason(InputSuppressionReason.LevelUpSelection, true))
            {
                ApplyToDetectors();
            }
        }

        private void HandleSelectionClosed(int level)
        {
            if (_suppression.SetReason(InputSuppressionReason.LevelUpSelection, false))
            {
                ApplyToDetectors();
            }
        }

        // 합산된 억제 상태를 두 입력 감지기에 동일하게 반영한다.
        private void ApplyToDetectors()
        {
            bool enabled = _suppression.IsInputEnabled;

            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.SetInputEnabled(enabled);
            }

            if (_gangshinInputDetector != null)
            {
                _gangshinInputDetector.SetInputEnabled(enabled);
            }
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}
