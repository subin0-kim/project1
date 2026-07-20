using UnityEngine;

namespace Mukseon.Core
{
    /// <summary>
    /// 씬에 배치된 컴포넌트를 이름이 아닌 타입으로 찾아오는 공용 헬퍼(PR #110 리뷰 반영).
    ///
    /// 수동 배선 없이 서로를 찾아 붙는 컴포넌트들(HUD 부트스트래퍼·입력 게이트·결과 화면)이 각자
    /// 같은 구현을 복사해 갖고 있었다. 버전 분기(<c>FindObjectOfType</c> 폐기)가 섞여 있어
    /// 복사본이 늘수록 한 곳만 고치고 나머지를 놓치기 쉬우므로 한 곳으로 모은다.
    /// </summary>
    public static class SceneObjectFinder
    {
        /// <summary>
        /// 씬에서 <typeparamref name="T"/> 하나를 찾는다. 비활성 오브젝트도 포함한다 —
        /// 결과 화면처럼 평소 꺼져 있다가 필요할 때 켜지는 컴포넌트가 대상이기 때문이다.
        /// </summary>
        public static T Find<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
