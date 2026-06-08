using System;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Stats;
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

        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        // 공격 쿨타임 하한(#40, stat_balance_mvp.md — 쿨타임이 지나치게 낮아지지 않도록).
        private const float MinAttackCooldown = 0.1f;

        // 마지막 공격 시각(unscaledTime). 강신 슬로모/일시정지에 영향받지 않도록 실시간 기준으로 둔다.
        private float _lastAttackTime = -999f;

        [Header("Animator Parameters")]
        [SerializeField]
        private string _upAttackTrigger = "AttackUp";
        [SerializeField]
        private string _downAttackTrigger = "AttackDown";
        [SerializeField]
        private string _leftAttackTrigger = "AttackLeft";
        [SerializeField]
        private string _rightAttackTrigger = "AttackRight";

        /// <summary>공격 실행 시 발생. 방향과 스와이프 끝점(스크린 좌표)을 전달합니다.</summary>
        public event Action<SwipeDirection, Vector2> OnAttackExecuted;

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

            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
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

        private void HandleSwipeDetected(SwipeDirection direction, Vector2 endScreenPosition)
        {
            // 공격 쿨타임(#40): 쿨타임 내 재입력은 공격·애니메이션·혼불 당기기 전부 무시한다.
            if (!CanAttackNow())
            {
                return;
            }

            _lastAttackTime = Time.unscaledTime;
            ExecuteAttack(direction, endScreenPosition);
            TriggerAttackAnimation(direction);
        }

        private bool CanAttackNow()
        {
            float cooldown = _playerStatSystem != null ? _playerStatSystem.GetValue(StatType.AttackCooldown) : 0f;
            if (cooldown <= 0f)
            {
                return true;
            }

            cooldown = Mathf.Max(MinAttackCooldown, cooldown);
            return Time.unscaledTime - _lastAttackTime >= cooldown;
        }

        private void ExecuteAttack(SwipeDirection direction, Vector2 endScreenPosition)
        {
            OnAttackExecuted?.Invoke(direction, endScreenPosition);

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
