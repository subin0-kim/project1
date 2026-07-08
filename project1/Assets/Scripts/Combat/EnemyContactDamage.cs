using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 기본 접촉 데미지(enemy_design.md): 플레이어와 닿아있는 동안 매초 지속 데미지를 입힌다.
    /// 물리 콜백 대신 거리 판정을 사용한다 — 플레이어는 화면 중앙에 고정이므로
    /// 적 자신과 플레이어 사이의 거리만 비교하면 되고, Rigidbody2D 동적 충돌이 필요 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyContactDamage : MonoBehaviour
    {
        [Tooltip("이 거리 이내면 '닿아있음'으로 판정한다.")]
        [SerializeField, Min(0.05f)]
        private float _contactRadius = 0.6f;

        [Tooltip("접촉 데미지 틱 간격(초). 기획 기준 1초.")]
        [SerializeField, Min(0.1f)]
        private float _tickInterval = 1f;

        [Tooltip("틱당 데미지. MonsterData의 초당 접촉 데미지로 덮어써진다. 0이면 데미지를 주지 않는다(기믹 적).")]
        [SerializeField, Min(0f)]
        private float _damagePerTick = 10f;

        private EnemyHealth _enemyHealth;
        private PlayerHealth _playerHealth;

        // 다음 틱까지 남은 시간. 0 이하이면 접촉 즉시 데미지가 들어간다(첫 접촉 프레임 포함).
        private float _tickTimer;

        public float ContactRadius => _contactRadius;
        public float DamagePerTick => _damagePerTick;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            ResetForReuse();
        }

        /// <summary>
        /// 풀 재사용 가드: 이전 생애의 틱 타이머가 남지 않도록 초기화한다.
        /// OnEnable에서 호출되며, EditMode 테스트에서는 SetActive로 OnEnable이 불리지 않으므로 직접 호출한다.
        /// </summary>
        internal void ResetForReuse()
        {
            _tickTimer = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (_enemyHealth == null)
            {
                _enemyHealth = GetComponent<EnemyHealth>();
            }

            if (_damagePerTick <= 0f || _enemyHealth == null || !_enemyHealth.IsAlive)
            {
                return;
            }

            // 틱 타이머는 접촉 여부와 무관하게 감소시킨다.
            // 닿았다 떨어졌다를 반복해도 최대 초당 1틱을 넘지 않게 하기 위함이다.
            _tickTimer = Mathf.Max(0f, _tickTimer - Mathf.Max(0f, deltaTime));

            // 플레이어 참조는 매 틱 지연 해석한다. 파괴된 참조는 유니티 pseudo-null로 걸러지므로,
            // 플레이어가 적보다 늦게 생성되거나 재생성되는 경우에도 새 인스턴스를 다시 찾는다.
            if (_playerHealth == null)
            {
                _playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (_playerHealth == null)
                {
                    return;
                }
            }

            if (!_playerHealth.IsAlive)
            {
                return;
            }

            // 거리 판정: 플레이어 고정(중앙) 전제이므로 sqrMagnitude 비교면 충분하다.
            // 스프라이트 정렬 등으로 Z 오프셋이 생겨도 판정이 틀어지지 않도록 2D(XY) 거리만 사용한다.
            Vector2 enemyPosition = transform.position;
            Vector2 playerPosition = _playerHealth.transform.position;
            float sqrDistance = (enemyPosition - playerPosition).sqrMagnitude;
            bool inContact = sqrDistance <= _contactRadius * _contactRadius;

            if (inContact && _tickTimer <= 0f)
            {
                _playerHealth.TakeDamage(_damagePerTick, _enemyHealth);
                _tickTimer = _tickInterval;
            }
        }

        /// <summary>
        /// MonsterData의 초당 접촉 데미지를 반영한다. 스폰 시 WaveCombatDirector가 호출한다.
        /// </summary>
        public void ApplyMonsterData(MonsterData data)
        {
            if (data == null)
            {
                return;
            }

            // 틱 간격이 1초 기준이므로 '초당 데미지 × 간격 = 틱당 데미지'로 환산한다.
            _damagePerTick = data.ContactDamagePerSecond * _tickInterval;
        }

        /// <summary>테스트/외부 배선용 플레이어 타겟 주입.</summary>
        public void SetPlayerTarget(PlayerHealth playerHealth)
        {
            _playerHealth = playerHealth;
        }
    }
}
