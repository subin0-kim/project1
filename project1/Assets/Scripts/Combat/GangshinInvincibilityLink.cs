using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class GangshinInvincibilityLink : MonoBehaviour
    {
        [SerializeField]
        private GangshinController _gangshinController;

        [SerializeField]
        private PlayerHealth _playerHealth;

        private void Awake()
        {
            if (_gangshinController == null)
            {
                _gangshinController = GetComponent<GangshinController>();
            }

            if (_playerHealth == null)
            {
                _playerHealth = GetComponent<PlayerHealth>();
            }
        }

        private void OnEnable()
        {
            if (_gangshinController != null)
            {
                _gangshinController.OnStateChanged += HandleGangshinStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_gangshinController != null)
            {
                _gangshinController.OnStateChanged -= HandleGangshinStateChanged;
            }

            if (_playerHealth != null)
            {
                _playerHealth.SetInvincible(false);
            }
        }

        private void HandleGangshinStateChanged(GangshinState newState)
        {
            if (_playerHealth == null)
            {
                return;
            }

            _playerHealth.SetInvincible(newState == GangshinState.Active);
        }
    }
}
