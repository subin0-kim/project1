using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 재생의 굿거리(#74) — 발동 방식 ⑥ 상시 패시브(공용 스킬).
    /// 스킬 획득 즉시 전투 중 초당 일정량의 HP를 지속 회복한다. 회복량은 고정 수치로,
    /// 신당 체력(최대 HP) 업그레이드와 독립적으로 동작한다(skill_balance_mvp.md §3).
    ///
    /// 매 프레임 deltaTime에 비례해 회복하므로 회복이 매끄럽고, 최대 HP 초과 회복은
    /// <see cref="PlayerHealth.Heal"/>의 클램프가, 사망 후 회복은 동일 메서드의 사망 가드가 막는다.
    /// 레벨 추적은 <see cref="PlayerLevelSystem.OnSkillEffectPending"/> 구독 + OnEnable 동기화로
    /// 처리한다(BarrierAuraSkill·InkTrailSlowSkill과 동일 패턴).
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthRegenSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField]
        private PlayerHealth _playerHealth;

        [SerializeField, Tooltip("연동 SkillData의 SkillId. OnEnable 레벨 동기화에 사용.")]
        private string _skillId = "health_regen";

        [Header("Per-Level (index 0 = Lv1) — skill_balance_mvp.md §3")]
        [SerializeField, Tooltip("레벨별 초당 회복량(HP/초). 고정 수치 — 최대 HP 업그레이드와 독립적으로 동작.")]
        private float[] _regenPerSecondByLevel = { 2f, 4f, 7f };

        public const int MaxLevel = 3;

        private int _level;

        public int Level => _level;

        /// <summary>현재 초당 회복량(HP/초). 미보유=0.</summary>
        public float CurrentRegenPerSecond => _level < 1 ? 0f : GetPerLevel(_regenPerSecondByLevel, _level, 0f);

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }

            if (_playerHealth == null)
            {
                _playerHealth = GetComponent<PlayerHealth>();
            }
        }

        private void OnEnable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
                // 비활성 중 부여/레벨업 이벤트를 놓쳤을 수 있어 현재 레벨을 직접 동기화한다.
                ApplyLevel(_playerLevelSystem.GetSkillLevel(_skillId));
            }
        }

        private void OnDisable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
            }
        }

        private void HandleSkillEffectPending(SkillData skill, int nextLevel)
        {
            if (skill == null || skill.EffectType != LevelUpSkillEffectType.HealthRegen)
            {
                return;
            }

            ApplyLevel(nextLevel);
        }

        /// <summary>레벨을 직접 설정한다(이벤트 핸들러 및 테스트에서 사용). [0, MaxLevel]로 클램프.</summary>
        public void ApplyLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, MaxLevel);
        }

        private void Update()
        {
            if (_level < 1)
            {
                return;
            }

            ApplyRegen(Time.deltaTime);
        }

        /// <summary>
        /// <paramref name="deltaTime"/> 동안의 회복(초당 회복량 × deltaTime)을 적용한다.
        /// Update가 매 프레임 호출하며, 테스트에서는 임의의 deltaTime으로 직접 호출한다.
        /// 미보유·사망·최대 HP 도달 시의 처리는 PlayerHealth.Heal에 위임한다.
        /// </summary>
        public void ApplyRegen(float deltaTime)
        {
            if (_level < 1 || _playerHealth == null || deltaTime <= 0f)
            {
                return;
            }

            float amount = CurrentRegenPerSecond * deltaTime;
            if (amount > 0f)
            {
                _playerHealth.Heal(amount);
            }
        }

        private static float GetPerLevel(float[] perLevel, int level, float fallback)
        {
            if (level < 1 || perLevel == null || perLevel.Length == 0)
            {
                return fallback;
            }

            int index = Mathf.Clamp(level - 1, 0, perLevel.Length - 1);
            return perLevel[index];
        }
    }
}
