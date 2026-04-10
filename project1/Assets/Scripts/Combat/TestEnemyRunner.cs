using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// EnemyManager 구현 전 테스트용 Runner.
    /// 씬에 하나만 배치하면 활성화된 모든 EnemyBase의 ManualUpdate()를 호출합니다.
    /// EnemyManager 구현 후 제거할 것.
    /// </summary>
    public class TestEnemyRunner : MonoBehaviour
    {
        private void Update()
        {
            var enemies = EnemyBase.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null)
                    enemies[i].ManualUpdate();
            }
        }
    }
}
