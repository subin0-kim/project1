# 6. 시스템 아키텍처 및 데이터 구조 (Architecture & Data)

## 6.1 핵심 아키텍처 원칙 (Architecture Principles)
1인 개발과 AI 코딩 어시스턴트(Cursor, Copilot) 활용에 최적화된 아키텍처입니다.
- **이벤트 기반 통신 (Event-Driven)**: 
  - UI 업데이트(체력바, 신력 게이지) 및 게임 상태 변화(게임오버, 레벨업)는 C# `event`나 `UnityEvent`를 활용한 옵저버 패턴으로 관리합니다.
  - 이렇게 결합도를 낮추면 향후 새로운 UI나 시스템을 추가할 때 기존 코드를 수정할 일이 크게 줄어듭니다.
- **오브젝트 풀링 (Object Pooling)**: 
  - 생성/파괴가 안 그래도 빈번한 게임(스와이프 먹선, 적 캐릭터, 경험치 혼불, 타격 먹물 파티클 등)이므로, `UnityEngine.Pool.ObjectPool`을 사용해 프레임 드랍을 원천 차단합니다.
  - 게임 플레이 도중 `Instantiate`나 `Destroy` 사용을 절대 지양합니다.

## 6.2 주요 데이터 구조 (Data Structure - Scriptable Objects)
게임 데이터는 하드코딩하지 않고 Scriptable Object(SO)로 관리하여 유지비용을 줄입니다.
- **MonsterData (몬스터 스탯)**:
  - 변수: `MonsterName`, `MaxHP`, `MoveSpeed`, `PatternType`, `DropSoulAmount`, `Prefab`
- **SkillData (11개 스킬 시스템)**:
  - 변수: `SkillName`, `Description`, `MaxLevel`, `Icon`, `BaseDamage`, `Cooldown`
- **CharacterData (무당/박수 스탯)**:
  - 변수: `CharacterName`, `BaseHP`, `AttackPower`, `AbilityGaugeMultiplier`, `UniqueSkillData`

## 6.3 세이브 및 로드 (Save & Load)
신당(로비)에서의 영구 성장 요소 저장을 위한 구조입니다.
- **저장 방식**: 빠르고 쉬운 구현을 위해 `JSON` 직렬화 후 `Application.persistentDataPath`에 저장합니다.
- **저장 데이터 모델 (`SaveData.cs`)**:
  - `TotalGold` (누적 금화)
  - `TotalSouls` (누적 영혼)
  - `UpgradeLevels` (Dictionary 형태, 예: `{"MaxHP": 3, "MagnetRadius": 1}`)
  - `UnlockedCharacters` (무당 기본 활성화, 박수 구매 여부 등)
