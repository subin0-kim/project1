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

        // 외곽선 글로우 셰이더(DirectionOutlineGlow)가 아틀라스 블리딩 방지를 위해 읽는 스프라이트 UV 바운드.
        private const string SpriteRectProperty = "_SpriteRect";

        private MaterialPropertyBlock _propertyBlock;
        private EnemyAttackSequence _attackSequence;
        private int _glowColorId;
        private int _spriteRectId;
        private Sprite _boundsSprite;
        private Vector4 _spriteBounds = new Vector4(0f, 0f, 1f, 1f);

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
            _spriteRectId = Shader.PropertyToID(SpriteRectProperty);
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

            Color color = ResolveColor(_enemyHealth.SwipeDirection);
            _spriteRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_glowColorId, color);
            _propertyBlock.SetVector(_spriteRectId, ResolveSpriteUvBounds());
            _spriteRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// 이 적의 외곽선 글로우와 동일한 팔레트 인스턴스로 지정 방향의 색을 조회한다(#82).
        /// HUD 색 오브가 글로우와 같은 색을 쓰도록 외부(<c>GameplayHudBootstrapper</c>)에서 사용한다.
        /// </summary>
        public Color ResolveColor(SwipeDirection direction)
        {
            return DirectionColorPalette.Resolve(_palette, direction);
        }

        /// <summary>
        /// 현재 스프라이트의 아틀라스 내 UV 바운드(min.xy, max.xy)를 반환한다(#82).
        /// 글로우 셰이더가 8방향 샘플을 이 바운드로 클램핑해, 아틀라스 패킹 시
        /// 이웃 스프라이트의 알파를 침범(bleeding)하는 것을 막는다. 스프라이트가 바뀔 때만 재계산한다.
        /// </summary>
        private Vector4 ResolveSpriteUvBounds()
        {
            Sprite sprite = _spriteRenderer.sprite;
            if (ReferenceEquals(sprite, _boundsSprite))
            {
                return _spriteBounds;
            }

            _boundsSprite = sprite;
            _spriteBounds = ComputeSpriteUvBounds(sprite);
            return _spriteBounds;
        }

        private static Vector4 ComputeSpriteUvBounds(Sprite sprite)
        {
            // 바운드를 모르면 전체 텍스처(0..1)로 폴백 — 셰이더 클램핑이 사실상 비활성화된다.
            if (sprite == null)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            Vector2[] uv = sprite.uv;
            if (uv == null || uv.Length == 0)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            Vector2 min = uv[0];
            Vector2 max = uv[0];
            for (int i = 1; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            return new Vector4(min.x, min.y, max.x, max.y);
        }
    }
}
