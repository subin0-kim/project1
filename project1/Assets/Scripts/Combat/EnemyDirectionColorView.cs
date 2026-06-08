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

        [Header("동적 변환 피드백 (#68)")]
        [SerializeField, Min(0f)]
        [Tooltip("변환 임박 시 글로우 깜빡임 속도.")]
        private float _imminentBlinkSpeed = 9f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("변환 임박 깜빡임이 흰색으로 섞이는 최대 비율.")]
        private float _imminentBlinkAmount = 0.7f;

        [SerializeField, Min(0.01f)]
        [Tooltip("방향 변환 순간 흰색 플래시 지속 시간(초).")]
        private float _convertFlashDuration = 0.25f;

        // 외곽선 글로우 셰이더(DirectionOutlineGlow)가 아틀라스 블리딩 방지를 위해 읽는 스프라이트 UV 바운드.
        private const string SpriteRectProperty = "_SpriteRect";

        private MaterialPropertyBlock _propertyBlock;
        private EnemyAttackSequence _attackSequence;
        private int _glowColorId;
        private int _spriteRectId;
        private Sprite _boundsSprite;
        private Vector4 _spriteBounds = new Vector4(0f, 0f, 1f, 1f);

        private EnemyDirectionConverter _converter;
        private float _imminence;   // 0~1, 변환 임박 강도
        private float _flashTimer;  // 변환 순간 플래시 잔여 시간
        private bool _pulseActive;  // 현재 펄스/플래시로 글로우 색을 덮어쓰는 중인지

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
            _converter = GetComponent<EnemyDirectionConverter>();
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

            if (_converter != null)
            {
                _converter.OnHitCountChanged += HandleHitCountChanged;
                _converter.OnConverted += HandleConverted;
            }

            // 풀 재사용 직후 변환 피드백 상태를 초기화한다.
            _imminence = 0f;
            _flashTimer = 0f;
            _pulseActive = false;

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

            if (_converter != null)
            {
                _converter.OnHitCountChanged -= HandleHitCountChanged;
                _converter.OnConverted -= HandleConverted;
            }
        }

        // 이벤트 시그니처상 인자를 받지만 현재 방향은 ApplyCurrentColor가 직접 조회하므로 사용하지 않는다.
        private void HandleDirectionChanged(SwipeDirection _)
        {
            ApplyCurrentColor();
        }

        private void HandleSequenceAdvanced(int _)
        {
            ApplyCurrentColor();
        }

        // 변환 임박 강도 갱신(#68). 0이 되면 평상 색으로 복귀한다. 카운트 값은 피드백에 쓰지 않으므로 무시한다.
        private void HandleHitCountChanged(int _, float intensity)
        {
            _imminence = Mathf.Clamp01(intensity);
        }

        // 방향 변환 순간(#68): 흰색 플래시를 시작한다. 색 전환 자체는 OnDirectionChanged가 처리한다.
        // from/to는 현재 미사용이나, 향후 변환 연출(먹물 번짐 파티클 등)을 방향별로 분기하기 위해 시그니처를 유지한다.
        private void HandleConverted(SwipeDirection from, SwipeDirection to)
        {
            _flashTimer = _convertFlashDuration;
        }

        private void Update()
        {
            // 애니메이션 등으로 SpriteRenderer의 스프라이트가 방향 이벤트와 무관하게 교체되면
            // 셰이더에 주입된 UV 바운드가 낡게 된다(아틀라스 적의 프레임 간 바운드 차이).
            // 스프라이트 변경을 감지해 다시 적용한다(#82). 변경이 없으면 캐시 비교만 수행한다.
            if (_spriteRenderer != null && !ReferenceEquals(_spriteRenderer.sprite, _boundsSprite))
            {
                ApplyCurrentColor();
            }

            UpdateConversionFeedback();
        }

        /// <summary>
        /// 변환 임박 깜빡임 + 변환 순간 플래시를 글로우 색에 반영한다(#68).
        /// 임박/플래시가 없으면 평상 색으로 1회 복귀 후 매 프레임 쓰기를 멈춘다.
        /// </summary>
        private void UpdateConversionFeedback()
        {
            if (_spriteRenderer == null || _enemyHealth == null)
            {
                return;
            }

            bool flashing = _flashTimer > 0f;
            if (flashing)
            {
                _flashTimer -= Time.deltaTime;
            }

            bool imminent = _imminence > 0f;
            if (!imminent && !flashing)
            {
                if (_pulseActive)
                {
                    // 펄스 종료 → 평상 색으로 복귀(이후 이벤트 발생 전까지 매 프레임 쓰기 없음).
                    _pulseActive = false;
                    ApplyCurrentColor();
                }

                return;
            }

            _pulseActive = true;

            float blink = 0f;
            if (imminent)
            {
                // 임박 깜빡임: 강도가 높을수록 빠르고 강하게 흰색으로 점멸한다.
                float wave = Mathf.Sin(Time.time * _imminentBlinkSpeed * (0.5f + _imminence)) * 0.5f + 0.5f;
                blink = wave * _imminentBlinkAmount * _imminence;
            }

            if (flashing)
            {
                // 변환 순간 강한 흰색 플래시(시간에 따라 감쇠).
                blink = Mathf.Max(blink, Mathf.Clamp01(_flashTimer / Mathf.Max(0.01f, _convertFlashDuration)));
            }

            Color glow = Color.Lerp(ResolveColor(_enemyHealth.SwipeDirection), Color.white, blink);
            WriteGlow(glow);
        }

        /// <summary>현재 방향(시퀀스 적은 현재 타격 대상 방향) 색을 글로우로 적용한다.</summary>
        public void ApplyCurrentColor()
        {
            if (_spriteRenderer == null || _enemyHealth == null)
            {
                return;
            }

            WriteGlow(ResolveColor(_enemyHealth.SwipeDirection));
        }

        /// <summary>주어진 글로우 색과 현재 스프라이트 UV 바운드를 MaterialPropertyBlock으로 적용한다.</summary>
        private void WriteGlow(Color glow)
        {
            _spriteRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_glowColorId, glow);
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
