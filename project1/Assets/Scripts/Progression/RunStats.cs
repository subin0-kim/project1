using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 한 번의 런에서 누적되는 결산 지표(#36). 결과 화면이 표시할 값을 담는다.
    /// MonoBehaviour에 의존하지 않는 순수 로직이라 단위 테스트가 용이하다.
    /// </summary>
    public sealed class RunStats
    {
        /// <summary>처치한 적 수.</summary>
        public int KillCount { get; private set; }

        /// <summary>생존 시간(초). 정지 중에는 누적되지 않는다.</summary>
        public float SurvivalSeconds { get; private set; }

        /// <summary>수집한 혼불(인런 경험치) 총량. 메타 재화 '영혼'(Spirit)과는 다르다.</summary>
        public int SoulCollected { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            SurvivalSeconds += deltaSeconds;
        }

        public void RegisterKill()
        {
            KillCount++;
        }

        public void RegisterSoul(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SoulCollected += amount;
        }

        public void Reset()
        {
            KillCount = 0;
            SurvivalSeconds = 0f;
            SoulCollected = 0;
        }

        /// <summary>생존 시간을 <c>M:SS</c>로 표기한다(10분 런 기준이라 시(hour) 단위는 두지 않는다).</summary>
        public static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
