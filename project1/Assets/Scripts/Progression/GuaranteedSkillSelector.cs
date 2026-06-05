using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 미니 보스 처치 시 보장 스킬 선택 1회를 PlayerLevelSystem 큐에 추가한다.
    /// 레벨업 선택과 동일한 큐(PendingLevelUps)를 공유하므로 처리 순서가 자동으로 보장된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GuaranteedSkillSelector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private MiniBossSpawner _miniBossSpawner;

        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        private void OnEnable()
        {
            if (_miniBossSpawner != null)
            {
                _miniBossSpawner.OnMiniBossDefeated += HandleMiniBossDefeated;
            }
        }

        private void OnDisable()
        {
            if (_miniBossSpawner != null)
            {
                _miniBossSpawner.OnMiniBossDefeated -= HandleMiniBossDefeated;
            }
        }

        private void HandleMiniBossDefeated(int stageIndex, MiniBossStageData stageData)
        {
            if (_playerLevelSystem == null)
            {
                Debug.LogWarning("[GuaranteedSkillSelector] PlayerLevelSystem 참조가 없습니다.");
                return;
            }

            _playerLevelSystem.AddGuaranteedSkillSelection();
        }
    }
}
