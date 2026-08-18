using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 재생의 굿거리(#74) — 발동 방식 ⑥ 상시 패시브(공용 스킬).
    /// 스킬 획득 즉시 전투 중 초당 일정량의 HP를 지속 회복한다. 회복량은 고정 수치로,
    /// 신당 체력(최대 HP) 업그레이드와 독립적으로 동작한다(skill_balance_mvp.md §3).
    ///
    /// 매 프레임 deltaTime에 비례해 회복하므로 회복이 매끄럽다. 사망했거나 이미 최대 HP면 ApplyRegen이
    /// 조기 반환해 불필요한 Heal 호출·이벤트를 피하고, 최종 안전장치로 <see cref="PlayerHealth.Heal"/>도 같은 조건을 막는다.
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

        // 레벨 값 배열 길이를 실효 최대 레벨로 삼는다(배열이 비면 MaxLevel로 폴백).
        // 인스펙터에서 배열을 늘리거나 줄여도 클램프가 정의된 데이터 범위를 벗어나지 않게 한다.
        private int EffectiveMaxLevel =>
            _regenPerSecondByLevel != null && _regenPerSecondByLevel.Length > 0
                ? _regenPerSecondByLevel.Length
                : MaxLevel;

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

            // 레벨 값 배열 길이가 설계상 최대 레벨과 다르면 GetPerLevel이 마지막 값을 반복 사용하게 되어
            // 의도와 다른 수치가 적용될 수 있으므로 설정 드리프트를 경고로 알린다(클램프 자체는 실효 길이 기준이라 범위는 안전).
            if (_regenPerSecondByLevel == null || _regenPerSecondByLevel.Length != MaxLevel)
            {
                int length = _regenPerSecondByLevel != null ? _regenPerSecondByLevel.Length : 0;
                Debug.LogWarning($"[HealthRegenSkill] _regenPerSecondByLevel 길이({length})가 MaxLevel({MaxLevel})과 다릅니다. 레벨별 수치 설정을 확인하세요.");
            }
        }

        private void OnEnable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
                // 이 효과 타입의 처리자가 있음을 알린다(등록이 없으면 선택 시 경고 — 빈 선택 감지, #66).
                _playerLevelSystem.RegisterEffectHandler(LevelUpSkillEffectType.HealthRegen);
                // 비활성 중 부여/레벨업 이벤트를 놓쳤을 수 있어 현재 레벨을 직접 동기화한다.
                ApplyLevel(_playerLevelSystem.GetSkillLevel(_skillId));
            }
        }

        private void OnDisable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
                _playerLevelSystem.UnregisterEffectHandler(LevelUpSkillEffectType.HealthRegen);
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

        /// <summary>레벨을 직접 설정한다(이벤트 핸들러 및 테스트에서 사용). [0, 실효 최대 레벨]로 클램프.</summary>
        public void ApplyLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, EffectiveMaxLevel);
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
        /// 미보유·사망·최대 HP 상태에서는 조기 반환해 불필요한 Heal 호출·이벤트를 피한다.
        /// </summary>
        public void ApplyRegen(float deltaTime)
        {
            if (_level < 1 || _playerHealth == null || deltaTime <= 0f)
            {
                return;
            }

            // 사망/최대 HP면 Heal이 내부적으로 무효 처리하지만, 매 프레임 호출 자체를 피하려고 먼저 거른다.
            if (!_playerHealth.IsAlive || _playerHealth.CurrentHealth >= _playerHealth.MaxHealth)
            {
                return;
            }

            // 여기 도달 시 _level >= 1(회복량 > 0)이고 deltaTime > 0이므로 amount는 항상 양수다.
            float amount = CurrentRegenPerSecond * deltaTime;
            _playerHealth.Heal(amount);
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
