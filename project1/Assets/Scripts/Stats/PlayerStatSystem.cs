using System;
using System.Collections.Generic;
using Mukseon.Core;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [DisallowMultipleComponent]
    public class PlayerStatSystem : MonoBehaviour
    {
        [SerializeField]
        private CharacterData _characterData;

        [SerializeField]
        private PlayerStatsDefinition _initialStats;

        private readonly Dictionary<StatType, RuntimeStat> _runtimeStats = new Dictionary<StatType, RuntimeStat>();

        // 이번 런에 실제로 적용된 캐릭터. InitializeFromDefinition에서 확정된다(#36).
        private CharacterData _activeCharacter;

        public event Action<StatType, float> OnStatChanged;

        /// <summary>
        /// 이번 런에 적용된 캐릭터. 선택 화면을 거쳤으면 선택된 캐릭터, 아니면 씬에 직렬화된 캐릭터다.
        /// 스탯뿐 아니라 기본 공격력(<see cref="Combat.SwipeAttackEventListener"/>)과
        /// 시작·레벨업 스킬 풀(<see cref="Progression.PlayerLevelSystem"/>)도 이 값을 읽으므로,
        /// 스탯과 같은 출처로 해석되어야 캐릭터가 반쪽만 바뀌는 일이 없다.
        /// </summary>
        public CharacterData CharacterData => _activeCharacter != null ? _activeCharacter : _characterData;

        private void Awake()
        {
            InitializeFromDefinition();
        }

        public void InitializeFromDefinition()
        {
            _runtimeStats.Clear();
            _activeCharacter = ResolveCharacterData();

            PlayerStatsDefinition sourceDefinition = ResolveInitialStatsDefinition();
            if (sourceDefinition == null)
            {
                Debug.LogWarning("[PlayerStatSystem] Initial stat definition is missing.");
                return;
            }

            for (int i = 0; i < sourceDefinition.Stats.Count; i++)
            {
                StatValueDefinition definition = sourceDefinition.Stats[i];
                _runtimeStats[definition.StatType] = new RuntimeStat(definition.BaseValue);
            }
        }

        /// <summary>
        /// 선택 화면에서 고른 캐릭터를 우선하고, 없으면 씬에 직렬화된 캐릭터로 폴백한다.
        /// 폴백 덕분에 게임플레이 씬을 에디터에서 단독 실행해도 종전처럼 동작한다(#36).
        /// </summary>
        private CharacterData ResolveCharacterData()
        {
            return RunContext.SelectedCharacter != null ? RunContext.SelectedCharacter : _characterData;
        }

        private PlayerStatsDefinition ResolveInitialStatsDefinition()
        {
            CharacterData character = CharacterData;
            if (character != null)
            {
                if (character.InitialStats == null)
                {
                    Debug.LogWarning($"[PlayerStatSystem] CharacterData '{character.name}' is missing InitialStats.");
                }
                else
                {
                    return character.InitialStats;
                }
            }

            return _initialStats;
        }

        internal bool TryGetRuntimeStat(StatType statType, out RuntimeStat runtimeStat)
        {
            return _runtimeStats.TryGetValue(statType, out runtimeStat);
        }

        public float GetValue(StatType statType)
        {
            return _runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat) ? runtimeStat.Value : 0f;
        }

        /// <summary>
        /// 지정 시스템에서 스탯값을 조회하되, 시스템이 없거나 스탯이 미정의(값 0)면 폴백값을 반환한다(#40).
        /// 여러 컴포넌트(SoulCollector/SwipeSoulPuller/GangshinController 등)의 폴백 조회 패턴을 한 곳으로 모은다.
        /// </summary>
        public static float ResolveValueOrDefault(PlayerStatSystem system, StatType statType, float fallback)
        {
            if (system == null)
            {
                return fallback;
            }

            float value = system.GetValue(statType);
            return value > 0f ? value : fallback;
        }

        public bool AddModifier(StatType statType, StatModifier modifier)
        {
            if (!_runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat))
            {
                return false;
            }

            runtimeStat.AddModifier(modifier);
            OnStatChanged?.Invoke(statType, runtimeStat.Value);
            return true;
        }

        public bool RemoveModifier(StatType statType, StatModifier modifier)
        {
            if (!_runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat))
            {
                return false;
            }

            bool removed = runtimeStat.RemoveModifier(modifier);
            if (removed)
            {
                OnStatChanged?.Invoke(statType, runtimeStat.Value);
            }

            return removed;
        }

        public int RemoveModifiersFromSource(StatType statType, object source)
        {
            if (!_runtimeStats.TryGetValue(statType, out RuntimeStat runtimeStat))
            {
                return 0;
            }

            int removed = runtimeStat.RemoveAllModifiersFromSource(source);
            if (removed > 0)
            {
                OnStatChanged?.Invoke(statType, runtimeStat.Value);
            }

            return removed;
        }
    }
}
