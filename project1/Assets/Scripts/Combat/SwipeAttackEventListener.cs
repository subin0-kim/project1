using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    public class SwipeAttackEventListener : MonoBehaviour
    {
        [SerializeField]
        private PlayerSwipeAttackController _playerSwipeAttackController;

        private void Awake()
        {
            if (_playerSwipeAttackController == null)
            {
                _playerSwipeAttackController = GetComponent<PlayerSwipeAttackController>();
            }
        }

        private void OnEnable()
        {
            if (_playerSwipeAttackController != null)
            {
                _playerSwipeAttackController.OnAttackExecuted += HandleAttackExecuted;
            }
        }

        private void OnDisable()
        {
            if (_playerSwipeAttackController != null)
            {
                _playerSwipeAttackController.OnAttackExecuted -= HandleAttackExecuted;
            }
        }

        private void HandleAttackExecuted(SwipeDirection direction)
        {
            Debug.Log($"[SwipeAttackEventListener] Apply damage/effects for direction: {direction}");
        }
    }
}
