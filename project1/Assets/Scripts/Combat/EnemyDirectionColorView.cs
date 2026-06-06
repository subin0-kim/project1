using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 적의 현재 방향 속성을 색상으로 시각화한다(#82, `combat_system.md` §3 — 외곽선 글로우).
    /// SpriteRenderer의 MaterialPropertyBlock에 방향 색상을 글로우 프로퍼티로 적용한다.
    /// 글로우 셰이더/머티리얼이 아직 연결되지 않아도 안전하게 동작한다(프로퍼티 미존재 시 no-op).
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public class EnemyDirectionColorView : MonoBehaviour
    {
        [SerializeField]
        private EnemyHealth _enemyHealth;

        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        private DirectionColorPalette _palette;

        [SerializeField]
        [Tooltip("글로우 색상을 적용할 셰이더 프로퍼티명. 외곽선 글로우 머티리얼과 일치해야 한다.")]
        private string _glowColorProperty = "_GlowColor";

        private MaterialPropertyBlock _propertyBlock;
        private EnemyAttackSequence _attackSequence;
        private int _glowColorId;

        private void Awake()
        {
            if (_enemyHealth == null)
            {
                _enemyHealth = GetComponent<EnemyHealth>();
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _attackSequence = GetComponent<EnemyAttackSequence>();
            _glowColorId = Shader.PropertyToID(_glowColorProperty);
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.OnDirectionChanged += HandleDirectionChanged;
            }

            if (_attackSequence != null)
            {
                _attackSequence.OnAdvanced += HandleSequenceAdvanced;
                _attackSequence.OnSequenceSet += ApplyCurrentColor;
            }

            // 스폰/재사용 직후 현재 방향 색을 즉시 반영한다.
            ApplyCurrentColor();
        }

        private void OnDisable()
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.OnDirectionChanged -= HandleDirectionChanged;
            }

            if (_attackSequence != null)
            {
                _attackSequence.OnAdvanced -= HandleSequenceAdvanced;
                _attackSequence.OnSequenceSet -= ApplyCurrentColor;
            }
        }

        private void HandleDirectionChanged(SwipeDirection direction)
        {
            ApplyCurrentColor();
        }

        private void HandleSequenceAdvanced(int currentIndex)
        {
            ApplyCurrentColor();
        }

        /// <summary>현재 방향(시퀀스 적은 현재 타격 대상 방향) 색을 글로우로 적용한다.</summary>
        public void ApplyCurrentColor()
        {
            if (_spriteRenderer == null || _enemyHealth == null)
            {
                return;
            }

            Color color = DirectionColorPalette.Resolve(_palette, _enemyHealth.SwipeDirection);
            _spriteRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_glowColorId, color);
            _spriteRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
