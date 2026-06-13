using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 보스 패턴 파훼 인디케이터(#69). 일반 적의 머리 위 고정 표시(<see cref="EnemyDirectionColorView"/>)와
    /// 별개로, "공격이 준비되는 위치"에 동적으로 배치된다. 위치는 보스 기준 오프셋
    /// (<see cref="BossPatternDefinition.IndicatorOffset"/>)으로 컨트롤러가 계산해 <see cref="Show"/>로 전달한다
    /// — 인디케이터 자체는 위치를 하드코딩하지 않는다.
    /// 색은 일반 적과 동일한 <see cref="DirectionColorPalette"/>를 재사용한다. 오브젝트 풀링으로 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossPatternIndicator : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        private DirectionColorPalette _palette;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        /// <summary>지정 방향 색으로 주어진 월드 위치에 인디케이터를 표시한다.</summary>
        public void Show(SwipeDirection direction, Vector3 worldPosition)
        {
            transform.position = worldPosition;
            ApplyColor(direction);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>인디케이터를 숨긴다. 풀 반환은 호출자(컨트롤러)가 담당한다.</summary>
        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyColor(SwipeDirection direction)
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            Color color = DirectionColorPalette.Resolve(_palette, direction);
            color.a = _spriteRenderer.color.a <= 0f ? 1f : _spriteRenderer.color.a;
            _spriteRenderer.color = color;
        }
    }
}
