using Mukseon.Core;
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

            _soulCollector = SceneObjectFinder.Find<SoulCollector>();

            if (_soulCollector != null)
            {
                _soulCollector.OnSoulCollected += HandleSoulCollected;
                return;
            }

            // 이 컴포넌트는 게임플레이 씬에만 배치되고, 그 씬에는 혼불 수집기가 반드시 있어야 한다.
            // 없으면 배선 오류이며, 조용히 넘어가면 결과 화면의 혼불 수치만 0으로 나와 원인을 찾기 어렵다.
            Debug.LogWarning("[RunStatsTracker] SoulCollector를 찾을 수 없습니다. 혼불 수집 집계가 동작하지 않습니다.", this);
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
