using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    public class LevelProgressionModel
    {
        private readonly float _baseThreshold;
        private readonly float _growthFactor;

        public LevelProgressionModel(float baseThreshold, float growthFactor, int startLevel = 1)
        {
            _baseThreshold = Mathf.Max(1f, baseThreshold);
            _growthFactor = Mathf.Max(1f, growthFactor);
            Level = Mathf.Max(1, startLevel);
            CurrentExperience = 0f;
            PendingLevelUps = 0;
        }

        public int Level { get; private set; }
        public float CurrentExperience { get; private set; }
        public int PendingLevelUps { get; private set; }

        public float GetCurrentThreshold()
        {
            int levelOffset = Mathf.Max(0, Level - 1);
            return _baseThreshold * Mathf.Pow(_growthFactor, levelOffset);
        }

        public bool AddExperience(float amount)
        {
            float validAmount = Mathf.Max(0f, amount);
            if (validAmount <= 0f)
            {
                return false;
            }

            CurrentExperience += validAmount;
            bool leveledUp = false;

            float currentThreshold = GetCurrentThreshold();
            while (CurrentExperience >= currentThreshold)
            {
                CurrentExperience -= currentThreshold;
                Level++;
                PendingLevelUps++;
                leveledUp = true;
                currentThreshold = GetCurrentThreshold();
            }

            return leveledUp;
        }

        public bool TryConsumePendingLevelUp()
        {
            if (PendingLevelUps <= 0)
            {
                return false;
            }

            PendingLevelUps--;
            return true;
        }

        /// <summary>
        /// 경험치/레벨업과 무관하게 스킬 선택 1회를 큐에 추가한다.
        /// 미니 보스 처치 등 외부 트리거에 의한 보장 선택에 사용한다.
        /// </summary>
        public void AddGuaranteedSelection()
        {
            PendingLevelUps++;
        }
    }
}
