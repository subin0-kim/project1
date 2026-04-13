using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.VFX
{
    /// <summary>
    /// ObjectPool과 연동되는 파티클 이펙트 컴포넌트.
    /// OnEnable 시 자동 재생하고, 파티클이 종료되면 콜백을 통해 풀에 반환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public class InkParticleEffect : MonoBehaviour
    {
        private ParticleSystem _ps;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();

            var main = _ps.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        private void OnEnable()
        {
            _ps.Play(true);
        }

        private void OnParticleSystemStopped()
        {
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
