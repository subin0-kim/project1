using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 선택 가능한 챕터 목록(#64). 구조는 <c>CharacterDatabase</c>(#36)와 같다 —
    /// 스테이지 선택 화면이 이 목록을 순회해 카드를 만들고, 세이브의 해금 정보와 대조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterDatabase", menuName = "Mukseon/Data/Chapter Database")]
    public class ChapterDatabase : ScriptableObject
    {
        [SerializeField]
        private List<ChapterData> _chapters = new List<ChapterData>();

        /// <summary>표시 순서대로의 챕터 목록(읽기 전용). null 항목이 섞여 있을 수 있으므로 사용 측에서 걸러야 한다.</summary>
        public IReadOnlyList<ChapterData> Chapters => _chapters;

        /// <summary>이번 런의 기본 챕터(1장). 목록이 비어 있으면 null.</summary>
        public ChapterData DefaultChapter
        {
            get
            {
                for (int i = 0; i < _chapters.Count; i++)
                {
                    if (_chapters[i] != null)
                    {
                        return _chapters[i];
                    }
                }

                return null;
            }
        }

        /// <summary>ID로 챕터를 찾는다. 없으면 null.</summary>
        public ChapterData Find(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return null;
            }

            for (int i = 0; i < _chapters.Count; i++)
            {
                ChapterData chapter = _chapters[i];
                if (chapter != null && chapter.ChapterId == chapterId)
                {
                    return chapter;
                }
            }

            return null;
        }

        /// <summary>
        /// 목록 자체의 무결성만 본다(빈 항목 · 중복 ID). 각 챕터 내부의 유효성은
        /// <see cref="ChapterData.IsValid"/>가 따로 판정한다 — 2·3장처럼 아직 골격만 있는 챕터가
        /// 목록 검증을 통째로 실패시키면, 플레이 가능한 1장까지 화면에서 사라진다.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (_chapters.Count == 0)
            {
                reason = "챕터 목록이 비어 있습니다.";
                return false;
            }

            var seenIds = new HashSet<string>();
            for (int i = 0; i < _chapters.Count; i++)
            {
                ChapterData chapter = _chapters[i];
                if (chapter == null)
                {
                    reason = $"{i}번 항목이 비어 있습니다.";
                    return false;
                }

                if (!seenIds.Add(chapter.ChapterId))
                {
                    reason = $"ChapterId가 중복됩니다: '{chapter.ChapterId}'";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
