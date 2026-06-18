namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 도깨비불 소환(#72)의 공유 재소환 쿨타임 클럭 — 순수 로직(테스트 가능).
    ///
    /// 규칙:
    /// - 드론이 처음 돌진을 시작(타깃 락온)하면 쿨타임이 시작된다.
    /// - 쿨타임이 경과하면 "소비된(자폭 후 숨김)" 드론을 한 번에 재소환해야 함을 알린다(<see cref="Tick"/> 반환 true).
    /// - 경과 시점에 아직 돌진 중인 드론이 있으면 다음 주기를 이어가, 그 드론들이 자폭한 뒤에도 재소환되게 한다.
    /// - 돌진 중인 드론이 없으면 클럭을 멈춘다(주변에 적이 없어 전체가 궤도만 돌면 재소환하지 않음).
    ///
    /// 실제 어떤 드론을 재소환할지(소비된 드론만)는 호출자가 결정한다. 이 클럭은 "언제 일괄 재소환할지"만 판정한다.
    /// </summary>
    public sealed class DokkaebiOrbResummonClock
    {
        private bool _running;
        private float _timer;

        public bool IsRunning => _running;
        public float Remaining => _timer;

        /// <summary>
        /// 한 프레임 진행한다.
        /// <paramref name="anyDroneCharging"/>: 현재 돌진(비행) 중인 드론이 하나라도 있는지.
        /// <paramref name="cooldown"/>: 현재 레벨의 재소환 쿨타임(초).
        /// 반환값이 true면 소비된 드론을 일괄 재소환해야 한다.
        /// </summary>
        public bool Tick(float deltaTime, bool anyDroneCharging, float cooldown)
        {
            if (!_running)
            {
                // 첫 교전(돌진 시작) 시 쿨타임을 시작한다. 시작 프레임에는 차감하지 않는다.
                if (anyDroneCharging)
                {
                    _timer = cooldown;
                    _running = true;
                }

                return false;
            }

            _timer -= deltaTime;
            if (_timer > 0f)
            {
                return false;
            }

            // 쿨타임 경과 — 일괄 재소환 신호. 아직 비행 중인 드론이 있으면 다음 주기를 잇고, 없으면 멈춘다.
            if (anyDroneCharging)
            {
                // 음수로 누적된 오버슈트(저프레임 등 deltaTime이 쿨타임을 초과)를 이월해 장기적으로 주기가 밀리지 않게 한다.
                _timer += cooldown;
            }
            else
            {
                _running = false;
            }

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
