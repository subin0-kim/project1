using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class EnemyMoverTests
    {
        private const float DeltaTime = 0.1f;

        private GameObject _enemyGo;
        private EnemyHealth _enemyHealth;
        private EnemyMover _mover;

        [SetUp]
        public void SetUp()
        {
            _enemyGo = new GameObject("Enemy");
            _enemyHealth = _enemyGo.AddComponent<EnemyHealth>();
            _enemyHealth.ResetHealth();
            _mover = _enemyGo.AddComponent<EnemyMover>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_enemyGo);
        }

        [Test]
        public void TrackPlayer_MovesEnemyTowardTarget()
        {
            var playerGo = new GameObject("Player");

            try
            {
                playerGo.transform.position = new Vector3(10f, 0f, 0f);
                _enemyGo.transform.position = Vector3.zero;
                _enemyHealth.SetMoveSpeed(5f);

                _mover.SetMovePattern(EnemyMovePattern.TrackPlayer);
                _mover.SetPlayerTarget(playerGo.transform);
                _mover.Tick(DeltaTime);

                float distanceAfter = Vector3.Distance(_enemyGo.transform.position, playerGo.transform.position);
                Assert.That(distanceAfter, Is.LessThan(10f));
                Assert.That(_enemyGo.transform.position.x, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void TrackPlayer_NoTarget_DoesNotMove()
        {
            _enemyGo.transform.position = new Vector3(3f, 3f, 0f);
            _enemyHealth.SetMoveSpeed(5f);

            _mover.SetMovePattern(EnemyMovePattern.TrackPlayer);
            _mover.SetPlayerTarget(null);
            _mover.Tick(DeltaTime);

            Assert.That(_enemyGo.transform.position, Is.EqualTo(new Vector3(3f, 3f, 0f)));
        }

        [Test]
        public void VerticalDrop_MovesEnemyDownward()
        {
            _enemyGo.transform.position = new Vector3(0f, 5f, 0f);
            _enemyHealth.SetMoveSpeed(5f);

            _mover.SetMovePattern(EnemyMovePattern.VerticalDrop);
            _mover.Tick(DeltaTime);

            Assert.That(_enemyGo.transform.position.y, Is.LessThan(5f));
            Assert.That(_enemyGo.transform.position.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void RiseFromGround_MovesEnemyUpward()
        {
            _enemyGo.transform.position = new Vector3(3f, 0f, 0f);
            _enemyHealth.SetMoveSpeed(5f);

            _mover.SetMovePattern(EnemyMovePattern.RiseFromGround);
            // OnEnable에서 _spawnPosition 캐시
            _mover.SendMessage("OnEnable");
            _mover.Tick(DeltaTime);

            Assert.That(_enemyGo.transform.position.y, Is.GreaterThan(0f));
            Assert.That(_enemyGo.transform.position.x, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void DeadEnemy_DoesNotMove()
        {
            var playerGo = new GameObject("Player");

            try
            {
                playerGo.transform.position = new Vector3(10f, 0f, 0f);
                _enemyGo.transform.position = Vector3.zero;
                _enemyHealth.SetMoveSpeed(5f);

                _mover.SetMovePattern(EnemyMovePattern.TrackPlayer);
                _mover.SetPlayerTarget(playerGo.transform);

                // 먼저 IsAlive 상태로 Tick하면 이동함을 확인
                _mover.Tick(DeltaTime);
                Assert.That(_enemyGo.transform.position.x, Is.GreaterThan(0f));

                // 죽인 뒤에는 이동하지 않음
                Vector3 positionBeforeKill = _enemyGo.transform.position;
                _enemyHealth.Kill(countAsKill: false);

                // _enemyHealth.IsAlive == false이므로 Tick이 이동을 스킵
                // (GO가 비활성화되어도 Tick을 직접 호출하면 내부 IsAlive 검사가 동작)
                _mover.Tick(DeltaTime);

                Assert.That(_enemyGo.transform.position, Is.EqualTo(positionBeforeKill));
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void SetMovePattern_ChangesPattern()
        {
            _mover.SetMovePattern(EnemyMovePattern.VerticalDrop);
            Assert.That(_mover.MovePattern, Is.EqualTo(EnemyMovePattern.VerticalDrop));

            _mover.SetMovePattern(EnemyMovePattern.TrackPlayer);
            Assert.That(_mover.MovePattern, Is.EqualTo(EnemyMovePattern.TrackPlayer));
        }
    }
}
