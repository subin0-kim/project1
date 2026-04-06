using System;
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
                Time.timeScale = 0f;
            }

            OnGameOver?.Invoke();
        }

        public void ResetGameOver()
        {
            _isGameOver = false;

            if (_pauseTimeOnGameOver)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
