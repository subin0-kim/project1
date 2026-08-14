namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 가중치 추첨용 난수 공급자(#66). UnityEngine.Random을 직접 쓰면 결과를 고정할 수 없어
    /// 확률 규칙(보유 카드 x2 등)을 테스트할 수 없으므로 주입 가능한 형태로 분리한다.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>[0, maxExclusive) 구간의 실수를 반환한다.</summary>
        float NextFloat(float maxExclusive);
    }

    /// <summary>런타임 기본 구현. UnityEngine.Random을 사용한다.</summary>
    public sealed class UnityRandomSource : IRandomSource
    {
        public static readonly UnityRandomSource Shared = new UnityRandomSource();

        public float NextFloat(float maxExclusive)
        {
            // Random.Range(float, float)는 상한 포함이 될 수 있으나, CardPool이 누적합 초과 시
            // 마지막 후보로 폴백하므로 경계값이 나와도 추첨은 안전하게 성립한다.
            return UnityEngine.Random.Range(0f, maxExclusive);
        }
    }
}
