using Mukseon.Core.Input;
using Mukseon.Core.Pool;
using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.Gameplay.VFX
{
    /// <summary>
    /// 부채살 흩뿌리기(#76) 발동 시각 연출.
    /// <see cref="SwipeAttackEventListener.OnFanAttackTriggered"/>를 구독해, 스와이프 방향을
    /// 중심으로 부채꼴(방사각) 안에 먹선 갈래들을 펼쳐 스폰한다. 각 갈래는 랜덤 먹선 패턴으로
    /// 바깥을 향해 뻗어나간다. 레벨별 갈래 수/방사각은 기획 수치(3/4/5갈래, 60/70/80°)를 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FanAttackVFX : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SwipeAttackEventListener _swipeAttackEventListener;

        [SerializeField, Tooltip("갈래 1개 프리팹 (InkLineBranchVFX 보유)")]
        private GameObject _branchPrefab;

        [SerializeField, Tooltip("먹선 패턴 스프라이트들 — 갈래마다 랜덤 선택")]
        private Sprite[] _strokeSprites;

        [Header("Per-Level Visuals (index 0 = Lv1)")]
        [SerializeField, Tooltip("레벨별 갈래 수(시각 전용). 게임플레이 타격 방향은 FanAttackPattern.BuildBranches가 별도로 결정하므로, 레벨 수치 조정 시 양쪽을 함께 맞춰야 한다.")]
        private int[] _branchCountPerLevel = { 3, 4, 5 };

        [SerializeField, Tooltip("레벨별 방사각(도, 시각 전용). FanAttackPattern 수치와 연동해 관리.")]
        private float[] _spreadAnglePerLevel = { 60f, 70f, 80f };

        [Header("Branch Appearance")]
        [SerializeField]
        private Color _inkColor = new Color(0.05f, 0.03f, 0.03f, 0.92f);

        [SerializeField, Tooltip("갈래 길이 범위(월드 유닛)")]
        private Vector2 _lengthRange = new Vector2(1.9f, 2.6f);

        [SerializeField, Tooltip("갈래 두께 범위(월드 유닛)")]
        private Vector2 _thicknessRange = new Vector2(0.4f, 0.55f);

        [SerializeField, Min(0f), Tooltip("플레이어 중심에서 갈래 시작점을 바깥으로 밀어내는 거리")]
        private float _innerOffset = 0.2f;

        [SerializeField, Range(0f, 20f), Tooltip("갈래별 각도 흔들림(도) — 유기적인 흩뿌림")]
        private float _angleJitter = 4f;

        private void Awake()
        {
            if (_swipeAttackEventListener == null)
            {
                _swipeAttackEventListener = GetComponent<SwipeAttackEventListener>();
            }
        }

        private void OnEnable()
        {
            if (_swipeAttackEventListener != null)
            {
                _swipeAttackEventListener.OnFanAttackTriggered += HandleFanTriggered;
            }
        }

        private void OnDisable()
        {
            if (_swipeAttackEventListener != null)
            {
                _swipeAttackEventListener.OnFanAttackTriggered -= HandleFanTriggered;
            }
        }

        private void HandleFanTriggered(SwipeDirection direction, Vector2 origin, int level)
        {
            if (_branchPrefab == null)
            {
                return;
            }

            int count = ResolvePerLevel(_branchCountPerLevel, level, 3);
            float spread = ResolvePerLevel(_spreadAnglePerLevel, level, 60f);
            if (count <= 0)
            {
                return;
            }

            float baseAngle = BaseAngleFor(direction);

            for (int i = 0; i < count; i++)
            {
                float angle = BranchAngle(baseAngle, i, count, spread)
                    + Random.Range(-_angleJitter, _angleJitter);

                SpawnBranch(origin, angle);
            }
        }

        private void SpawnBranch(Vector2 origin, float angleDegrees)
        {
            Vector2 dir = new Vector2(Mathf.Cos(angleDegrees * Mathf.Deg2Rad), Mathf.Sin(angleDegrees * Mathf.Deg2Rad));
            Vector3 spawnPos = (Vector3)(origin + dir * _innerOffset);
            Quaternion rotation = Quaternion.Euler(0f, 0f, angleDegrees);

            GameObject branch = AcquireBranch(spawnPos, rotation);
            if (branch == null)
            {
                return;
            }

            var line = branch.GetComponent<InkLineBranchVFX>();
            if (line != null)
            {
                Sprite sprite = PickSprite();
                float length = Random.Range(_lengthRange.x, _lengthRange.y);
                float thickness = Random.Range(_thicknessRange.x, _thicknessRange.y);
                line.Configure(sprite, length, thickness, _inkColor);
            }

            branch.SetActive(true);
        }

        private GameObject AcquireBranch(Vector3 position, Quaternion rotation)
        {
            // 비활성으로 꺼내 Configure 후 직접 활성화해야 OnEnable 연출이 올바른 값으로 시작한다.
            if (PoolManager.Instance != null)
            {
                return PoolManager.Instance.GetInactive(_branchPrefab, position, rotation);
            }

            GameObject obj = Instantiate(_branchPrefab, position, rotation);
            obj.SetActive(false);
            return obj;
        }

        private Sprite PickSprite()
        {
            if (_strokeSprites == null || _strokeSprites.Length == 0)
            {
                return null;
            }

            return _strokeSprites[Random.Range(0, _strokeSprites.Length)];
        }

        /// <summary>스와이프 방향에 해당하는 기준 각도(도, +X=0, CCW). 부채꼴의 중심.</summary>
        public static float BaseAngleFor(SwipeDirection direction)
        {
            switch (direction)
            {
                case SwipeDirection.Right: return 0f;
                case SwipeDirection.Up: return 90f;
                case SwipeDirection.Left: return 180f;
                case SwipeDirection.Down: return 270f;
                default: return 0f;
            }
        }

        /// <summary>부채꼴 안에서 index번째 갈래의 각도(도). count=1이면 중심.</summary>
        public static float BranchAngle(float baseAngle, int index, int count, float spread)
        {
            if (count <= 1)
            {
                return baseAngle;
            }

            float t = index / (float)(count - 1); // 0..1
            return baseAngle + Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
        }

        private static T ResolvePerLevel<T>(T[] perLevel, int level, T fallback)
        {
            if (perLevel == null || perLevel.Length == 0)
            {
                return fallback;
            }

            int index = Mathf.Clamp(level - 1, 0, perLevel.Length - 1);
            return perLevel[index];
        }
    }
}
