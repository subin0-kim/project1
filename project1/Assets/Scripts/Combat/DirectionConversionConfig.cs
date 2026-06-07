using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 방향 속성 동적 변환 시스템 설정(#68, `direction_dynamic_system.md`).
    /// 누적 타격 카운트별 방향 변환 확률 테이블과 변환 임박 연출 시작 임계값을 정의한다.
    /// 에셋이 지정되지 않아도 동작하도록 정적 디폴트 폴백을 제공한다.
    /// 보스/미니 보스 전용 확률 테이블은 후속 작업(선택적)에서 다룬다.
    /// </summary>
    [CreateAssetMenu(fileName = "DirectionConversionConfig", menuName = "Mukseon/Data/Direction Conversion Config")]
    public class DirectionConversionConfig : ScriptableObject
    {
        // 디폴트 확률 곡선(초안, direction_dynamic_system.md §확률 곡선).
        // index 0 = 1회차 타격, 마지막 = "N회 이상". 플레이테스트 후 조정 예정.
        private static readonly float[] DefaultProbabilities = { 0f, 0.10f, 0.25f, 0.50f, 0.75f };
        private const int DefaultImminentThreshold = 2;

        [SerializeField]
        [Tooltip("누적 타격 수별 방향 변환 확률(0~1). index 0 = 1회차. 카운트가 길이를 넘으면 마지막 값을 사용한다.")]
        private float[] _conversionProbabilities = { 0f, 0.10f, 0.25f, 0.50f, 0.75f };

        [SerializeField, Min(1)]
        [Tooltip("변환 임박 연출(흔들림/깜빡임)을 시작하는 누적 타격 카운트.")]
        private int _imminentThresholdCount = DefaultImminentThreshold;

        /// <summary>변환 임박 연출을 시작하는 누적 타격 카운트(1 이상).</summary>
        public int ImminentThresholdCount => Mathf.Max(1, _imminentThresholdCount);

        /// <summary>누적 타격 카운트(1 이상)에 대응하는 방향 변환 확률(0~1)을 반환한다.</summary>
        public float GetConversionChance(int hitCount)
        {
            return Resolve(_conversionProbabilities, hitCount);
        }

        /// <summary>에셋이 없을 때 사용하는 정적 디폴트 변환 확률.</summary>
        public static float DefaultConversionChance(int hitCount)
        {
            return Resolve(DefaultProbabilities, hitCount);
        }

        /// <summary>설정이 없을 때 사용하는 정적 디폴트 임박 임계값.</summary>
        public static int DefaultImminentThresholdCount => DefaultImminentThreshold;

        private static float Resolve(float[] table, int hitCount)
        {
            if (table == null || table.Length == 0 || hitCount <= 0)
            {
                return 0f;
            }

            // 카운트가 테이블 길이를 넘어서면 마지막 값("N회 이상")으로 클램핑한다.
            int index = Mathf.Min(hitCount, table.Length) - 1;
            return Mathf.Clamp01(table[index]);
        }
    }
}
