using System;
using Mukseon.Core;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class GameOverHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerHealth _playerHealth;

        [SerializeField]
        private WaveCombatDirector _waveCombatDirector;

        [Header("Settings")]
        [SerializeField]
        private bool _pauseTimeOnGameOver = true;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private bool _isGameOver;

        public bool IsGameOver => _isGameOver;
        public event Action OnGameOver;

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDied += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDied -= HandlePlayerDied;
            }

            // 게임오버로 정지를 건 채 비활성화되면(플레이 중지, 씬 언로드) 정지 원인이 영구히 남는다.
            // 소유자가 사라지는 시점에 스스로 해제해야 다음 런이 정지 상태로 시작되지 않는다(#109).
            if (_isGameOver && _pauseTimeOnGameOver)
            {
                TimeScaleService.SetPause(PauseReason.GameOver, false);
            }
        }

        private void HandlePlayerDied()
        {
            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log("[GameOverHandler] Game Over triggered.");
            }
#endif

            if (_waveCombatDirector != null)
            {
                _waveCombatDirector.StopWaves();
            }

            if (_pauseTimeOnGameOver)
            {
                TimeScaleService.SetPause(PauseReason.GameOver, true);
            }

            OnGameOver?.Invoke();
        }

        /// <summary>
        /// 게임오버 상태를 제자리에서 되돌린다. 현재 재도전 경로(<c>ScreenFlow.ReloadGameplay</c>)는 씬을
        /// 통째로 리로드해 새 인스턴스가 <c>_isGameOver == false</c>로 시작하므로 호출자가 없다.
        /// 씬 리로드 없이 런을 재시작하는 경로가 생기면 그때 쓰인다(PR #110 리뷰 지적).
        /// </summary>
        public void ResetGameOver()
        {
            _isGameOver = false;

            if (_pauseTimeOnGameOver)
            {
                TimeScaleService.SetPause(PauseReason.GameOver, false);
            }
        }
    }
}
