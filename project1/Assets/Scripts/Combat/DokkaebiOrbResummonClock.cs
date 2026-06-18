namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 도깨비불 소환(#72)의 공유 재소환 쿨타임 클럭 — 순수 로직(테스트 가능).
    ///
    /// 규칙:
    /// - 궤도 정원이 부족해지면(드론이 돌진해 나갔거나 자폭으로 소멸) 쿨타임이 시작된다.
    /// - 쿨타임이 경과하면 궤도 정원까지 한 번에 보충해야 함을 알린다(<see cref="Tick"/> 반환 true).
    /// - 보충으로 정원이 다시 채워지면(부족 상태 해제) 클럭은 멈춘다.
    ///   주변에 적이 없어 전체가 궤도만 돌면(= 정원이 가득 차 보충이 필요 없으면) 시작하지 않는다.
    ///
    /// 어떤 드론을/몇 개를 보충할지는 호출자가 결정한다(비행 중인 드론은 정원 계산에서 제외).
    /// 이 클럭은 "언제 정원을 보충할지"만 판정한다.
    /// </summary>
    public sealed class DokkaebiOrbResummonClock
    {
        private bool _running;
        private float _timer;

        public bool IsRunning => _running;
        public float Remaining => _timer;

        /// <summary>
        /// 한 프레임 진행한다.
        /// <paramref name="replenishPending"/>: 궤도 정원이 부족해 보충이 필요한지(궤도 드론 수 &lt; 정원).
        /// <paramref name="cooldown"/>: 현재 레벨의 재소환 쿨타임(초).
        /// 반환값이 true면 궤도 정원까지 보충해야 한다.
        /// </summary>
        public bool Tick(float deltaTime, bool replenishPending, float cooldown)
        {
            // 보충할 필요가 없으면(정원이 가득) 클럭을 멈춘다. 부족해지는 순간 다시 시작한다.
            if (!replenishPending)
            {
                _running = false;
                return false;
            }

            if (!_running)
            {
                // 정원이 처음 부족해진 시점에 쿨타임을 시작한다(시작 프레임은 차감하지 않는다).
                _timer = cooldown;
                _running = true;
                return false;
            }

            _timer -= deltaTime;
            if (_timer > 0f)
            {
                return false;
            }

            // 쿨타임 경과 — 보충 신호. 발화 후 정지하며, 보충 뒤에도 여전히 부족하면 다음 프레임에 재시작한다.
            _running = false;
            return true;
        }

        /// <summary>클럭을 초기 상태로 되돌린다(스킬 비활성화 등).</summary>
        public void Reset()
        {
            _running = false;
            _timer = 0f;
        }
    }
}
