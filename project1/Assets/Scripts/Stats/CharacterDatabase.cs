using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    /// <summary>
    /// 선택 가능한 캐릭터 목록(#36). 캐릭터 선택 화면이 이 목록을 순회해 카드를 만들고,
    /// 세이브의 해금 ID(<c>SaveData.UnlockedCharacters</c>)와 <see cref="CharacterData.CharacterId"/>를
    /// 대조해 잠금 여부를 판정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Mukseon/Data/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [SerializeField]
        private List<CharacterData> _characters = new List<CharacterData>();

        /// <summary>표시 순서대로의 캐릭터 목록(읽기 전용). null 항목이 섞여 있을 수 있으므로 사용 측에서 걸러야 한다.</summary>
        public IReadOnlyList<CharacterData> Characters => _characters;

        /// <summary>ID로 캐릭터를 찾는다. 없으면 null.</summary>
        public CharacterData Find(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            for (int i = 0; i < _characters.Count; i++)
            {
                CharacterData character = _characters[i];
                if (character != null && character.CharacterId == characterId)
                {
                    return character;
                }
            }

            return null;
        }

        /// <summary>
        /// 에셋이 화면에 쓸 수 있는 상태인지 검사한다. null 항목·중복 ID는 선택 화면에서
        /// 조용히 잘못된 캐릭터를 고르게 만들 수 있으므로 로드 시점에 걸러낸다.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (_characters.Count == 0)
            {
                reason = "캐릭터 목록이 비어 있습니다.";
                return false;
            }

            var seenIds = new HashSet<string>();
            for (int i = 0; i < _characters.Count; i++)
            {
                CharacterData character = _characters[i];
                if (character == null)
                {
                    reason = $"{i}번 항목이 비어 있습니다.";
                    return false;
                }

                if (!seenIds.Add(character.CharacterId))
                {
                    reason = $"CharacterId가 중복됩니다: '{character.CharacterId}'";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
