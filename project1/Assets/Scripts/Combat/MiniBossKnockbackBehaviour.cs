using System.Collections;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 미니 보스에 런타임으로 붙이는 누적 넉백 컴포넌트.
    /// 누적 데미지가 임계값에 도달할 때마다 플레이어 반대 방향으로 밀려나고 누적치를 리셋한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniBossKnockbackBehaviour : MonoBehaviour
    {
        private static readonly WaitForEndOfFrame _waitEndOfFrame = new WaitForEndOfFrame();

        private EnemyHealth _enemyHealth;
        private EnemyMover _mover;
        private float _knockbackThreshold;
        private float _knockbackDistance;
        private float _knockbackDuration;
        private Transform _playerTransform;
        private float _accumulatedDamage;
        private bool _isKnockingBack;

        public void Initialise(
            EnemyHealth enemyHealth,
            float knockbackThreshold,
            float knockbackDistance,
            float knockbackDuration,
            Transform playerTransform)
        {
            _enemyHealth = enemyHealth;
            _mover = GetComponent<EnemyMover>();
            _knockbackThreshold = Mathf.Max(0.01f, knockbackThreshold);
            _knockbackDistance = Mathf.Max(0f, knockbackDistance);
            _knockbackDuration = Mathf.Max(0.05f, knockbackDuration);
            _playerTransform = playerTransform;
            _accumulatedDamage = 0f;
            _isKnockingBack = false;

            _enemyHealth.OnDamagedDetailed += HandleDamaged;
            _enemyHealth.OnDeath += HandleDeath;
        }

        private void HandleDamaged(EnemyHealth enemy, float damage, object source)
        {
            if (_isKnockingBack)
            {
                return;
            }

            _accumulatedDamage += damage;
            if (_accumulatedDamage >= _knockbackThreshold)
            {
                _accumulatedDamage = 0f;
                StartCoroutine(KnockbackCoroutine());
            }
        }

        private IEnumerator KnockbackCoroutine()
        {
            _isKnockingBack = true;

            if (_mover != null)
            {
                _mover.enabled = false;
            }

            Vector3 awayDir = _playerTransform != null
                ? (transform.position - _playerTransform.position).normalized
                : Vector3.right;

            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + awayDir * _knockbackDistance;
            float elapsed = 0f;

            while (elapsed < _knockbackDuration)
            {
                float t = elapsed / _knockbackDuration;
                // 초반에 빠르게 밀리고 끝에서 감속
                transform.position = Vector3.Lerp(startPos, endPos, t * t * (3f - 2f * t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = endPos;

            if (_mover != null)
            {
                _mover.enabled = true;
            }

            _isKnockingBack = false;
        }

        private void HandleDeath(EnemyHealth enemy)
        {
            _enemyHealth.OnDamagedDetailed -= HandleDamaged;
            _enemyHealth.OnDeath -= HandleDeath;
            StopAllCoroutines();

            if (_mover != null)
            {
                _mover.enabled = true;
            }
        }
    }
}
