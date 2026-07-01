using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 살풀이 검무 [무당 기본 강신, #30]. 하얀 소창이 화면 전체를 휩쓸어 모든 적에게 즉시 데미지를 준다.
    /// Lv3에서 기절 효과가 추가된다(gangshin_balance_mvp.md).
    ///
    /// 이슈 문서상 명칭은 GangshinAbility_Mudang이나, 프로젝트 네이밍 규칙(클래스 PascalCase)에 맞춰
    /// GangshinAbilityMudang으로 정의한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GangshinAbilityMudang : GangshinAbilityBase
    {
        [Header("VFX (선택)")]
        [SerializeField, Tooltip("발동 시 잠시 표시되는 소창 스윕 연출. 미지정 시 스킵.")]
        private GameObject _sweepVfx;

        [SerializeField, Min(0f), Tooltip("소창 스윕 연출 표시 시간(초).")]
        private float _sweepVfxDuration = 0.6f;

        private float _vfxHideTimer;

        public override void Activate(GangshinSlotContext context)
        {
            GangshinAbilityLevel level = _data != null ? _data.GetLevel(context.Level) : default;

            // 화면 전체 적에게 데미지( + Lv3 기절)를 즉시 적용한다.
            GangshinAbilityEffects.ApplyToAll(context.Enemies, level.Damage, level.StunDuration, context.Source);

            PlaySweepVfx();
        }

        private void PlaySweepVfx()
        {
            if (_sweepVfx == null)
            {
                return;
            }

            // 지속시간이 0 이하면 타이머 기반 자동 비활성화가 동작하지 않아 VFX가 영구히 남는다.
            // 이 경우 켜지 않고 즉시 꺼둔다(설정 실수 방어).
            if (_sweepVfxDuration <= 0f)
            {
                _sweepVfx.SetActive(false);
                _vfxHideTimer = 0f;
                return;
            }

            _sweepVfx.SetActive(true);
            _vfxHideTimer = _sweepVfxDuration;
        }

        private void Update()
        {
            if (_vfxHideTimer <= 0f)
            {
                return;
            }

            // 강신 발동 중 Time.timeScale이 낮아지므로 unscaledDeltaTime으로 연출 길이를 일정하게 유지한다.
            _vfxHideTimer -= Time.unscaledDeltaTime;
            if (_vfxHideTimer <= 0f && _sweepVfx != null)
            {
                _sweepVfx.SetActive(false);
            }
        }
    }
}
