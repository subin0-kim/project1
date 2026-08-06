using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Core
{
    /// <summary>
    /// 씬을 넘어 "이번 런의 설정"을 나르는 홀더(#36). 캐릭터 선택 화면이 값을 채우고, 게임플레이 씬의
    /// <see cref="PlayerStatSystem"/>이 <c>Awake</c>에서 읽어간다.
    ///
    /// 씬 로드 후 외부에서 <c>PlayerStatSystem</c>에 주입하는 방식은 <c>Awake</c> 실행 순서에 의존해
    /// 깨지기 쉽다. 반대로 <c>Awake</c>가 이 static을 <b>읽어가게</b> 하면 순서 문제가 성립하지 않는다.
    /// 비어 있으면(게임플레이 씬을 에디터에서 단독 실행한 경우) 씬에 직렬화된 값으로 폴백한다.
    /// </summary>
    public static class RunContext
    {
        /// <summary>이번 런에 선택된 캐릭터. null이면 씬에 직렬화된 캐릭터를 쓴다.</summary>
        public static CharacterData SelectedCharacter { get; set; }

        /// <summary>
        /// 이번 런에 선택된 챕터(#64). null이면 게임플레이 씬의 디렉터들이 각자 직렬화된 값으로 폴백한다.
        /// 캐릭터와 같은 규약이다 — 스테이지 선택 화면이 채우고, 웨이브·미니 보스·보스 디렉터가 읽어간다.
        /// </summary>
        public static ChapterData SelectedChapter { get; set; }

        /// <summary>
        /// Enter Play Mode 설정에서 Domain Reload가 꺼져 있으면 static이 세션 간 유지된다.
        /// 이전 세션의 선택이 남아 다음 플레이에 새어 들어가지 않도록 진입 시 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            SelectedCharacter = null;
            SelectedChapter = null;
        }
    }
}
