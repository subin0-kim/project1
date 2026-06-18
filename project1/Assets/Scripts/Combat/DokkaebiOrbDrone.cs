using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 도깨비불 소환(#72)의 드론 — 풀링되는 궤도 비행 오브젝트.
    /// 소유 스킬(<see cref="DokkaebiOrbSkill"/>)이 풀에서 꺼내 <see cref="Initialize"/>로 설정한 뒤 활성화한다.
    ///
    /// 상태:
    /// - <b>Orbit</b>: 플레이어(궤도 중심) 주변을 원형으로 비행하며 탐지 범위 내 가장 가까운 적을 탐색한다.
    /// - <b>Charging</b>: 타깃을 향해 돌진한다. 근접 도달 시 자폭.
    ///   돌진 중 타깃이 사라지면 <b>드론 위치 기준</b>으로 다른 적을 재탐색하고, 없으면 마지막 타깃 위치까지
    ///   계속 날아가 그 자리에서 자폭한다(궤도로 되돌아가지 않는다 — 여러 드론이 한 적을 노릴 때의 순간이동 방지).
    /// - <b>Consumed</b>: 자폭 후 모습을 감춘 채 스킬의 일괄 재소환(<see cref="Resummon"/>)을 기다린다.
    ///
    /// 재소환 쿨타임은 개별 드론이 아니라 스킬이 공유 클럭으로 관리한다(돌진 시작 시 시작, 경과 시 소비된 드론 일괄 재소환).
    /// 반경·데미지 등 수치는 매 프레임 소유 스킬에서 실시간 조회하므로 스킬 레벨업이 즉시 반영된다.
    /// 폭발 데미지는 공용 <see cref="RadialDamage.ApplyInRadius"/>로 적용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DokkaebiOrbDrone : MonoBehaviour
    {
        public enum DroneState
        {
            Orbit,
            Charging,
            Consumed,
        }

        [SerializeField, Tooltip("자폭 시 생성할 폭발 VFX 프리팹(선택). 풀링 대상.")]
        private GameObject _explosionVfxPrefab;

        private DokkaebiOrbSkill _skill;
        private DroneState _state;
        private float _phaseDeg;
        private EnemyHealth _target;
        private Vector2 _chargeTargetPos;

        private SpriteRenderer[] _renderers;
        private bool _renderersCached;

        /// <summary>현재 드론 상태. 스킬이 공유 쿨타임/일괄 재소환 판정에 사용한다.</summary>
        public DroneState State => _state;

        /// <summary>
        /// 풀에서 비활성으로 꺼낸 직후(활성화 전)에 호출해 드론을 설정한다.
        /// </summary>
        public void Initialize(DokkaebiOrbSkill skill, float phaseDeg)
        {
            _skill = skill;
            _phaseDeg = phaseDeg;
            _state = DroneState.Orbit;
            _target = null;

            SetVisible(true);
            SnapToOrbit();
        }

        /// <summary>소유 스킬이 드론 수 변동 시 궤도 위상을 균등 분배하기 위해 호출한다.</summary>
        public void SetOrbitPhase(float phaseDeg)
        {
            _phaseDeg = phaseDeg;
        }

        /// <summary>스킬이 공유 쿨타임 경과로 일괄 재소환할 때 호출 — 소비된 드론을 궤도로 되돌린다.</summary>
        public void Resummon()
        {
            _target = null;
            _state = DroneState.Orbit;
            SetVisible(true);
            SnapToOrbit();
        }

        private void OnDisable()
        {
            // 풀 반환·파괴 시 상태를 깨끗이 리셋해 재사용 시 오염을 막는다.
            _state = DroneState.Orbit;
            _target = null;
        }

        private void Update()
        {
            if (_skill == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            // 궤도 각도는 항상 진행시켜, 재소환 위치가 자연스럽게 분산되도록 한다.
            _phaseDeg = Mathf.Repeat(_phaseDeg + _skill.OrbitAngularSpeedDeg * dt, 360f);

            switch (_state)
            {
                case DroneState.Orbit:
                    TickOrbit();
                    break;
                case DroneState.Charging:
                    TickCharging(dt);
                    break;
                case DroneState.Consumed:
                    // 스킬의 일괄 재소환을 기다린다(자체 쿨타임 없음).
                    break;
            }
        }

        private void TickOrbit()
        {
            SnapToOrbit();

            // 탐지는 드론(도깨비불) 자신의 현재 위치를 기준으로 한다(돌진 중 재탐색과 동일 기준).
            EnemyHealth target = DokkaebiOrbTargeting.FindNearestTarget(
                transform.position, _skill.CurrentDetectRange, EnemyHealth.ActiveEnemies);

            if (target != null)
            {
                _target = target;
                _chargeTargetPos = target.transform.position;
                _state = DroneState.Charging;
            }
        }

        private void TickCharging(float dt)
        {
            Vector2 dronePos = transform.position;

            if (_target != null && _target.IsAlive && _target.IsTargetable)
            {
                // 타깃 추적 — 현재 위치를 갱신한다.
                _chargeTargetPos = _target.transform.position;
            }
            else
            {
                // 타깃 소실 — 드론 위치 기준으로 다른 적을 재탐색한다.
                // 없으면 _target=null로 두고 마지막 위치(_chargeTargetPos)까지 날아가 그 자리에서 자폭한다.
                EnemyHealth reacquired = DokkaebiOrbTargeting.FindNearestTarget(
                    dronePos, _skill.CurrentDetectRange, EnemyHealth.ActiveEnemies);

                if (reacquired != null)
                {
                    _target = reacquired;
                    _chargeTargetPos = reacquired.transform.position;
                }
                else
                {
                    _target = null;
                }
            }

            Vector2 toTarget = _chargeTargetPos - dronePos;
            float detonate = _skill.DetonateDistance;
            if (toTarget.sqrMagnitude <= detonate * detonate)
            {
                Detonate();
                return;
            }

            // MoveTowards로 목표 위치를 초과하지 않게 이동을 제한한다(저프레임/고속 돌진 시 오버슈트·진동 방지).
            Vector2 newPos = Vector2.MoveTowards(dronePos, _chargeTargetPos, _skill.ChargeSpeed * dt);
            transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
        }

        private void Detonate()
        {
            Vector2 explosionCenter = transform.position;
            RadialDamage.ApplyInRadius(
                explosionCenter,
                _skill.ExplosionRadius,
                _skill.CurrentExplosionDamage,
                EnemyHealth.ActiveEnemies,
                _skill);

            SpawnExplosionVfx(explosionCenter);

            _target = null;
            _state = DroneState.Consumed;
            SetVisible(false);
        }

        private void SpawnExplosionVfx(Vector2 position)
        {
            if (_explosionVfxPrefab == null)
            {
                return;
            }

            var pos = new Vector3(position.x, position.y, 0f);
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Get(_explosionVfxPrefab, pos, Quaternion.identity);
            }
            else
            {
                Instantiate(_explosionVfxPrefab, pos, Quaternion.identity);
            }
        }

        private void SnapToOrbit()
        {
            Vector2 center = OrbitCenterPosition;
            float rad = _phaseDeg * Mathf.Deg2Rad;
            float radius = _skill.OrbitRadius;
            transform.position = new Vector3(
                center.x + Mathf.Cos(rad) * radius,
                center.y + Mathf.Sin(rad) * radius,
                transform.position.z);
        }

        // _skill.OrbitCenter는 항상 non-null이다(스킬이 미지정 시 자기 transform으로 폴백).
        private Vector2 OrbitCenterPosition => _skill.OrbitCenter.position;

        private void SetVisible(bool visible)
        {
            if (!_renderersCached)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>(true);
                _renderersCached = true;
            }

            if (_renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }
    }
}
