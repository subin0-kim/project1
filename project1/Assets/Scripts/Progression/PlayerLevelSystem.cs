using System;
using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    public enum LevelUpSkillEffectType
    {
        StatFlat = 0,
        StatPercent = 1,
        BonusTargets = 2,
        PickupRadius = 3
    }

    [DisallowMultipleComponent]
    public class PlayerLevelSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField]
        private SwipeAttackEventListener _swipeAttackEventListener;

        [SerializeField]
        private SoulCollector _soulCollector;

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

        private readonly List<SkillData> _currentChoices = new List<SkillData>(3);
        private readonly Dictionary<string, int> _skillLevels = new Dictionary<string, int>();

        private LevelProgressionModel _progressionModel;
        private float _timeScaleBeforePause = 1f;

        public event Action<int, float, float> OnExperienceChanged;
        public event Action<int, IReadOnlyList<SkillData>> OnLevelSelectionOpened;
        public event Action<int> OnLevelSelectionClosed;
        public event Action<SkillData, int> OnSkillApplied;

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

            if (_soulCollector == null)
            {
                _soulCollector = GetComponent<SoulCollector>();
            }

            ResolveSkillDefinitions();
            _progressionModel = new LevelProgressionModel(_baseExperienceThreshold, _thresholdGrowthFactor);
            NotifyExperienceChanged();
        }

        private void OnDisable()
        {
            if (IsSelectionOpen)
            {
                ResumeGameTime();
                IsSelectionOpen = false;
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

            int newLevel = IncrementSkillLevel(selected.SkillId);
            OnSkillApplied?.Invoke(selected, newLevel);

            IsSelectionOpen = false;
            _currentChoices.Clear();
            OnLevelSelectionClosed?.Invoke(CurrentLevel);
            ResumeGameTime();

            OpenNextSelection();
            return true;
        }

        public int GetSkillLevel(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return 0;
            }

            return _skillLevels.TryGetValue(skillId, out int level) ? level : 0;
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

        private void BuildRandomChoices()
        {
            _currentChoices.Clear();

            var candidates = new List<SkillData>(_skillDefinitions.Count);
            for (int i = 0; i < _skillDefinitions.Count; i++)
            {
                SkillData definition = _skillDefinitions[i];
                if (definition == null)
                {
                    Debug.LogWarning("[PlayerLevelSystem] SkillData list contains a null entry.");
                    continue;
                }

                if (!definition.IsValid(out string reason))
                {
                    Debug.LogWarning($"[PlayerLevelSystem] SkillData '{definition.name}' is invalid. {reason}");
                    continue;
                }

                if (GetSkillLevel(definition.SkillId) >= definition.MaxLevel)
                {
                    continue;
                }

                candidates.Add(definition);
            }

            if (candidates.Count <= 0)
            {
                return;
            }

            Shuffle(candidates);
            int choiceCount = Mathf.Min(3, candidates.Count);
            for (int i = 0; i < choiceCount; i++)
            {
                _currentChoices.Add(candidates[i]);
            }
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = 0; i < list.Count - 1; i++)
            {
                int swapIndex = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }

        private void ApplySkillEffect(SkillData definition)
        {
            switch (definition.EffectType)
            {
                case LevelUpSkillEffectType.StatFlat:
                    if (_playerStatSystem != null)
                    {
                        _playerStatSystem.AddModifier(definition.StatType, new StatModifier(definition.Value, StatModifierType.Flat, this));
                    }
                    break;
                case LevelUpSkillEffectType.StatPercent:
                    if (_playerStatSystem != null)
                    {
                        _playerStatSystem.AddModifier(definition.StatType, new StatModifier(definition.Value, StatModifierType.Percent, this));
                    }
                    break;
                case LevelUpSkillEffectType.BonusTargets:
                    if (_swipeAttackEventListener != null)
                    {
                        _swipeAttackEventListener.AddBonusTargets(Mathf.RoundToInt(definition.Value));
                    }
                    break;
                case LevelUpSkillEffectType.PickupRadius:
                    if (_soulCollector != null)
                    {
                        _soulCollector.AddPickupRadius(definition.Value);
                    }
                    break;
            }
        }

        private int IncrementSkillLevel(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return 0;
            }

            int previous = GetSkillLevel(skillId);
            int current = previous + 1;
            _skillLevels[skillId] = current;
            return current;
        }

        private void ResolveSkillDefinitions()
        {
            if (_skillDefinitions != null && _skillDefinitions.Count > 0)
            {
                return;
            }

            CharacterData characterData = _playerStatSystem != null ? _playerStatSystem.CharacterData : null;
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

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }

        private void ResumeGameTime()
        {
            if (!_pauseGameOnSelection)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforePause <= 0f ? 1f : _timeScaleBeforePause;
            _timeScaleBeforePause = 1f;
        }
    }
}
