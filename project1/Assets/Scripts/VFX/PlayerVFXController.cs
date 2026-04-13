using Mukseon.Core.Pool;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.VFX
{
    /// <summary>
    /// 플레이어 관련 VFX를 총괄합니다.
    /// - 적 피격 시 먹물 splatter 파티클
    /// - 적 처치 시 먹물 burst 파티클 + "정화" 플로팅 텍스트(GameplayHudBootstrapper에서 처리)
    /// - InkExplosionOnKill 스킬 활성화 시 처치 위치에 추가 폭발 이펙트
    /// - BarrierRadiusExpand 스킬 적용 시 결계 링 크기 갱신
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVFXController : MonoBehaviour
    {
        [Header("Particle Prefabs")]
        [SerializeField, Tooltip("적 피격 시 spawning되는 먹물 splatter 이펙트 프리팹")]
        private GameObject _hitEffectPrefab;

        [SerializeField, Tooltip("적 처치 시 spawning되는 먹물 burst 이펙트 프리팹")]
        private GameObject _deathEffectPrefab;

        [SerializeField, Tooltip("InkExplosionOnKill 스킬 활성화 시 처치 위치에 추가로 spawning되는 폭발 프리팹")]
        private GameObject _inkExplosionPrefab;

        [Header("Barrier Visual")]
        [SerializeField, Tooltip("결계 시각화에 사용할 SpriteRenderer (플레이어 하위 오브젝트)")]
        private SpriteRenderer _barrierRenderer;

        [SerializeField, Min(0f), Tooltip("초기 결계 반지름 (월드 유닛)")]
        private float _barrierRadius = 3f;

        private PlayerLevelSystem _playerLevelSystem;
        private bool _inkExplosionOnKillActive;

        private void Awake()
        {
            _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            if (_playerLevelSystem == null)
            {
                Debug.LogWarning("[PlayerVFXController] PlayerLevelSystem 컴포넌트를 찾을 수 없습니다. 스킬 VFX가 작동하지 않습니다.");
            }

            UpdateBarrierVisual();
        }

        private void OnEnable()
        {
            EnemyHealth.AnyEnemyDamaged += HandleAnyEnemyDamaged;
            EnemyHealth.AnyEnemyDied += HandleAnyEnemyDied;

            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
            }
        }

        private void OnDisable()
        {
            EnemyHealth.AnyEnemyDamaged -= HandleAnyEnemyDamaged;
            EnemyHealth.AnyEnemyDied -= HandleAnyEnemyDied;

            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
            }
        }

        /// <summary>외부(스킬 시스템 등)에서 결계 반지름을 직접 설정합니다.</summary>
        public void SetBarrierRadius(float radius)
        {
            _barrierRadius = Mathf.Max(0f, radius);
            UpdateBarrierVisual();
        }

        private void HandleSkillEffectPending(SkillData skill, int nextLevel)
        {
            switch (skill.EffectType)
            {
                case LevelUpSkillEffectType.InkExplosionOnKill:
                    _inkExplosionOnKillActive = true;
                    break;
                case LevelUpSkillEffectType.BarrierRadiusExpand:
                    // 매 선택 시 skill.Value만큼 고정 확장 (nextLevel 무관)
                    _barrierRadius += skill.Value;
                    UpdateBarrierVisual();
                    break;
            }
        }

        private void HandleAnyEnemyDamaged(EnemyHealth enemy, float damage)
        {
            if (enemy == null)
            {
                return;
            }

            SpawnParticle(_hitEffectPrefab, enemy.transform.position);
        }

        private void HandleAnyEnemyDied(EnemyHealth enemy)
        {
            if (enemy == null)
            {
                return;
            }

            SpawnParticle(_deathEffectPrefab, enemy.transform.position);

            if (_inkExplosionOnKillActive)
            {
                SpawnParticle(_inkExplosionPrefab, enemy.transform.position);
            }
        }

        private void SpawnParticle(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Get(prefab, position, Quaternion.identity);
            }
            else
            {
                Instantiate(prefab, position, Quaternion.identity);
            }
        }

        private void UpdateBarrierVisual()
        {
            if (_barrierRenderer == null)
            {
                return;
            }

            bool visible = _barrierRadius > 0f;
            _barrierRenderer.enabled = visible;
            if (visible)
            {
                float diameter = _barrierRadius * 2f;

                // 부모 스케일과 스프라이트 크기를 보정하여 월드 유닛과 일치시킴
                Vector3 lossyScale = _barrierRenderer.transform.parent != null
                    ? _barrierRenderer.transform.parent.lossyScale
                    : Vector3.one;

                float spriteWorldSize = 1f;
                if (_barrierRenderer.sprite != null)
                {
                    spriteWorldSize = _barrierRenderer.sprite.bounds.size.x;
                }

                float scale = diameter / (spriteWorldSize * lossyScale.x);
                _barrierRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
