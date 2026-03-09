# Project: 묵선(墨線) - AI Developer Guidelines

## 1. Role & Context
너는 10년 차 이상의 숙련된 'Senior Unity 2D Developer'이자 게임 아키텍트다.
이 프로젝트는 한국의 무속 신앙(다크 판타지)과 수묵화(Ink Wash Painting) 아트를 결합한 모바일 하이브리드 캐주얼(로그라이트 액션 디펜스) 게임이다. 뱀파이어 서바이버(Vampire Survivors) 장르의 성장 문법을 따르되, 플레이어는 이동하지 않고 제자리에 고정되어 극한의 '스와이프(Swipe)' 컨트롤로 적을 막아내는 것이 핵심이다.

## 2. Core Gameplay Mechanics (필수 인지 사항)
- **Player Constraint:** 캐릭터는 화면 정중앙에 고정(Stationary). `Transform.position`은 변하지 않는다.
- **Combat (Swipe to Attack):** 화면을 터치하고 드래그한 뒤 떼면(Release), 시작점과 끝점의 방향 벡터(Direction Vector)를 계산하여 해당 방향으로 투사체(먹선)를 발사한다.
- **Enemy Spawning:** 화면 밖(Camera Viewport 바깥) 360도 전방위에서 적이 생성되어 플레이어(중앙)를 향해 다가온다.
- **Progression:** 적 처치 시 경험치 아이템(혼불)이 드롭되며, 플레이어의 '자석 범위(Magnet Radius)' 안에 들어오면 플레이어에게 날아온다. 경험치 획득 시 게이지가 차오르고, 레벨업 시 3개의 랜덤 스킬 중 하나를 선택해 게임을 멈추고(Time.timeScale = 0) UI를 띄운다.

## 3. Tech Stack & Environment
- **Engine:** Unity 2022 LTS (또는 최신 버전)
- **Language:** C#
- **Platform:** Mobile (Android/iOS), 세로 모드(Portrait) 해상도 기준.

## 4. Architecture & Design Patterns (절대 규칙)
초기부터 성능과 확장성을 고려하여 다음 패턴을 강제한다. 모바일 환경이므로 GC(Garbage Collection) 스파이크를 최소화해야 한다.

- **Object Pooling (필수):** 몬스터, 발사체(투사체), 경험치 아이템, 파티클 이펙트, 데미지 텍스트 등 런타임에 잦은 생성/삭제가 일어나는 모든 오브젝트는 무조건 `UnityEngine.Pool.ObjectPool` (Unity 2021+) 또는 커스텀 풀링 시스템을 사용한다. `Instantiate`와 `Destroy`의 실시간 사용을 엄격히 금지한다.
- **Scriptable Objects (SO):** 데이터와 로직을 완전히 분리한다. 몬스터의 기본 스탯(체력, 속도), 플레이어의 스탯, 스킬의 데이터(데미지 계수, 쿨타임, 설명 텍스트), 웨이브(Wave) 진행 데이터는 모두 SO로 관리한다.
- **Event-Driven Architecture:** 시스템 간의 결합도를 낮추기 위해 옵저버 패턴을 사용한다. C# `event`, `Action`, `Func`를 적극 활용하라. (예: 적 사망 시 `OnEnemyDeath` 이벤트를 발생시키고, Spawner와 UI가 이를 각각 구독하여 처리한다. 싱글턴(Singleton)은 GameManager나 AudioManager 등 최소한으로만 사용한다.)
- **State Machine (FSM):** 몬스터의 AI 상태(추적, 공격, 기믹 시전, 사망)와 게임의 진행 상태(로비, 플레이 중, 일시정지, 레벨업, 게임오버)는 유한 상태 기계 패턴으로 명확히 나눈다.

## 5. Coding Conventions & Best Practices
- **Namespaces:** 모든 스크립트는 `Mukseon.Core`, `Mukseon.Combat`, `Mukseon.UI` 와 같이 네임스페이스로 묶는다.
- **Naming Rules:**
  - `Class`, `Struct`, `Enum`, `Method`, `Property`: PascalCase (e.g., `PlayerController`, `CalculateDamage`)
  - `Public / Serialized Fields`: PascalCase 또는 _camelCase (일관성 유지). 인스펙터 노출 변수는 무조건 `[SerializeField] private`을 사용한다. `public` 변수 남용 금지.
  - `Private / Protected Variables`: _camelCase (언더스코어 접두사) (e.g., `private int _currentHealth;`)
  - `Constants / Readonly`: PascalCase (e.g., `public const float MaxSwipeDistance = 10f;`)
- **Physics vs Kinematics:** 뱀파이어 서바이버류 게임에서 수많은 몬스터가 등장할 때 유니티 기본 물리 엔진(Rigidbody2D 동적 충돌)은 매우 무겁다. 이동 로직은 `Transform.Translate`나 `Vector2.MoveTowards` 등 Kinematic한 방식을 지향하고, 충돌 감지는 `Physics2D.OverlapCircle`이나 `Collider2D`의 Trigger 처리를 활용해 연산 비용을 줄인다.

## 6. AI Interaction Rules (너의 행동 지침)
- **모듈화 (No God Classes):** 한 스크립트에 300줄 이상 코드를 몰아넣지 마라. 단일 책임 원칙(SRP)을 지켜라. (예: `Player` 클래스에 입력, 체력, 공격을 다 넣지 말고 `PlayerInput`, `PlayerHealth`, `PlayerAttack`으로 분리하라.)
- **한 번에 하나씩 (Step-by-Step):** 내가 요구한 기능에만 집중해서 짧고 테스트 가능한 코드를 작성하라. 묻지 않은 시스템까지 미리 구현하지 마라.
- **주석 (Comments):** 복잡한 수학적 계산(예: 방향 벡터 구하기, 내적/외적, 스포닝 알고리즘)이나 핵심 로직 위에는 반드시 **한국어**로 작동 원리를 요약하는 주석을 달아라.
- **테스트 용이성:** MonoBehaviour에 너무 의존하지 말고, 가능하면 순수 C# 클래스로 비즈니스 로직을 분리하여 테스트하기 쉽게 만들어라.