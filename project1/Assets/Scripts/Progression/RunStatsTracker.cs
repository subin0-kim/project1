using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 게임플레이 씬에 배치되어 런 결산 지표를 모으는 수집기(#36). 결과 화면이 이 컴포넌트를 찾아 값을 읽는다.
    /// 집계 로직 자체는 <see cref="RunStats"/>(순수 C#)에 있고, 여기서는 이벤트 구독과 틱만 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunStatsTracker : MonoBehaviour
    {
        private readonly RunStats _stats = new RunStats();

        private SoulCollector _soulCollector;

        public RunStats Stats => _stats;

        private void OnEnable()
        {
            EnemyHealth.AnyEnemyDied += HandleEnemyDied;
        }

        private void OnDisable()
        {
            EnemyHealth.AnyEnemyDied -= HandleEnemyDied;

            if (_soulCollector != null)
            {
                _soulCollector.OnSoulCollected -= HandleSoulCollected;
                _soulCollector = null;
            }
        }

        // 혼불 수집기는 플레이어에 붙어 있어 씬 참조를 수동 배선하지 않고 찾아 붙는다(HUD와 같은 방식).
        // Start에서 찾는 이유는 다른 컴포넌트의 Awake가 모두 끝난 뒤여야 확실히 존재하기 때문이다.
        private void Start()
        {
            if (_soulCollector != null)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            _soulCollector = FindFirstObjectByType<SoulCollector>(FindObjectsInactive.Include);
#else
            _soulCollector = FindObjectOfType<SoulCollector>();
#endif

            if (_soulCollector != null)
            {
                _soulCollector.OnSoulCollected += HandleSoulCollected;
            }
        }

        // Time.deltaTime은 timeScale이 반영된 값이라, 게임오버·레벨업·화면 전환으로 정지된 동안에는
        // 0이 되어 생존 시간이 자동으로 멈춘다. 별도의 정지 판정이 필요 없다.
        private void Update()
        {
            _stats.Tick(Time.deltaTime);
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            _stats.RegisterKill();
        }

        private void HandleSoulCollected(int amount)
        {
            _stats.RegisterSoul(amount);
        }
    }
}
