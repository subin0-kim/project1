using System;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerSwipeAttackController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private SwipeInputDetector _swipeInputDetector;

        [Header("Animator Parameters")]
        [SerializeField]
        private string _upAttackTrigger = "AttackUp";
        [SerializeField]
        private string _downAttackTrigger = "AttackDown";
        [SerializeField]
        private string _leftAttackTrigger = "AttackLeft";
        [SerializeField]
        private string _rightAttackTrigger = "AttackRight";

        public event Action<SwipeDirection> OnAttackExecuted;

        private Animator _animator;
        private int _upAttackTriggerHash;
        private int _downAttackTriggerHash;
        private int _leftAttackTriggerHash;
        private int _rightAttackTriggerHash;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }

            if (_swipeInputDetector == null)
            {
                _swipeInputDetector = FindSwipeDetectorInScene();
            }

            BuildAnimatorTriggerHashes();

            if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[PlayerSwipeAttackController] AnimatorController is not assigned. Attack triggers will not play clips.");
            }
        }

        private void OnEnable()
        {
            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.OnSwipeDetected += HandleSwipeDetected;
            }
            else
            {
                Debug.LogWarning("[PlayerSwipeAttackController] SwipeInputDetector reference is missing.");
            }
        }

        private void OnDisable()
        {
            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.OnSwipeDetected -= HandleSwipeDetected;
            }
        }

        private void HandleSwipeDetected(SwipeDirection direction)
        {
            ExecuteAttack(direction);
            TriggerAttackAnimation(direction);
        }

        private void ExecuteAttack(SwipeDirection direction)
        {
            OnAttackExecuted?.Invoke(direction);

#if UNITY_EDITOR
            Debug.Log($"[PlayerSwipeAttackController] Attack executed by swipe: {direction}");
#endif
        }

        private void TriggerAttackAnimation(SwipeDirection direction)
        {
            if (_animator == null)
            {
                return;
            }

            switch (direction)
            {
                case SwipeDirection.Up:
                    ResetAllAttackTriggers();
                    _animator.SetTrigger(_upAttackTriggerHash);
                    break;
                case SwipeDirection.Down:
                    ResetAllAttackTriggers();
                    _animator.SetTrigger(_downAttackTriggerHash);
                    break;
                case SwipeDirection.Left:
                    ResetAllAttackTriggers();
                    _animator.SetTrigger(_leftAttackTriggerHash);
                    break;
                case SwipeDirection.Right:
                    ResetAllAttackTriggers();
                    _animator.SetTrigger(_rightAttackTriggerHash);
                    break;
            }
        }

        private void ResetAllAttackTriggers()
        {
            _animator.ResetTrigger(_upAttackTriggerHash);
            _animator.ResetTrigger(_downAttackTriggerHash);
            _animator.ResetTrigger(_leftAttackTriggerHash);
            _animator.ResetTrigger(_rightAttackTriggerHash);
        }

        private void BuildAnimatorTriggerHashes()
        {
            _upAttackTriggerHash = Animator.StringToHash(_upAttackTrigger);
            _downAttackTriggerHash = Animator.StringToHash(_downAttackTrigger);
            _leftAttackTriggerHash = Animator.StringToHash(_leftAttackTrigger);
            _rightAttackTriggerHash = Animator.StringToHash(_rightAttackTrigger);
        }

        private void OnValidate()
        {
            BuildAnimatorTriggerHashes();
        }

        private static SwipeInputDetector FindSwipeDetectorInScene()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<SwipeInputDetector>();
#else
            return FindObjectOfType<SwipeInputDetector>();
#endif
        }
    }
}
