using System;
using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression.Cards;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [DisallowMultipleComponent]
    public class PlayerLevelSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField]
        private SwipeAttackEventListener _swipeAttackEventListener;

        [Header("Progression")]
        [SerializeField, Min(1f)]
        private float _baseExperienceThreshold = 5f;

        [SerializeField, Min(1f)]
        private float _thresholdGrowthFactor = 1.35f;

        [SerializeField]
        private bool _pauseGameOnSelection = true;

        [Header("Skill Pool")]
        [SerializeField]
        private List<SkillData> _skillDefinitions = new List<SkillData>();

        private readonly List<SkillData> _currentChoices = new List<SkillData>(CardPool<SkillData>.DefaultDrawCount);
        private readonly SkillLevelRegistry _skillLevels = new SkillLevelRegistry();
        private readonly CardEligibilityFilterSet<SkillData> _cardEligibilityFilters = new CardEligibilityFilterSet<SkillData>();
        private readonly SkillEffectHandlerRegistry _effectHandlers = new SkillEffectHandlerRegistry();

        private LevelProgressionModel _progressionModel;
        private CardPool<SkillData> _cardPool;
        private CardEffectApplier _effectApplier;
        private bool _startingSkillsGranted;

        public event Action<int, float, float> OnExperienceChanged;
        public event Action<int, IReadOnlyList<SkillData>> OnLevelSelectionOpened;
        public event Action<int> OnLevelSelectionClosed;
        public event Action<SkillData, int> OnSkillApplied;

        /// <summary>
        /// 해당 시스템이 아직 구현되지 않은 스킬 효과 타입이 선택되었을 때 발생합니다.
        /// 각 전담 시스템(DokkaebiOrb, InkExplosion 등)이 이 이벤트를 구독하여 효과를 처리합니다.
        /// 두 번째 인자는 적용될 스킬의 다음 레벨 번호입니다. 구독자가 레벨 시스템을 직접 조회할 경우
        /// 레벨 증가 전 시점이므로 off-by-one이 발생할 수 있어 명시적으로 전달합니다.
        /// </summary>
        public event Action<SkillData, int> OnSkillEffectPending;

        public int CurrentLevel => _progressionModel != null ? _progressionModel.Level : 1;
        public float CurrentExperience => _progressionModel != null ? _progressionModel.CurrentExperience : 0f;
        public float CurrentThreshold => _progressionModel != null ? _progressionModel.GetCurrentThreshold() : Mathf.Max(1f, _baseExperienceThreshold);
        public bool IsSelectionOpen { get; private set; }
        public IReadOnlyList<SkillData> CurrentChoices => _currentChoices;

        private void Awake()
        {
            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }

            if (_swipeAttackEventListener == null)
            {
                _swipeAttackEventListener = GetComponent<SwipeAttackEventListener>();
            }

            ResolveSkillDefinitions();
            _effectApplier = new CardEffectApplier(_playerStatSystem, _swipeAttackEventListener, this);
            _progressionModel = new LevelProgressionModel(_baseExperienceThreshold, _thresholdGrowthFactor);
            NotifyExperienceChanged();
        }

        // 시작 스킬은 Start에서 부여한다. 다른 컴포넌트의 OnEnable(구독)이 모두 끝난 뒤여야
        // OnSkillEffectPending 구독자(예: FanAttackSkill)가 레벨 1 부여를 놓치지 않는다.
        // 카드 풀도 여기서 만든다: PlayerStatSystem.Awake가 끝나야 이번 런의 캐릭터가 확정된다(#66).
        // 구독자가 OnEnable에서 먼저 건 추첨 조건(AddCardEligibilityFilter)은 이 시점에 함께 반영된다.
        private void Start()
        {
            InitializeCardPool();
            GrantStartingSkills();
        }

        private void OnDisable()
        {
            if (IsSelectionOpen)
            {
                ResumeGameTime();
                IsSelectionOpen = false;
                // 선택지가 열린 채 비활성화되면 닫힘 이벤트를 발화해, 이를 구독하는 측(예: 입력 비활성화
                // 게이트, HUD)이 억제/표시 상태를 정리하도록 한다. 이벤트가 없으면 씬 전환 없는 재시작
                // 등에서 입력이 영구 차단될 수 있다(#42).
                OnLevelSelectionClosed?.Invoke(CurrentLevel);
            }
        }

        public void AddExperience(float amount)
        {
            if (_progressionModel == null)
            {
                return;
            }

            bool leveledUp = _progressionModel.AddExperience(amount);
            NotifyExperienceChanged();

            if (leveledUp && !IsSelectionOpen)
            {
                OpenNextSelection();
            }
        }

        public bool ApplyChoice(int choiceIndex)
        {
            if (!IsSelectionOpen)
            {
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= _currentChoices.Count)
            {
                return false;
            }

            SkillData selected = _currentChoices[choiceIndex];
            ApplySkillEffect(selected);

            int newLevel = _skillLevels.Increment(selected.SkillId);
            OnSkillApplied?.Invoke(selected, newLevel);

            IsSelectionOpen = false;
            _currentChoices.Clear();
            OnLevelSelectionClosed?.Invoke(CurrentLevel);
            ResumeGameTime();

            OpenNextSelection();
            return true;
        }

        /// <summary>
        /// 경험치/레벨업과 무관하게 스킬 선택 1회를 큐에 추가한다.
        /// 미니 보스 처치 트리거에서 호출한다.
        /// </summary>
        public void AddGuaranteedSkillSelection()
        {
            if (_progressionModel == null)
            {
                return;
            }

            _progressionModel.AddGuaranteedSelection();

            if (!IsSelectionOpen)
            {
                OpenNextSelection();
            }
        }

        /// <summary>해당 카드의 현재 보유 레벨(미보유 = 0). 카드 풀의 최대 레벨 제외 / 가중치 판정에 쓰인다.</summary>
        public int GetSkillLevel(string skillId) => _skillLevels.GetLevel(skillId);

        /// <summary>
        /// CharacterData.StartingSkills를 레벨 1로 부여한다(예: 무당 = 부채살 흩뿌리기, #76).
        /// 레벨업 풀(BuildRandomChoices)은 GetSkillLevel을 보므로, 부여 후엔 레벨 2부터 카드로 등장한다.
        /// 멱등: 이미 부여됐으면 재실행하지 않는다.
        /// </summary>
        public void GrantStartingSkills()
        {
            if (_startingSkillsGranted)
            {
                return;
            }

            _startingSkillsGranted = true;

            IReadOnlyList<SkillData> startingSkills = _playerStatSystem?.CharacterData?.StartingSkills;
            if (startingSkills == null)
            {
                return;
            }

            for (int i = 0; i < startingSkills.Count; i++)
            {
                GrantSkill(startingSkills[i]);
            }
        }

        private void GrantSkill(SkillData skill)
        {
            if (skill == null)
            {
                return;
            }

            if (!skill.IsValid(out string reason))
            {
                Debug.LogWarning($"[PlayerLevelSystem] 시작 스킬 '{skill.name}'이(가) 유효하지 않습니다. {reason}");
                return;
            }

            // 이미 보유 중이면 중복 부여하지 않는다.
            if (GetSkillLevel(skill.SkillId) > 0)
            {
                return;
            }

            // ApplySkillEffect는 미내장 효과(부채살 등)에 OnSkillEffectPending(skill, 1)을 발행하므로
            // 레벨 증가 전에 호출해 구독자가 레벨 1을 받게 한다(ApplyChoice와 동일한 순서).
            ApplySkillEffect(skill);
            int newLevel = _skillLevels.Increment(skill.SkillId);
            OnSkillApplied?.Invoke(skill, newLevel);
        }

        private void OpenNextSelection()
        {
            if (_progressionModel == null)
            {
                return;
            }

            if (!_progressionModel.TryConsumePendingLevelUp())
            {
                return;
            }

            BuildRandomChoices();
            if (_currentChoices.Count <= 0)
            {
                OpenNextSelection();
                return;
            }

            IsSelectionOpen = true;
            PauseGameTime();
            OnLevelSelectionOpened?.Invoke(CurrentLevel, _currentChoices);
        }

        /// <summary>
        /// 이번 런의 카드 풀을 만든다(#66). 캐릭터 전용 카드 필터링과 유효성 검사는 여기서 한 번만 수행하고,
        /// 최대 레벨 제외 / 가중치는 매 추첨 시점에 <see cref="CardPool{T}.Draw"/>가 다시 계산한다.
        /// </summary>
        public void InitializeCardPool()
        {
            string characterId = _playerStatSystem != null && _playerStatSystem.CharacterData != null
                ? _playerStatSystem.CharacterData.CharacterId
                : null;

            if (string.IsNullOrWhiteSpace(characterId))
            {
                Debug.LogWarning("[PlayerLevelSystem] 이번 런의 캐릭터를 확인할 수 없어 캐릭터 전용 카드 필터링을 건너뜁니다.", this);
            }

            // 추첨 조건은 이 컴포넌트가 소유하므로(_cardEligibilityFilters) 풀을 다시 만들어도 그대로 유지된다.
            _cardPool = new CardPool<SkillData>(EnumerateValidDefinitions(), characterId)
            {
                EligibilityFilter = _cardEligibilityFilters.Evaluate,
            };

            if (_cardPool.Count <= 0)
            {
                Debug.LogWarning("[PlayerLevelSystem] 카드 풀이 비어 있습니다. 레벨업 선택지가 표시되지 않습니다.", this);
            }
        }

        /// <summary>
        /// 추첨 시점에 카드를 추가로 걸러내는 조건을 등록한다(<see cref="CardEligibilityFilterSet{T}"/>).
        /// 지금 선택해도 적용할 수 없는 카드를 빼서 "빈 선택"을 막는다(예: <see cref="GangshinCardApplier"/>).
        /// 여기서 카드 풀을 만들지 않는다: 이 호출은 구독자의 OnEnable(= <see cref="Start"/> 이전)에서
        /// 일어나므로 풀을 미리 만들면 이중 생성이 되고 정의 유효성 경고도 두 번 찍힌다.
        ///
        /// 조건은 반드시 <b>메서드 그룹</b>으로 넘긴다. 람다는 호출마다 새 델리게이트가 만들어져
        /// <see cref="RemoveCardEligibilityFilter"/>가 실패하고, 비활성/재활성을 반복하면 조건이 쌓여
        /// AND 평가 결과가 조용히 좁아진다.
        /// </summary>
        public void AddCardEligibilityFilter(Func<SkillData, bool> filter)
        {
            if (!_cardEligibilityFilters.Add(filter))
            {
                Debug.LogWarning("[PlayerLevelSystem] 카드 추첨 조건 등록이 무시되었습니다(null이거나 이미 등록된 조건).", this);
            }
        }

        /// <summary>자신이 등록한 조건만 해제한다. 다른 등록자의 조건에는 영향을 주지 않는다.</summary>
        public void RemoveCardEligibilityFilter(Func<SkillData, bool> filter)
        {
            if (!_cardEligibilityFilters.Remove(filter))
            {
                Debug.LogWarning("[PlayerLevelSystem] 등록된 적 없는 카드 추첨 조건을 해제하려 했습니다. 람다로 등록하면 해제되지 않으니 메서드 그룹으로 넘기세요.", this);
            }
        }

        /// <summary>
        /// 이 효과 타입을 처리하는 시스템이 활성화되었음을 등록한다(담당 컴포넌트의 OnEnable).
        /// 판정 이유는 <see cref="SkillEffectHandlerRegistry"/> 참조.
        /// </summary>
        public void RegisterEffectHandler(LevelUpSkillEffectType effectType) => _effectHandlers.Register(effectType);

        /// <summary>담당 시스템이 비활성화될 때 등록을 해제한다(OnDisable).</summary>
        public void UnregisterEffectHandler(LevelUpSkillEffectType effectType) => _effectHandlers.Unregister(effectType);

        private void BuildRandomChoices()
        {
            EnsureCardPool();
            _cardPool.Draw(GetSkillLevel, _currentChoices);
        }

        private void EnsureCardPool()
        {
            if (_cardPool == null)
            {
                InitializeCardPool();
            }
        }

        /// <summary>null / 유효하지 않은 정의를 경고와 함께 걸러낸다. 풀 생성 시 1회만 순회한다.</summary>
        private IEnumerable<SkillData> EnumerateValidDefinitions()
        {
            for (int i = 0; i < _skillDefinitions.Count; i++)
            {
                SkillData definition = _skillDefinitions[i];
                if (definition == null)
                {
                    Debug.LogWarning("[PlayerLevelSystem] SkillData list contains a null entry.", this);
                    continue;
                }

                if (!definition.IsValid(out string reason))
                {
                    Debug.LogWarning($"[PlayerLevelSystem] SkillData '{definition.name}' is invalid. {reason}", this);
                    continue;
                }

                yield return definition;
            }
        }

        private void ApplySkillEffect(SkillData definition)
        {
            if (_effectApplier != null && _effectApplier.TryApply(definition))
            {
                return;
            }

            // 전담 시스템(도깨비불 / 결계 / 강신 등)이 처리해야 하는 효과.
            OnSkillEffectPending?.Invoke(definition, GetSkillLevel(definition.SkillId) + 1);

            // 판정 기준은 "구독자가 있는가"가 아니라 "이 타입을 처리하는 시스템이 등록되어 있는가"다.
            // 구독자는 자기 타입이 아니면 무시하므로, 구독자 유무로 보면 담당 컴포넌트가 씬에서 빠져도
            // 경고 없이 지나간다(카드는 뜨는데 아무 일도 일어나지 않는 상태).
            if (!_effectHandlers.IsHandled(definition.EffectType))
            {
                Debug.LogWarning($"[PlayerLevelSystem] ApplySkillEffect: '{definition.SkillId}'의 효과 타입 {definition.EffectType}을(를) 처리할 시스템이 없습니다. CardEffectApplier에 케이스를 추가하거나, 담당 컴포넌트가 씬에 있고 RegisterEffectHandler를 호출하는지 확인하세요.", this);
            }
        }

        private void ResolveSkillDefinitions()
        {
            if (_skillDefinitions != null && _skillDefinitions.Count > 0)
            {
                return;
            }

            CharacterData characterData = _playerStatSystem?.CharacterData;
            if (characterData != null && characterData.LevelUpSkills != null && characterData.LevelUpSkills.Count > 0)
            {
                _skillDefinitions = new List<SkillData>(characterData.LevelUpSkills);
                return;
            }

            Debug.LogWarning("[PlayerLevelSystem] No SkillData configured. Level-up choices will be unavailable.");
        }

        private void NotifyExperienceChanged()
        {
            OnExperienceChanged?.Invoke(CurrentLevel, CurrentExperience, CurrentThreshold);
        }

        private void PauseGameTime()
        {
            if (!_pauseGameOnSelection)
            {
                return;
            }

            TimeScaleService.SetPause(PauseReason.LevelUpSelection, true);
        }

        private void ResumeGameTime()
        {
            if (!_pauseGameOnSelection)
            {
                return;
            }

            TimeScaleService.SetPause(PauseReason.LevelUpSelection, false);
        }
    }
}
