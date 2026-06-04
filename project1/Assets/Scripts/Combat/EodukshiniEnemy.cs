using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 어둑시니 적 컴포넌트.
    /// 화면 가장자리로 이동 후 먹물(어둠 오버레이)로 시야를 방해한다.
    /// 처치 시 어둠이 즉시 소산된다. 각 인스턴스가 독립 오버레이를 소유하므로
    /// 다수 동시 등장 시 오버레이가 중첩되어 시야 방해가 가중된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class EodukshiniEnemy : MonoBehaviour
    {
        private enum State
        {
            MovingToEdge,
            WaitingAtEdge,
            DarknessActive,
            Cooldown,
        }

        [Header("Darkness Visual")]
        [SerializeField]
        private GameObject _overlayPrefab;

        [SerializeField]
        private Sprite _inkSplatterSprite;

        [Header("Timing")]
        [SerializeField, Min(0f)]
        private float _waitBeforeAttack = 1f;

        [SerializeField, Min(0.1f)]
        private float _darknessDuration = 5f;

        [SerializeField, Min(0.1f)]
        private float _fadeInDuration = 0.5f;

        [SerializeField, Min(0.1f)]
        private float _fadeOutDuration = 1.5f;

        [SerializeField, Min(0f)]
        private float _cooldownDuration = 3f;

        private EnemyHealth _enemyHealth;
        private DarknessOverlay _overlay;
        private State _state;
        private float _stateTimer;
        private bool _fadeOutStarted;
        private bool _isDarknessApplied;
        private Vector3 _edgeTarget;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _enemyHealth.OnDied += HandleDied;

            if (_overlay == null && _overlayPrefab != null)
            {
                _overlay = Instantiate(_overlayPrefab).GetComponentInChildren<DarknessOverlay>();
            }

            if (_overlay != null && _inkSplatterSprite != null)
            {
                _overlay.Initialize(_inkSplatterSprite);
            }

            _edgeTarget = CalculateScreenEdgePosition();
            _state = State.MovingToEdge;
            _stateTimer = 0f;
            _isDarknessApplied = false;
        }

        private void OnDisable()
        {
            _enemyHealth.OnDied -= HandleDied;
            if (_isDarknessApplied)
            {
                _overlay?.FadeOut(_fadeOutDuration);
                _isDarknessApplied = false;
            }
        }

        private void OnDestroy()
        {
            if (_overlay != null)
            {
                Destroy(_overlay.gameObject);
            }
        }

        private void Update()
        {
            if (!_enemyHealth.IsAlive)
            {
                return;
            }

            _stateTimer += Time.deltaTime;

            switch (_state)
            {
                case State.MovingToEdge:
                    UpdateMovingToEdge();
                    break;
                case State.WaitingAtEdge:
                    if (_stateTimer >= _waitBeforeAttack)
                    {
                        EnterDarknessActive();
                    }
                    break;
                case State.DarknessActive:
                    UpdateDarknessActive();
                    break;
                case State.Cooldown:
                    if (_stateTimer >= _cooldownDuration)
                    {
                        _edgeTarget = CalculateScreenEdgePosition();
                        _state = State.MovingToEdge;
                        _stateTimer = 0f;
                    }
                    break;
            }
        }

        private void UpdateMovingToEdge()
        {
            float step = _enemyHealth.MoveSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, _edgeTarget, step);

            if (Vector3.Distance(transform.position, _edgeTarget) < 0.05f)
            {
                _state = State.WaitingAtEdge;
                _stateTimer = 0f;
            }
        }

        private void EnterDarknessActive()
        {
            _state = State.DarknessActive;
            _stateTimer = 0f;
            _fadeOutStarted = false;
            if (_overlay != null)
            {
                _overlay.FadeIn(_fadeInDuration);
                _isDarknessApplied = true;
            }
        }

        private void UpdateDarknessActive()
        {
            if (!_fadeOutStarted && _stateTimer >= _darknessDuration - _fadeOutDuration)
            {
                _fadeOutStarted = true;
                if (_isDarknessApplied)
                {
                    _overlay?.FadeOut(_fadeOutDuration);
                    _isDarknessApplied = false;
                }
            }

            if (_stateTimer >= _darknessDuration)
            {
                _state = State.Cooldown;
                _stateTimer = 0f;
            }
        }

        private void HandleDied()
        {
            if (_isDarknessApplied)
            {
                _overlay?.FadeOut(_fadeOutDuration);
                _isDarknessApplied = false;
            }
        }

        private Vector3 CalculateScreenEdgePosition()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return transform.position;
            }

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 camPos = cam.transform.position;

            // 4변 중 랜덤 선택, 내부 여백 15% 남긴 가장자리
            int side = Random.Range(0, 4);
            Vector3 local;
            switch (side)
            {
                case 0: local = new Vector3(Random.Range(-halfW * 0.8f, halfW * 0.8f),  halfH * 0.85f, 0f); break; // 상
                case 1: local = new Vector3(Random.Range(-halfW * 0.8f, halfW * 0.8f), -halfH * 0.85f, 0f); break; // 하
                case 2: local = new Vector3(-halfW * 0.85f, Random.Range(-halfH * 0.8f, halfH * 0.8f), 0f); break; // 좌
                default: local = new Vector3( halfW * 0.85f, Random.Range(-halfH * 0.8f, halfH * 0.8f), 0f); break; // 우
            }

            return new Vector3(camPos.x + local.x, camPos.y + local.y, 0f);
        }
    }
}
