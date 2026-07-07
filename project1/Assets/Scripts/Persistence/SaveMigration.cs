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

            // 버전별 단계 마이그레이션 지점. 현재는 v1이 최신이라 버전 승격만 수행한다.
            if (data.SaveDataVersion < SaveData.CurrentVersion)
            {
                // 예: if (data.SaveDataVersion < 2) { /* v1 → v2 필드 변환 */ }
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
