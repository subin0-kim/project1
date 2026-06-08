using System.Collections.Generic;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 경험치 구슬 '혼불'(#67, `honbul_system.md`). 적 처치 위치에 드랍되어
    /// 세 가지 상태로 동작한다: 정적(Idle, 소멸 타이머 진행) → 스와이프 끝점 당기기(Pulled)
    /// 또는 자력 흡수(Attracting). 당겨지거나 흡수되는 동안에는 소멸 타이머가 정지한다.
    /// 모든 혼불은 오브젝트 풀로 관리되며, 활성 혼불은 <see cref="ActiveSouls"/>에 등록되어
    /// 스와이프 끝점 탐색(<see cref="SwipeSoulPuller"/>)에 사용된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoulOrb : MonoBehaviour
    {
        private enum SoulState
        {
            Idle,        // 정적: 드랍 직후 산개 후 정지. 소멸 타이머가 진행된다.
            Pulled,      // 스와이프 끝점 당기기로 중앙을 향해 이동 중. 소멸 타이머 정지.
            Attracting   // 자력 반경 진입 후 가속하며 흡수되는 중. 소멸 타이머 정지.
        }

        private static readonly List<SoulOrb> _activeSouls = new List<SoulOrb>(64);

        // Enter Play Mode 설정에서 Domain Reload가 꺼져 있으면 static 필드가 세션 간 유지된다.
        // 이전 세션의 파괴된 혼불 참조가 남아 누수/불필요한 순회를 일으키지 않도록 시작 시 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveSouls()
        {
            _activeSouls.Clear();
        }

        /// <summary>현재 씬에 활성화된 모든 혼불. 스와이프 끝점 당기기 탐색용(읽기 전용).</summary>
        public static IReadOnlyList<SoulOrb> ActiveSouls => _activeSouls;

        [SerializeField, Min(1)]
        private int _experienceAmount = 1;

        [Header("드랍 산개")]
        [SerializeField, Min(0f)]
        private float _scatterSpeed = 1.2f;

        [Header("자력 흡수")]
        [SerializeField, Min(0.1f)]
        private float _attractSpeed = 6f;

        [SerializeField, Min(0.1f)]
        private float _attractAcceleration = 12f;

        [Header("스와이프 끝점 당기기")]
        [SerializeField, Min(0.1f)]
        [Tooltip("스와이프 끝점에 당겨질 때 중앙 방향으로 이동하는 속도.")]
        private float _pullSpeed = 8f;

        [Header("소멸")]
        [SerializeField, Min(0.1f)]
        [Tooltip("드랍 후 미획득 시 소멸까지 걸리는 시간(초). 이동 상태에서는 정지/리셋된다.")]
        private float _lifetime = 15f;

        [SerializeField, Min(0f)]
        [Tooltip("소멸 N초 전부터 깜빡임 경고를 시작한다.")]
        private float _warningDuration = 3f;

        [SerializeField, Min(0.1f)]
        [Tooltip("소멸 경고 깜빡임 속도.")]
        private float _blinkSpeed = 12f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("깜빡임 시 내려가는 최소 알파 비율.")]
        private float _blinkMinAlpha = 0.25f;

        private SpriteRenderer _spriteRenderer;
        private Color _baseColor = Color.white;
        private float _initialAttractSpeed;

        private SoulState _state;
        private Vector3 _scatterVelocity;
        private float _despawnTimer;
        private float _pullRemaining;
        private float _currentAttractSpeed;

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _baseColor = _spriteRenderer.color;
            }

            _initialAttractSpeed = _attractSpeed;
        }

        private void OnEnable()
        {
            _activeSouls.Add(this);

            // 풀 재사용 시 상태를 완전히 초기화한다.
            float randomX = Random.Range(-1f, 1f);
            float randomY = Random.Range(-1f, 1f);
            Vector3 direction = new Vector3(randomX, randomY, 0f).normalized;
            _scatterVelocity = direction * _scatterSpeed;

            _state = SoulState.Idle;
            _despawnTimer = 0f;
            _pullRemaining = 0f;
            _currentAttractSpeed = _initialAttractSpeed;
            RestoreColor();
        }

        private void OnDisable()
        {
            _activeSouls.Remove(this);
        }

        private void Update()
        {
            SoulCollector collector = SoulCollector.ActiveCollector;
            if (collector == null)
            {
                // 수거자가 없어도 소멸은 진행해 화면에 무한히 쌓이지 않도록 한다.
                TickDespawn();
                return;
            }

            Vector3 center = collector.transform.position;
            center.z = transform.position.z;
            Vector3 toCenter = center - transform.position;
            float distance = toCenter.magnitude;

            // 자력 반경 진입 시 어떤 상태에서든 흡수로 전환한다.
            if (_state != SoulState.Attracting && distance <= collector.AttractionRadius)
            {
                EnterAttracting();
            }

            switch (_state)
            {
                case SoulState.Idle:
                    UpdateIdle();
                    break;
                case SoulState.Pulled:
                    UpdatePulled(toCenter, distance);
                    break;
                case SoulState.Attracting:
                    UpdateAttracting(collector, toCenter, distance);
                    break;
            }
        }

        /// <summary>
        /// 스와이프 끝점 당기기로 이 혼불을 중앙 방향으로 <paramref name="moveDistance"/>만큼 이동시킨다.
        /// 이미 당겨지거나 흡수 중이면 무시한다(연속 스와이프의 중복 당기기 방지).
        /// </summary>
        public void Pull(float moveDistance)
        {
            if (_state == SoulState.Pulled || _state == SoulState.Attracting)
            {
                return;
            }

            _state = SoulState.Pulled;
            _scatterVelocity = Vector3.zero;
            _pullRemaining = Mathf.Max(0f, moveDistance);
            _despawnTimer = 0f;   // 이동 상태 전환 시 소멸 타이머 리셋.
            RestoreColor();
        }

        public void SetExperienceAmount(int experienceAmount)
        {
            _experienceAmount = Mathf.Max(1, experienceAmount);
        }

        private void UpdateIdle()
        {
            // 드랍 직후 산개 속도를 점차 감쇠시켜 제자리에 정착한다.
            transform.position += _scatterVelocity * Time.deltaTime;
            // 프레임레이트 독립적 지수 감쇠. Lerp 방식은 프레임 드랍 시 deltaTime*4가 1을 넘으면 속도가 즉시 끊긴다.
            _scatterVelocity *= Mathf.Exp(-4f * Time.deltaTime);
            TickDespawn();
        }

        private void UpdatePulled(Vector3 toCenter, float distance)
        {
            if (_pullRemaining <= 0f)
            {
                // 당기기 이동 거리를 모두 소진하면 다시 정적 상태로 복귀하며 소멸 타이머를 재개한다.
                _state = SoulState.Idle;
                return;
            }

            Vector3 dir = distance > 0.001f ? toCenter / distance : Vector3.zero;
            float step = Mathf.Min(_pullSpeed * Time.deltaTime, _pullRemaining);
            transform.position += dir * step;
            _pullRemaining -= step;
        }

        private void UpdateAttracting(SoulCollector collector, Vector3 toCenter, float distance)
        {
            if (distance <= collector.CollectRadius)
            {
                collector.Collect(_experienceAmount);
                Release();
                return;
            }

            _currentAttractSpeed += _attractAcceleration * Time.deltaTime;
            Vector3 dir = distance > 0.001f ? toCenter / distance : Vector3.zero;
            // 가속된 속도가 남은 거리를 넘어 중앙을 지나치며 진동하는 것을 막는다(오버슈트 클램프).
            float step = Mathf.Min(_currentAttractSpeed * Time.deltaTime, distance);
            transform.position += dir * step;
        }

        private void EnterAttracting()
        {
            _state = SoulState.Attracting;
            _despawnTimer = 0f;
            _currentAttractSpeed = _initialAttractSpeed;
            RestoreColor();
        }

        private void TickDespawn()
        {
            _despawnTimer += Time.deltaTime;
            if (_despawnTimer >= _lifetime)
            {
                Release();
                return;
            }

            UpdateBlink();
        }

        /// <summary>소멸 경고 구간(_lifetime - _warningDuration ~ _lifetime)에서 알파를 점멸시킨다.</summary>
        private void UpdateBlink()
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            float remaining = _lifetime - _despawnTimer;
            if (remaining > _warningDuration)
            {
                return;
            }

            float wave = Mathf.Abs(Mathf.Sin(_despawnTimer * _blinkSpeed));
            Color color = _baseColor;
            color.a = _baseColor.a * Mathf.Lerp(_blinkMinAlpha, 1f, wave);
            _spriteRenderer.color = color;
        }

        private void RestoreColor()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _baseColor;
            }
        }

        private void Release()
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
