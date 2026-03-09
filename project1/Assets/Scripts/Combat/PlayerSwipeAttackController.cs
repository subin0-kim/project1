using System;
using System.Collections;
using System.Collections.Generic;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerSwipeAttackController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private SwipeInputDetector _swipeInputDetector;

        [Header("Attack Animation")]
        [SerializeField, Min(0.01f)]
        private float _frameDuration = 0.08f;
        [SerializeField]
        private bool _restoreIdleAfterAttack = true;
        [SerializeField]
        private bool _autoGeneratePlaceholderSprites = true;

        [Header("Optional Manual Sprites")]
        [SerializeField]
        private Sprite _idleSprite;
        [SerializeField]
        private Sprite[] _upAttackFrames;
        [SerializeField]
        private Sprite[] _downAttackFrames;
        [SerializeField]
        private Sprite[] _leftAttackFrames;
        [SerializeField]
        private Sprite[] _rightAttackFrames;

        public event Action<SwipeDirection> OnAttackExecuted;

        private SpriteRenderer _spriteRenderer;
        private Coroutine _attackRoutine;
        private readonly Dictionary<SwipeDirection, Sprite[]> _attackAnimations = new Dictionary<SwipeDirection, Sprite[]>();
        private readonly List<UnityEngine.Object> _runtimeGeneratedAssets = new List<UnityEngine.Object>();

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_swipeInputDetector == null)
            {
                _swipeInputDetector = FindSwipeDetectorInScene();
            }

            BuildAnimationLibrary();

            if (_idleSprite != null)
            {
                _spriteRenderer.sprite = _idleSprite;
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

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeGeneratedAssets.Count; i++)
            {
                if (_runtimeGeneratedAssets[i] != null)
                {
                    Destroy(_runtimeGeneratedAssets[i]);
                }
            }

            _runtimeGeneratedAssets.Clear();
        }

        private void HandleSwipeDetected(SwipeDirection direction)
        {
            ExecuteAttack(direction);

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
            }

            _attackRoutine = StartCoroutine(PlayAttackAnimation(direction));
        }

        private void ExecuteAttack(SwipeDirection direction)
        {
            OnAttackExecuted?.Invoke(direction);

#if UNITY_EDITOR
            Debug.Log($"[PlayerSwipeAttackController] Attack executed by swipe: {direction}");
#endif
        }

        private IEnumerator PlayAttackAnimation(SwipeDirection direction)
        {
            Sprite[] frames;
            if (!_attackAnimations.TryGetValue(direction, out frames) || !HasValidFrames(frames))
            {
                yield break;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null)
                {
                    continue;
                }

                _spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(_frameDuration);
            }

            if (_restoreIdleAfterAttack && _idleSprite != null)
            {
                _spriteRenderer.sprite = _idleSprite;
            }

            _attackRoutine = null;
        }

        private void BuildAnimationLibrary()
        {
            _attackAnimations.Clear();

            _attackAnimations[SwipeDirection.Up] = _upAttackFrames;
            _attackAnimations[SwipeDirection.Down] = _downAttackFrames;
            _attackAnimations[SwipeDirection.Left] = _leftAttackFrames;
            _attackAnimations[SwipeDirection.Right] = _rightAttackFrames;

            bool isComplete =
                _idleSprite != null &&
                HasValidFrames(_upAttackFrames) &&
                HasValidFrames(_downAttackFrames) &&
                HasValidFrames(_leftAttackFrames) &&
                HasValidFrames(_rightAttackFrames);

            if (!isComplete && _autoGeneratePlaceholderSprites)
            {
                GeneratePlaceholderSprites();
            }
        }

        private void GeneratePlaceholderSprites()
        {
            _idleSprite = CreatePlayerSprite("Player_Idle", SwipeDirection.None, 0);
            _upAttackFrames = CreateAttackFrames(SwipeDirection.Up);
            _downAttackFrames = CreateAttackFrames(SwipeDirection.Down);
            _leftAttackFrames = CreateAttackFrames(SwipeDirection.Left);
            _rightAttackFrames = CreateAttackFrames(SwipeDirection.Right);

            _attackAnimations[SwipeDirection.Up] = _upAttackFrames;
            _attackAnimations[SwipeDirection.Down] = _downAttackFrames;
            _attackAnimations[SwipeDirection.Left] = _leftAttackFrames;
            _attackAnimations[SwipeDirection.Right] = _rightAttackFrames;
        }

        private Sprite[] CreateAttackFrames(SwipeDirection direction)
        {
            return new[]
            {
                CreatePlayerSprite($"Player_Attack_{direction}_A", direction, 0),
                CreatePlayerSprite($"Player_Attack_{direction}_B", direction, 1)
            };
        }

        private Sprite CreatePlayerSprite(string name, SwipeDirection direction, int variant)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = $"{name}_Texture";

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color body = new Color(0.18f, 0.68f, 0.95f, 1f);
            Color accent = new Color(1f, 0.33f, 0.23f, 1f);
            Color eye = Color.white;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            DrawCircle(pixels, size, 32, 30, 17, body);
            DrawRect(pixels, size, 26, 29, 34, 37, eye);
            DrawRect(pixels, size, 35, 38, 34, 37, eye);

            if (direction != SwipeDirection.None)
            {
                DrawAttackAccent(pixels, size, direction, variant, accent);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
            sprite.name = name;

            _runtimeGeneratedAssets.Add(texture);
            _runtimeGeneratedAssets.Add(sprite);

            return sprite;
        }

        private static void DrawAttackAccent(Color[] pixels, int size, SwipeDirection direction, int variant, Color color)
        {
            int thickness = variant == 0 ? 4 : 7;
            int extension = variant == 0 ? 10 : 14;

            switch (direction)
            {
                case SwipeDirection.Up:
                    DrawRect(pixels, size, 29, 34, 47, Mathf.Min(63, 47 + extension), color);
                    DrawRect(pixels, size, 24, 39, 43, 46, color);
                    break;
                case SwipeDirection.Down:
                    DrawRect(pixels, size, 29, 34, Mathf.Max(0, 5 - extension), 5, color);
                    DrawRect(pixels, size, 24, 39, 6, 9, color);
                    break;
                case SwipeDirection.Left:
                    DrawRect(pixels, size, Mathf.Max(0, 5 - extension), 5, 29, 34, color);
                    DrawRect(pixels, size, 6, 9, 24, 39, color);
                    break;
                case SwipeDirection.Right:
                    DrawRect(pixels, size, 47, Mathf.Min(63, 47 + extension), 29, 34, color);
                    DrawRect(pixels, size, 43, 46, 24, 39, color);
                    break;
            }

            // A simple pulse variation by widening around the center line.
            if (variant == 1)
            {
                if (direction == SwipeDirection.Up || direction == SwipeDirection.Down)
                {
                    DrawRect(pixels, size, 32 - thickness, 31 + thickness, 26, 28, color);
                }
                else
                {
                    DrawRect(pixels, size, 26, 28, 32 - thickness, 31 + thickness, color);
                }
            }
        }

        private static void DrawCircle(Color[] pixels, int size, int centerX, int centerY, int radius, Color color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= size)
                {
                    continue;
                }

                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= size)
                    {
                        continue;
                    }

                    int dx = x - centerX;
                    int dy = y - centerY;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                    {
                        pixels[(y * size) + x] = color;
                    }
                }
            }
        }

        private static void DrawRect(Color[] pixels, int size, int minX, int maxX, int minY, int maxY, Color color)
        {
            int clampedMinX = Mathf.Clamp(minX, 0, size - 1);
            int clampedMaxX = Mathf.Clamp(maxX, 0, size - 1);
            int clampedMinY = Mathf.Clamp(minY, 0, size - 1);
            int clampedMaxY = Mathf.Clamp(maxY, 0, size - 1);

            for (int y = clampedMinY; y <= clampedMaxY; y++)
            {
                for (int x = clampedMinX; x <= clampedMaxX; x++)
                {
                    pixels[(y * size) + x] = color;
                }
            }
        }

        private static bool HasValidFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0;
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
