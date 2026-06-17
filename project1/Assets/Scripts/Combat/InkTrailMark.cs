using System;
using System.Collections.Generic;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 끈적한 묵액(#75)의 먹물 자국 — 스와이프 끝점에 생성되는 원형 슬로우 존.
    /// 매 프레임 반경 내 적의 <see cref="EnemyMover.RequestSlow"/>를 호출해 이동 속도를 줄이고,
    /// 지속 시간이 끝나면 페이드 후 풀에 반환된다. 슬로우는 프레임 단위라 적이 자국을 벗어나면 자동 복원된다.
    ///
    /// 풀에서 비활성으로 꺼내 <see cref="Initialize"/>로 설정한 뒤 SetActive(true)로 활성화한다.
    /// 같은 자리에 다시 생성될 경우 새 자국 대신 <see cref="Refresh"/>로 지속 시간만 갱신한다(중첩 처리).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class InkTrailMark : MonoBehaviour
    {
        [SerializeField, Tooltip("자국 먹물 색(RGB). 알파는 _baseAlpha로 제어.")]
        private Color _inkColor = new Color(0.05f, 0.03f, 0.03f, 1f);

        [SerializeField, Range(0f, 1f), Tooltip("자국 기본 알파(페이드 시작 전)")]
        private float _baseAlpha = 0.65f;

        [SerializeField, Range(0f, 1f), Tooltip("이 비율의 잔여 시간부터 알파 페이드 시작")]
        private float _fadeStartRemainingRatio = 0.4f;

        private SpriteRenderer _renderer;
        private float _radius = 1f;
        private float _slowMultiplier = 0.7f;
        private float _duration = 2f;
        private float _remaining;
        private bool _active;

        /// <summary>
        /// 자국이 비활성화(만료·풀 반환 등)될 때 발생. 구독한 스킬이 추적 목록에서 즉시 제거해,
        /// 풀 재사용 시 참조 오염/누수를 방지한다.
        /// </summary>
        public event Action<InkTrailMark> OnDeactivated;

        public Vector2 WorldPosition => transform.position;
        public bool IsActiveMark => _active && isActiveAndEnabled;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>활성화 전(비활성 상태)에서 호출해 자국을 설정한다.</summary>
        public void Initialize(float duration, float slowMultiplier, float radius)
        {
            _duration = Mathf.Max(0.1f, duration);
            _slowMultiplier = Mathf.Clamp01(slowMultiplier);
            _radius = Mathf.Max(0.1f, radius);
            _remaining = _duration;
        }

        /// <summary>중첩 생성 시 지속 시간을 갱신한다(이미 활성인 자국에 호출).</summary>
        public void Refresh(float duration)
        {
            _duration = Mathf.Max(0.1f, duration);
            _remaining = _duration;
        }

        private void OnEnable()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            _remaining = _duration;
            _active = true;

            ApplyRadiusScale();
            ApplyAlpha(1f);
        }

        private void OnDisable()
        {
            // 풀 반환·만료·파괴 등 비활성화 시점에 구독자(스킬)가 즉시 참조를 정리하도록 알린다.
            _active = false;
            OnDeactivated?.Invoke(this);
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            ApplySlowToEnemiesInRange();

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _active = false;
                ReturnToPool();
                return;
            }

            // 잔여 시간이 fadeStart 이하로 내려가면 알파를 0까지 선형 페이드.
            float fadeWindow = _duration * _fadeStartRemainingRatio;
            float t = fadeWindow > 0.0001f ? Mathf.Clamp01(_remaining / fadeWindow) : 1f;
            ApplyAlpha(t);
        }

        private void ApplySlowToEnemiesInRange()
        {
            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;
            if (enemies == null)
            {
                return;
            }

            float sqrRadius = _radius * _radius;
            Vector2 center = transform.position;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (((Vector2)enemy.transform.position - center).sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                enemy.Mover?.RequestSlow(_slowMultiplier);
            }
        }

        private void ApplyRadiusScale()
        {
            if (_renderer == null || _renderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = _renderer.sprite.bounds.size;
            float diameter = _radius * 2f;
            float sx = spriteSize.x > 0.0001f ? diameter / spriteSize.x : diameter;
            float sy = spriteSize.y > 0.0001f ? diameter / spriteSize.y : diameter;
            transform.localScale = new Vector3(sx, sy, 1f);
        }

        private void ApplyAlpha(float fade01)
        {
            if (_renderer == null)
            {
                return;
            }

            Color c = _inkColor;
            c.a = _baseAlpha * Mathf.Clamp01(fade01);
            _renderer.color = c;
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
