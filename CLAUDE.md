# 묵선(墨線) — CLAUDE.md

## 프로젝트 개요
한국 무속 신앙(다크 판타지) + 수묵화(Ink Wash Painting) 아트를 결합한 모바일 로그라이트 액션 디펜스 게임.
뱀파이어 서바이버(Vampire Survivors) 장르의 성장 문법을 따르되, 플레이어는 이동하지 않고 제자리에서 스와이프(Swipe) 컨트롤로 적을 막아낸다.

---

## 핵심 게임플레이 — 반드시 인지할 것

- **플레이어 고정:** 캐릭터는 화면 정중앙에 고정. `Transform.position`은 절대 변하지 않는다.
- **전투:** 터치 시작점 → 끝점의 방향 벡터를 계산해 해당 방향으로 투사체(먹선)를 발사한다.
- **적 스폰:** Camera Viewport 바깥 360도 전방위에서 생성되어 플레이어(중앙)를 향해 이동.
- **성장:** 적 처치 → 경험치 아이템(혼불) 드롭 → 자석 범위 내 자동 수집 → 레벨업 시 게임 일시정지(`Time.timeScale = 0`) + 랜덤 스킬 3개 선택 UI 표시.

---

## 기술 스택

- **엔진:** Unity 6.3 LTS
- **언어:** C#
- **플랫폼:** Android / iOS, 세로 모드(Portrait) 기준

---

## 아키텍처 규칙 — 절대 규칙

### Object Pooling (필수)
런타임에 생성/삭제가 빈번한 오브젝트(몬스터, 투사체, 경험치 아이템, 파티클, 데미지 텍스트)는 반드시 `UnityEngine.Pool.ObjectPool`을 사용한다.
**`Instantiate` / `Destroy` 실시간 호출 금지.**

### Scriptable Objects
다음 데이터는 모두 SO로 관리한다:
- 몬스터 기본 스탯 (체력, 속도)
- 플레이어 스탯
- 스킬 데이터 (데미지 계수, 쿨타임, 설명 텍스트)
- 웨이브 진행 데이터

### 이벤트 기반 아키텍처
시스템 간 결합도를 낮추기 위해 C# `event`, `Action`, `Func`를 적극 활용한다.
싱글턴(Singleton)은 `GameManager`, `AudioManager` 등 최소한으로만 허용한다.

```csharp
// 예시: 적 사망 이벤트
public static event Action<EnemyData> OnEnemyDeath;
// Spawner, UI 등이 각각 구독하여 처리
```

### State Machine (FSM)
- **몬스터 AI:** 추적 → 공격 → 기믹 시전 → 사망
- **게임 진행:** 로비 → 플레이 중 → 일시정지 → 레벨업 → 게임오버

### 물리 처리
수많은 몬스터 이동 시 `Rigidbody2D` 동적 충돌은 사용하지 않는다.
- **이동:** `Transform.Translate` 또는 `Vector2.MoveTowards` (Kinematic)
- **충돌 감지:** `Physics2D.OverlapCircle` 또는 `Collider2D` Trigger

---

## 코딩 컨벤션

### 네임스페이스
모든 스크립트는 네임스페이스로 묶는다:
- `Mukseon.Core`
- `Mukseon.Combat`
- `Mukseon.UI`

### 네이밍 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스 / 구조체 / 열거형 / 메서드 / 프로퍼티 | PascalCase | `PlayerController`, `CalculateDamage` |
| public 필드 | PascalCase | `public float MaxHealth` |
| [SerializeField] private 필드 | _camelCase | `[SerializeField] private int _currentHealth` |
| private / protected 변수 | _camelCase | `private float _speed` |
| 상수 / readonly | PascalCase | `public const float MaxSwipeDistance = 10f` |

> 인스펙터 노출 변수는 `[SerializeField] private` 원칙. `public` 필드 사용 최소화.

---

## 작업 규칙 — Claude Code 행동 지침

- **모듈화:** 스크립트 1개당 300줄 이하. 단일 책임 원칙(SRP) 준수.
  - `Player` 대신 `PlayerInput`, `PlayerHealth`, `PlayerAttack`으로 분리할 것.
- **한 번에 하나씩:** 요청된 기능에만 집중. 요청하지 않은 시스템을 미리 구현하지 말 것.
- **테스트 용이성:** `MonoBehaviour` 의존 최소화. 비즈니스 로직은 순수 C# 클래스로 분리.
- **하드코딩 금지:** 밸런스 수치는 반드시 ScriptableObject 또는 상수로 분리.
- **주석:** 복잡한 수학 계산(방향 벡터, 내적/외적, 스폰 알고리즘)과 핵심 로직에는 **한국어**로 작동 원리 주석을 필수로 작성한다.

---

## 작업 흐름

```
기획 문서 (planning doc, 구현 무관)
    → GitHub Issues (구현 티켓)
        → Claude Code 구현
            → pre-commit hook 검증
                → PR 및 리뷰
```

> 기획 문서에 코드 블록을 포함하지 않는 것을 원칙으로 한다.
