using Mukseon.Gameplay.Progression;
using Mukseon.Gameplay.VFX;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 항마의 결계(降魔 結界, #73) — 발동 방식 ⑥ 상시 패시브(공용 스킬).
    /// 스킬 획득 즉시 플레이어 주변 결계 반경 내 적에게 일정 주기로 틱 데미지를 준다.
    /// 결계 원형 테두리(순수 시각 UI)는 <see cref="PlayerVFXController.SetBarrierRadius"/>로 현재 반경에 동기화한다.
    ///
    /// 재정의(player_stats.md): "결계 확장(공격 가능 반경 증가)" → "범위 내 틱 데미지". enum은 BarrierRadiusExpand를 그대로 사용.
    /// 레벨 추적은 <see cref="PlayerLevelSystem.OnSkillEffectPending"/> 구독 + OnEnable 동기화로 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BarrierAuraSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField]
        private PlayerVFXController _playerVFXController;

        [SerializeField, Tooltip("결계 중심(미지정 시 이 컴포넌트의 Transform)")]
        private Transform _origin;

        [SerializeField, Tooltip("결계 범위를 표시하는 흰 원 SpriteRenderer(플레이어 자식). 반경에 맞춰 스케일·표시된다.")]
        private SpriteRenderer _rangeIndicator;

        [SerializeField, Tooltip("연동 SkillData의 SkillId. OnEnable 레벨 동기화에 사용.")]
        private string _skillId = "barrier_aura";

        [Header("Per-Level (index 0 = Lv1) — skill_balance_mvp.md §2")]
        [SerializeField, Min(0.1f), Tooltip("결계 기본 반경(월드 유닛). 레벨별 배수가 곱해진다.")]
        private float _baseRadius = 3f;

        [SerializeField, Tooltip("레벨별 반경 배수(기본값=1.0, +20%=1.2, +40%=1.4)")]
        private float[] _radiusMultiplierPerLevel = { 1.0f, 1.2f, 1.4f };

        [SerializeField, Tooltip("레벨별 틱 데미지")]
        private float[] _tickDamagePerLevel = { 5f, 8f, 12f };

        [SerializeField, Tooltip("레벨별 틱 간격(초)")]
        private float[] _tickIntervalPerLevel = { 1.0f, 0.9f, 0.8f };

        public const int MaxLevel = 3;

        private int _level;
        private float _tickTimer;

        public int Level => _level;

        /// <summary>현재 결계 반경(미보유=0).</summary>
        public float CurrentRadius => _level < 1 ? 0f : _baseRadius * GetPerLevel(_radiusMultiplierPerLevel, _level, 1f);

        /// <summary>현재 틱 데미지(미보유=0).</summary>
        public float CurrentTickDamage => _level < 1 ? 0f : GetPerLevel(_tickDamagePerLevel, _level, 0f);

        /// <summary>현재 틱 간격(초).</summary>
        public float CurrentTickInterval => Mathf.Max(0.05f, GetPerLevel(_tickIntervalPerLevel, _level, 1f));

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }

            if (_playerVFXController == null)
            {
                _playerVFXController = GetComponent<PlayerVFXController>();
            }

            if (_origin == null)
            {
                _origin = transform;
            }
        }

        private void Start()
        {
            // OnEnable 시점에는 캐릭터를 생성한 측의 스케일 조정(player.localScale = ...)이
            // 아직 반영되지 않았을 수 있다. 초기화가 끝난 Start에서 한 번 더 동기화해 범위 표시기 스케일을 보정한다.
            SyncVisualRadius();
        }

        private void OnEnable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;
                // 이 효과 타입의 처리자가 있음을 알린다(등록이 없으면 선택 시 경고 — 빈 선택 감지, #66).
                _playerLevelSystem.RegisterEffectHandler(LevelUpSkillEffectType.BarrierRadiusExpand);
                // 비활성 중 부여/레벨업 이벤트를 놓쳤을 수 있어 현재 레벨을 직접 동기화한다.
                ApplyLevel(_playerLevelSystem.GetSkillLevel(_skillId));
            }
        }

        private void OnDisable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
                _playerLevelSystem.UnregisterEffectHandler(LevelUpSkillEffectType.BarrierRadiusExpand);
            }
        }

        private void HandleSkillEffectPending(SkillData skill, int nextLevel)
        {
            if (skill == null || skill.EffectType != LevelUpSkillEffectType.BarrierRadiusExpand)
            {
                return;
            }

            ApplyLevel(nextLevel);
        }

        /// <summary>레벨을 직접 설정한다(이벤트 핸들러 및 테스트에서 사용). [0, MaxLevel]로 클램프, 반경 비주얼 동기화.</summary>
        public void ApplyLevel(int level)
        {
            int previousLevel = _level;
            _level = Mathf.Clamp(level, 0, MaxLevel);

            // 최초 획득(0 → 보유)일 때만 타이머를 리셋해 깔끔하게 첫 인터벌을 시작한다.
            // 레벨업이나 재활성화(OnEnable) 시에는 기존 틱 주기를 유지해 첫 틱이 지연되지 않게 한다.
            if (previousLevel < 1 && _level >= 1)
            {
                _tickTimer = 0f;
            }

            SyncVisualRadius();
        }

        private void Update()
        {
            if (_level < 1)
            {
                return;
            }

            _tickTimer += Time.deltaTime;
            float interval = CurrentTickInterval;

            // 한 프레임에 여러 틱이 누적될 수 있으므로 while로 소비한다(저프레임/긴 deltaTime 대비).
            while (_tickTimer >= interval)
            {
                _tickTimer -= interval;
                DoTick();
            }
        }

        private void DoTick()
        {
            // _origin은 Awake에서 transform으로 보정되므로 항상 non-null이다.
            BarrierTickDamage.Apply((Vector2)_origin.position, CurrentRadius, CurrentTickDamage, EnemyHealth.ActiveEnemies, this);
        }

        private void SyncVisualRadius()
        {
            // 레벨 0(미보유)에서는 결계 링 비주얼을 건드리지 않는다(순수 시각 UI는 기본값으로 항상 표시).
            if (_level >= 1 && _playerVFXController != null)
            {
                _playerVFXController.SetBarrierRadius(CurrentRadius);
            }

            UpdateRangeIndicator();
        }

        /// <summary>흰 원 범위 표시기를 현재 반경에 맞춰 스케일하고, 스킬 보유 시에만 표시한다.</summary>
        private void UpdateRangeIndicator()
        {
            if (_rangeIndicator == null)
            {
                return;
            }

            bool show = _level >= 1;
            _rangeIndicator.enabled = show;
            if (!show || _rangeIndicator.sprite == null)
            {
                return;
            }

            // 부모(플레이어) 스케일을 보정해 월드 지름이 정확히 2*반경이 되도록 한다.
            float spriteWorldSize = _rangeIndicator.sprite.bounds.size.x;
            if (spriteWorldSize <= 0.0001f)
            {
                return;
            }

            Transform parent = _rangeIndicator.transform.parent;
            float parentScale = parent != null ? parent.lossyScale.x : 1f;
            if (Mathf.Abs(parentScale) < 0.0001f)
            {
                parentScale = 1f;
            }

            float scale = (CurrentRadius * 2f) / (spriteWorldSize * parentScale);
            _rangeIndicator.transform.localScale = new Vector3(scale, scale, 1f);
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
