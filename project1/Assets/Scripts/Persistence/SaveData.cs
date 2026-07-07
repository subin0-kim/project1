using System;
using System.Collections.Generic;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// 메타 진행 영구 저장 데이터 모델(#33). JsonUtility로 직렬화되므로 필드는 public이며,
    /// 필드명이 곧 JSON 키가 된다 — 기존 필드명을 바꾸면 세이브 호환이 깨진다.
    /// 실제 저장 트리거(신당 구매/적립/해금/튜토리얼)는 각 소유 이슈(#34/#36/#61/#39)가 담당한다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>현재 세이브 스키마 버전. 필드 추가/구조 변경 시 올리고 SaveMigration에 단계를 추가한다.</summary>
        public const int CurrentVersion = 1;

        /// <summary>게임 시작부터 기본 해금되는 캐릭터(무당). CharacterData 에셋의 CharacterId와 일치해야 한다.</summary>
        public const string DefaultUnlockedCharacterId = "character.mudang";

        public int SaveDataVersion = CurrentVersion;

        /// <summary>누적 골드(영구 성장 재화).</summary>
        public long TotalGold;

        /// <summary>누적 영혼(콘텐츠 해금 재화). 인런 EXP '혼불'(Soul*)과 구분되는 'Spirit'.</summary>
        public long TotalSpirit;

        /// <summary>신당 업그레이드 ID → 레벨. 키는 ShrineUpgradeData(#34)가 정의한다.</summary>
        public SerializableStringIntMap UpgradeLevels = new SerializableStringIntMap();

        /// <summary>해금된 캐릭터 ID 목록. 기본값에 무당 포함.</summary>
        public List<string> UnlockedCharacters = new List<string>();

        /// <summary>영혼으로 해금한 신규 스킬 ID 목록(기본 11종 제외).</summary>
        public List<string> UnlockedSkills = new List<string>();

        /// <summary>튜토리얼 완료 여부(#39가 기록).</summary>
        public bool TutorialCompleted;

        /// <summary>신규 저장 파일의 초기 상태를 만든다(무당 기본 해금).</summary>
        public static SaveData CreateDefault()
        {
            return new SaveData
            {
                SaveDataVersion = CurrentVersion,
                TotalGold = 0,
                TotalSpirit = 0,
                UpgradeLevels = new SerializableStringIntMap(),
                UnlockedCharacters = new List<string> { DefaultUnlockedCharacterId },
                UnlockedSkills = new List<string>(),
                TutorialCompleted = false,
            };
        }
    }
}
