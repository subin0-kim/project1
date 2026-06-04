using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 어둑시니의 먹물 공격 시 InkSplatter 이미지를 랜덤 배치해 화면을 가리는 오버레이.
    /// 각 어둑시니 인스턴스가 독립적으로 소유한다. Canvas 하위에 배치되어야 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DarknessOverlay : MonoBehaviour
    {
        private const int SplatColumns = 4;
        private const int SplatRows = 3;
        private const int SplatCount = SplatColumns * SplatRows;
        private const float SplatBaseSize = 520f;

        [SerializeField]
        private Sprite _inkSplatterSprite;

        [SerializeField, Range(0f, 1f)]
        private float _maxAlpha = 1f;

        private CanvasGroup _canvasGroup;
        private readonly List<RectTransform> _splatters = new List<RectTransform>(SplatCount);

        private float _startAlpha;
        private float _targetAlpha;
        private float _fadeDuration;
        private float _fadeTimer;
        private bool _fading;
        private bool _initialized;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void Start()
        {
            if (!_initialized && _inkSplatterSprite != null)
            {
                BuildSplatters();
            }
        }

        private void Update()
        {
            if (!_fading)
            {
                return;
            }

            _fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTimer / _fadeDuration);
            _canvasGroup.alpha = Mathf.Lerp(_startAlpha, _targetAlpha, t);

            if (t >= 1f)
            {
                _fading = false;
            }
        }

        public void Initialize(Sprite inkSprite)
        {
            if (inkSprite == null || (inkSprite == _inkSplatterSprite && _initialized))
            {
                return;
            }

            _inkSplatterSprite = inkSprite;
            BuildSplatters();
        }

        public void FadeIn(float duration)
        {
            RandomizeSplatters();
            BeginFade(_canvasGroup.alpha, _maxAlpha, duration);
        }

        public void FadeOut(float duration)
        {
            BeginFade(_canvasGroup.alpha, 0f, duration);
        }

        public void ForceHide()
        {
            _fading = false;
            _canvasGroup.alpha = 0f;
        }

        private void BeginFade(float from, float to, float duration)
        {
            _startAlpha = from;
            _targetAlpha = to;
            _fadeDuration = Mathf.Max(0.01f, duration);
            _fadeTimer = 0f;
            _fading = true;
        }

        private void BuildSplatters()
        {
            foreach (RectTransform rt in _splatters)
            {
                if (rt != null)
                {
                    Destroy(rt.gameObject);
                }
            }
            _splatters.Clear();

            for (int i = 0; i < SplatCount; i++)
            {
                GameObject go = new GameObject($"InkSplat_{i}");
                go.transform.SetParent(transform, false);

                Image img = go.AddComponent<Image>();
                img.sprite = _inkSplatterSprite;
                img.color = new Color(0.05f, 0.02f, 0.08f, 1f);
                img.raycastTarget = false;
                img.preserveAspect = true;

                _splatters.Add(go.GetComponent<RectTransform>());
            }

            RandomizeSplatters();
            _initialized = true;
        }

        private void RandomizeSplatters()
        {
            if (_splatters.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _splatters.Count; i++)
            {
                int col = i % SplatColumns;
                int row = i / SplatColumns;

                float nx = (col + 0.5f + Random.Range(-0.3f, 0.3f)) / SplatColumns;
                float ny = (row + 0.5f + Random.Range(-0.3f, 0.3f)) / SplatRows;

                RectTransform rt = _splatters[i];
                rt.anchorMin = new Vector2(nx, ny);
                rt.anchorMax = new Vector2(nx, ny);
                rt.anchoredPosition = Vector2.zero;

                float scale = Random.Range(1.0f, 1.8f);
                rt.sizeDelta = new Vector2(SplatBaseSize * scale, SplatBaseSize * scale);
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
        }
    }
}
