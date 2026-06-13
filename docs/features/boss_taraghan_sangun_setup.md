# 타락한 산군 보스전 — Unity 에디터 셋업 가이드 (#69, 1페이즈)

> 이 문서는 #69 1페이즈 핵심 구현의 **C# 시스템을 씬/프리팹/데이터에 연결**하는 수작업 절차다.
> 스크립트·ScriptableObject 클래스·EditMode 테스트는 코드로 완료되어 있으며, 아래 에셋 생성과
> 컴포넌트 연결, 위치/수치 튜닝은 Unity 에디터에서 진행한다(플레이테스트 기반 조율).

관련 스크립트(모두 `Assets/Scripts/Combat/`):
`MountainKingBossController` · `MountainKingBossPatternData`(+`BossPhaseDefinition`) ·
`BossPatternDefinition` · `BossPatternType` · `BossCounterType` ·
`BossPatternIndicator` · `MountainKingMinionSpawner`.

---

## 1. 부하 호랑이 (Minion) 에셋

기존 적 셋업(`Settings/Data/Monsters/Monster_*.asset` + `Prefabs/Enemies/*`)을 미러링한다.

1. **MonsterData 생성**: `Assets/Settings/Data/Monsters/`에서
   `Create → Mukseon/Data/Monster Data` → 이름 `Monster_CorruptedTiger`.
   - `IsBoss = false` (중요 — 비보스여야 스폰 시 단일 방향 속성이 랜덤 배정된다).
   - `MaxHealth` / `MoveSpeed`: 잡몹 수준(플레이테스트로 조정, 초안 HP 3~5).
   - `SoulDropCount` / `ExperiencePerOrb`: 일반 잡몹 기준.
2. **미니언 프리팹 생성**: `Assets/Prefabs/Enemies/`에 `Minion_CorruptedTiger.prefab`.
   - 구성: `SpriteRenderer`(스프라이트 = `Art/Placeholders/CorruptedTiger.png`) +
     `BoxCollider2D` + `EnemyHealth` + `EnemyMover`(패턴 `TrackPlayer`) +
     `EnemyDirectionColorView`(선택) + `EnemySoulDropper`(선택).
   - `EnemyHealth._monsterData` = `Monster_CorruptedTiger`.
   - `MonsterData._enemyPrefab` = 이 프리팹(서로 참조). 일반 적 프리팹과 동일한 패턴.
3. **풀 등록**: 씬의 `PoolManager` 프리셋에 `Minion_CorruptedTiger` 추가(InitialSize 5, MaxSize 10 권장).

---

## 2. 패턴 데이터 (MountainKingBossPatternData)

`Assets/Settings/Data/BossData/`에서 `Create → Mukseon/Data/MountainKing Boss Pattern Data`
→ `BossPatternData_CorruptedMountainKing` (기존 `BossData_CorruptedMountainKing` 관례 따름).

- **Phases[0] (1페이즈)** — `CycleIntervalMin/Max = 8 / 10`. Patterns 3개:

  | Type | CounterType | Telegraph | Execute | Recover | CounterWindow | UnhandledDamage | CounterBonusDamage | MinionCount | IndicatorOffset |
  |------|-------------|-----------|---------|---------|---------------|-----------------|--------------------|-------------|-----------------|
  | Charge(돌진) | BossDirection | 1.0 | 0.5 | 0.5 | 0(=예고+발동) | 15 | 25 | 0 | 보스 진입 경로상 (튜닝) |
  | ClawSwipe(발톱) | PatternDirection | 1.2 | 0.4 | 0.5 | 0 | 12 | 20 | 0 | 발톱이 뻗는 위치 (튜닝) |
  | Roar(포효) | None | 1.5 | 0.5 | 0.8 | 0 | 0 | 0 | **3** | 보스 머리 위 (튜닝) |

- **Counter** — `CounterBonusMultiplier = 1.5`.
- **Positioning** — `OffscreenMargin` / `OnscreenMargin` / `ApproachSpeed` / `RetreatSpeed`
  는 카메라 크기에 맞춰 튜닝(초안: 2 / 2 / 12 / 10).
- **PhaseTransition** — `PhaseTransitionCutsceneSeconds = 2` (사용은 2페이즈 커밋).

> `IndicatorOffset`은 **코드에 하드코딩하지 않는다.** 아트 완성 후 각 패턴의 "공격 준비 위치"에
> 맞춰 이 값만 조정한다.

> 2페이즈(Phases[1])는 후속 커밋에서 채운다. 비워두면 `GetPhase`가 1페이즈로 폴백한다.

---

## 3. 패턴 인디케이터 프리팹

`Assets/Prefabs/`에 `BossPatternIndicator.prefab`.
- `SpriteRenderer`(화살표/색 구슬 스프라이트, 임시 플레이스홀더 가능) + `BossPatternIndicator`.
- `BossPatternIndicator._spriteRenderer` 연결, `_palette` = 기존 `DirectionColorPalette` 에셋
  (일반 적과 동일 팔레트 재사용).
- `PoolManager` 프리셋에 등록(InitialSize 1~2).

---

## 4. 보스 프리팹 / 씬 연결

**보스 프리팹** `Prefabs/Enemies/Boss_CorruptedMountainKing.prefab`에
`MountainKingBossController` 컴포넌트 추가(이미 `EnemyHealth` + `BossHealthComponent` +
`EnemyAttackSequence` 존재):
- `_patternData` = 2번에서 만든 SO.
- `_indicatorPrefab` = 3번 인디케이터 프리팹.
- `_homeOnRightSide` = 보스를 둘 방향(true=우측).
- 나머지 참조(`_encounterDirector` / `_swipeInputDetector` / `_minionSpawner` / `_playerHealth` /
  `_camera`)는 비워두면 런타임에 `FindAnyObjectByType`로 자동 해석된다. 명시 연결을 권장.

**씬 오브젝트**:
- 빈 GameObject `MountainKingMinionSpawner` 추가 후 컴포넌트 부착:
  - `_minionMonsterData` = `Monster_CorruptedTiger`.
  - `_leftSpawnPoints` / `_rightSpawnPoints`: 화면 좌/우 밖에 각 3개(상/중/하) 빈 Transform을
    배치해 연결(총 6개). 화면 바깥에 두는 것이 핵심.
  - `_camera` = 메인 카메라(좌/우 판정 기준).
- 기존 `BossEncounterDirector`는 #37대로 동작. `OnBossEncounterStarted`가 컨트롤러 전투를 시작한다.

---

## 5. 검증

- **EditMode 테스트**: `BossPatternDefinitionTests` / `MountainKingBossPatternDataTests` /
  `MountainKingBossControllerTests` / `MountainKingMinionSpawnerTests` 그린 확인 (Test Runner).
- **인게임**(에셋 연결 후):
  1. 보스 등장 → 화면 밖 대기 시 스와이프로 타격되지 않음.
  2. 8~10초 주기로 돌진/발톱/포효 발동, 진입 시 본체 타격 가능.
  3. 카운터 입력(돌진=보스 방향, 발톱=인디케이터 방향) 성공 시 패턴 취소 + 보스 보너스 피해(×1.5).
  4. 카운터 실패 시 플레이어 피해 수령(포효 제외).
  5. 포효 시 보스와 같은 방향 포인트에서 부하 3마리 소환, 전멸 전까지 다른 패턴 정지.

---

## 후속 커밋 (이슈 #69, 같은 브랜치)

- 2페이즈 패턴: 연속 할퀴기(시퀀스 카운터 + 인디케이터 순차 활성화), 광란 돌진, 포효 강화(5마리).
- 1페이즈 패턴의 2페이즈 강화, 2페이즈 전환 연출(무적, `OnPhaseChanged` 훅 활용).
