using System.Collections.Generic;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// 로드된 세이브 데이터를 현재 스키마 버전으로 승격/정규화한다(#33).
    /// 구버전 필드 보강, null 컬렉션 방어, 기본 해금 캐릭터 보장을 담당한다.
    /// 향후 필드 구조가 바뀌면 버전별 단계 마이그레이션을 이곳에 추가한다.
    /// </summary>
    public static class SaveMigration
    {
        public static SaveData Migrate(SaveData data)
        {
            if (data == null)
            {
                return SaveData.CreateDefault();
            }

            // JsonUtility 역직렬화나 구버전 파일에서 누락될 수 있는 컬렉션을 방어적으로 초기화한다.
            if (data.UpgradeLevels == null)
            {
                data.UpgradeLevels = new SerializableStringIntMap();
            }
            if (data.UnlockedCharacters == null)
            {
                data.UnlockedCharacters = new List<string>();
            }
            if (data.UnlockedSkills == null)
            {
                data.UnlockedSkills = new List<string>();
            }
            if (data.DirectionColors == null)
            {
                data.DirectionColors = new DirectionColorOverrides();
            }

            // 버전별 단계 마이그레이션 지점.
            if (data.SaveDataVersion < SaveData.CurrentVersion)
            {
                // v1 → v2: 방향 색상 설정 필드 추가(#83).
                // v1 파일에는 해당 키가 아예 없으므로, JsonUtility가 값을 덮어쓰지 않아 필드 초기값이
                // 그대로 남는 것이 정상이다. 다만 손상된 파일이 0을 넣어 '글로우 전용'으로 시작하는
                // 사고를 막기 위해, 구버전으로 판별된 데이터는 여기서 명시적으로 기본값을 세운다.
                if (data.SaveDataVersion < 2)
                {
                    data.DirectionDisplayMode = (int)Core.DirectionDisplayMode.Both;
                    data.DirectionArrowAssist = false;
                    data.DirectionColors.Clear();
                }

                data.SaveDataVersion = SaveData.CurrentVersion;
            }

            // 무당은 항상 기본 해금 상태를 보장한다.
            if (!data.UnlockedCharacters.Contains(SaveData.DefaultUnlockedCharacterId))
            {
                data.UnlockedCharacters.Add(SaveData.DefaultUnlockedCharacterId);
            }

            return data;
        }
    }
}
