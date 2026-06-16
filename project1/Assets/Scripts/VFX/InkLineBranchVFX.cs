using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.VFX
{
    /// <summary>
    /// 부채살 흩뿌리기(#76)의 한 '갈래' — 먹선 한 줄이 바깥으로 뻗어나가는 연출.
    /// 풀에서 비활성 상태로 꺼낸 뒤 <see cref="Configure"/>로 스프라이트/길이/두께/색을 설정하고
    /// SetActive(true)로 활성화하면, OnEnable에서 연출을 시작한다.
    /// 길이를 0→목표로 늘리며(바깥으로 뻗는 느낌) 알파를 서서히 0으로 만든 뒤 풀에 반환한다.
    ///
    /// 스프라이트는 왼쪽(안쪽) 끝이 피벗이어야 한다(피벗 (0, 0.5)).
    /// 그래야 localScale.x를 키울 때 플레이어 쪽 원점에서 바깥으로 자라난다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class InkLineBranchVFX : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("연출 총 길이(초)")]
        private float _duration = 0.3f;

        [SerializeField, Range(0f, 1f), Tooltip("시작 시 길이 비율(이 값에서 1.0까지 뻗어나감)")]
        private float _startLengthFactor = 0.25f;

        [SerializeField, Range(0f, 1f), Tooltip("뻗어나가기가 끝나는 시점(정규화 시간)")]
        private float _extendEndTime = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("알파 페이드가 시작되는 시점(정규화 시간)")]
        private float _fadeStartTime = 0.45f;

        private SpriteRenderer _renderer;
        private float _targetLength = 2f;
        private float _thickness = 0.25f;
        private Color _baseColor = Color.black;
        private float _elapsed;
        private bool _animating;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>활성화 전(비활성 상태)에서 호출해 갈래의 외형을 설정한다.</summary>
        public void Configure(Sprite sprite, float length, float thickness, Color color)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }

            _targetLength = Mathf.Max(0.01f, length);
            _thickness = Mathf.Max(0.01f, thickness);
            _baseColor = color;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _animating = true;
            ApplyFrame(0f);
        }

        private void Update()
        {
            if (!_animating)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            ApplyFrame(t);

            if (t >= 1f)
            {
                _animating = false;
                ReturnToPool();
            }
        }

        private void ApplyFrame(float t)
        {
            if (_renderer == null || _renderer.sprite == null)
            {
                return;
            }

            // 길이: startLengthFactor → 1.0 까지 ease-out으로 뻗어나간다.
            float extendT = _extendEndTime <= 0f ? 1f : Mathf.Clamp01(t / _extendEndTime);
            float lengthFactor = Mathf.Lerp(_startLengthFactor, 1f, SmoothStep(extendT));
            float currentLength = _targetLength * lengthFactor;

            Vector2 spriteSize = _renderer.sprite.bounds.size;
            float sx = spriteSize.x > 0.0001f ? currentLength / spriteSize.x : currentLength;
            float sy = spriteSize.y > 0.0001f ? _thickness / spriteSize.y : _thickness;
            transform.localScale = new Vector3(sx, sy, 1f);

            // 알파: fadeStartTime 이후 1→0.
            float fade = _fadeStartTime >= 1f
                ? 1f
                : 1f - Mathf.Clamp01((t - _fadeStartTime) / (1f - _fadeStartTime));
            Color c = _baseColor;
            c.a = _baseColor.a * fade;
            _renderer.color = c;
        }

        private static float SmoothStep(float x)
        {
            return x * x * (3f - 2f * x);
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
