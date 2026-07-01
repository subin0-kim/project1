using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 파천의 징 [박수 기본 강신, #30]. 먹물 파동이 중앙에서 바깥으로 뻗어나가며 전체 피해 + 기절(Stun).
    /// Lv3에서는 파동을 2회 연속 발사한다(2번째 파동은 잠시 딜레이 후, 기절 중인 적에 적중해 대량 피해).
    ///
    /// 이슈 문서상 명칭은 GangshinAbility_Baksoo이나, 프로젝트 네이밍 규칙(클래스 PascalCase)에 맞춰
    /// GangshinAbilityBaksoo로 정의한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GangshinAbilityBaksoo : GangshinAbilityBase
    {
        [Header("Wave")]
        [SerializeField, Min(0.1f), Tooltip("파동이 도달하는 최대 반경(월드 유닛). 화면을 덮을 만큼 크게.")]
        private float _maxRadius = 12f;

        [SerializeField, Min(0.05f), Tooltip("파동이 최대 반경까지 확장되는 데 걸리는 시간(초).")]
        private float _waveDuration = 0.4f;

        [SerializeField, Min(0.01f), Tooltip("Lv3 2번째 파동 발사 전 딜레이(초).")]
        private float _secondWaveDelay = 0.35f;

        [Header("VFX (선택)")]
        [SerializeField, Tooltip("파동을 시각화하는 흰 원 SpriteRenderer(플레이어 자식). 반경에 맞춰 스케일된다.")]
        private SpriteRenderer _waveRing;

        // 발동 컨텍스트 및 현재 레벨 수치.
        private GangshinSlotContext _context;
        private float _damage;
        private float _stunDuration;

        // 파동 진행 상태.
        private readonly HashSet<EnemyHealth> _hitSet = new HashSet<EnemyHealth>();
        private bool _waveActive;
        private float _elapsed;
        private float _radius;
        private int _wavesRemaining;
        private float _pendingDelay;

        internal float CurrentRadius => _radius;
        internal bool IsWaveActive => _waveActive;
        internal int WavesRemaining => _wavesRemaining;

        public override void Activate(GangshinSlotContext context)
        {
            _context = context;
            GangshinAbilityLevel level = _data != null ? _data.GetLevel(context.Level) : default;
            _damage = level.Damage;
            _stunDuration = level.StunDuration;

            // Lv3(DoubleWave)은 2회, 그 외는 1회 발사.
            _wavesRemaining = level.DoubleWave ? 2 : 1;
            _pendingDelay = 0f;

            StartWave();
        }

        private void Update()
        {
            if (!_waveActive && _pendingDelay <= 0f)
            {
                return;
            }

            // 파동은 게임 월드와 동일하게 스케일 시간으로 확장한다(발동 중 슬로우 연출과 일관).
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);

            // Lv3 2번째 파동 대기: 딜레이가 끝나면 새 파동을 시작한다.
            if (_pendingDelay > 0f)
            {
                _pendingDelay -= deltaTime;
                if (_pendingDelay <= 0f && _wavesRemaining > 0)
                {
                    StartWave();
                }

                return;
            }

            if (!_waveActive)
            {
                return;
            }

            _elapsed += deltaTime;
            float progress = _waveDuration > 0f ? Mathf.Clamp01(_elapsed / _waveDuration) : 1f;
            _radius = _maxRadius * progress;

            // 확장하는 파면(원점~_radius)에 새로 들어온 적에게만 데미지·기절을 적용한다.
            GangshinAbilityEffects.ApplyExpandingRing(
                _context.Origin, _radius, _damage, _stunDuration, _context.Enemies, _hitSet, _context.Source);

            UpdateRingVisual(progress);

            if (progress >= 1f)
            {
                EndWave();
            }
        }

        private void StartWave()
        {
            _hitSet.Clear();
            _elapsed = 0f;
            _radius = 0f;
            _waveActive = true;
            _wavesRemaining--;

            if (_waveRing != null)
            {
                _waveRing.enabled = true;
            }
        }

        private void EndWave()
        {
            _waveActive = false;

            if (_wavesRemaining > 0)
            {
                // 남은 파동(Lv3 2번째)을 잠시 딜레이 후 발사한다.
                _pendingDelay = Mathf.Max(0.01f, _secondWaveDelay);
                return;
            }

            if (_waveRing != null)
            {
                _waveRing.enabled = false;
            }
        }

        /// <summary>흰 원 파동 비주얼을 현재 반경(지름 = 2*반경)에 맞춰 스케일하고, 파면이 나아갈수록 옅게 페이드한다.</summary>
        private void UpdateRingVisual(float progress)
        {
            if (_waveRing == null || _waveRing.sprite == null)
            {
                return;
            }

            float spriteWorldSize = _waveRing.sprite.bounds.size.x;
            if (spriteWorldSize <= 0.0001f)
            {
                return;
            }

            // 부모(플레이어) 스케일을 보정해 월드 지름이 정확히 2*반경이 되도록 한다.
            Transform parent = _waveRing.transform.parent;
            float parentScale = parent != null ? parent.lossyScale.x : 1f;
            if (Mathf.Abs(parentScale) < 0.0001f)
            {
                parentScale = 1f;
            }

            float scale = (_radius * 2f) / (spriteWorldSize * parentScale);
            _waveRing.transform.localScale = new Vector3(scale, scale, 1f);

            Color color = _waveRing.color;
            color.a = Mathf.Lerp(1f, 0f, progress);
            _waveRing.color = color;
        }
    }
}
