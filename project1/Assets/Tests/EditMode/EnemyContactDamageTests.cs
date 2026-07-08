using System.Reflection;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class EnemyContactDamageTests
    {
        private const float ContactRadius = 0.6f;
        private const float TickInterval = 1f;
        private const float DamagePerTick = 10f;

        private GameObject _enemyGo;
        private EnemyHealth _enemyHealth;
        private EnemyContactDamage _contactDamage;

        private GameObject _playerGo;
        private PlayerHealth _playerHealth;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("Player");
            _playerGo.AddComponent<PlayerStatSystem>();
            _playerHealth = _playerGo.AddComponent<PlayerHealth>();
            SetPrivateField(_playerHealth, "_fallbackMaxHealth", 100f);
            _playerHealth.ResetHealth();

            _enemyGo = new GameObject("Enemy");
            _enemyHealth = _enemyGo.AddComponent<EnemyHealth>();
            _enemyHealth.ResetHealth();
            _contactDamage = _enemyGo.AddComponent<EnemyContactDamage>();
            SetPrivateField(_contactDamage, "_contactRadius", ContactRadius);
            SetPrivateField(_contactDamage, "_tickInterval", TickInterval);
            SetPrivateField(_contactDamage, "_damagePerTick", DamagePerTick);
            _contactDamage.SetPlayerTarget(_playerHealth);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_enemyGo);
            Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void InContact_DamagesPlayerImmediately()
        {
            _enemyGo.transform.position = _playerGo.transform.position;

            _contactDamage.Tick(0.016f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f - DamagePerTick).Within(0.01f));
        }

        [Test]
        public void InContact_DoesNotExceedOneTickPerInterval()
        {
            _enemyGo.transform.position = _playerGo.transform.position;

            // 여러 프레임이 지나도 틱 간격(1초) 안에서는 데미지가 1회만 들어가야 한다.
            for (int i = 0; i < 10; i++)
            {
                _contactDamage.Tick(0.016f);
            }

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f - DamagePerTick).Within(0.01f));
        }

        [Test]
        public void InContact_TicksAgainAfterInterval()
        {
            _enemyGo.transform.position = _playerGo.transform.position;

            _contactDamage.Tick(0.016f);
            _contactDamage.Tick(TickInterval + 0.01f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f - DamagePerTick * 2f).Within(0.01f));
        }

        [Test]
        public void OutOfContact_DoesNotDamage()
        {
            _enemyGo.transform.position = new Vector3(ContactRadius + 1f, 0f, 0f);

            _contactDamage.Tick(0.016f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void DeadEnemy_DoesNotDamage()
        {
            _enemyGo.transform.position = _playerGo.transform.position;
            _enemyHealth.Kill(countAsKill: false);

            _contactDamage.Tick(0.016f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void ZeroDamage_GimmickEnemy_DoesNotDamage()
        {
            // 기믹 적(어둑시니/목귀)은 접촉 데미지 0 — MonsterData 적용 후 데미지가 없어야 한다.
            var data = ScriptableObject.CreateInstance<MonsterData>();
            try
            {
                SetPrivateField(data, "_contactDamagePerSecond", 0f);
                _contactDamage.ApplyMonsterData(data);

                _enemyGo.transform.position = _playerGo.transform.position;
                _contactDamage.Tick(0.016f);

                Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void ApplyMonsterData_ConvertsPerSecondToPerTick()
        {
            // 초당 데미지 × 틱 간격 = 틱당 데미지 환산 검증 (간격 0.5초, 초당 10 → 틱당 5)
            SetPrivateField(_contactDamage, "_tickInterval", 0.5f);

            var data = ScriptableObject.CreateInstance<MonsterData>();
            try
            {
                SetPrivateField(data, "_contactDamagePerSecond", 10f);
                _contactDamage.ApplyMonsterData(data);

                Assert.That(_contactDamage.DamagePerTick, Is.EqualTo(5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Reenable_ResetsTickTimer()
        {
            _enemyGo.transform.position = _playerGo.transform.position;

            // 1틱 소모 — 다음 틱까지 아직 간격이 남아 있는 상태를 만든다.
            _contactDamage.Tick(0.016f);

            // 풀 재사용 흉내: 런타임에는 OnEnable이 ResetForReuse를 호출한다.
            // EditMode에서는 SetActive로 OnEnable이 불리지 않으므로 직접 호출한다.
            _contactDamage.ResetForReuse();

            _contactDamage.Tick(0.016f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f - DamagePerTick * 2f).Within(0.01f));
        }

        [Test]
        public void ZAxisOffset_DoesNotAffectContact()
        {
            // 스프라이트 정렬 등으로 Z 오프셋이 있어도 XY 평면 거리만으로 접촉을 판정해야 한다.
            _enemyGo.transform.position = _playerGo.transform.position + new Vector3(0f, 0f, 5f);

            _contactDamage.Tick(0.016f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(100f - DamagePerTick).Within(0.01f));
        }

        [Test]
        public void DeadPlayer_DoesNotReceiveDamage()
        {
            _enemyGo.transform.position = _playerGo.transform.position;
            _playerHealth.TakeDamage(1000f);
            Assert.That(_playerHealth.IsAlive, Is.False);

            float healthAfterDeath = _playerHealth.CurrentHealth;
            _contactDamage.Tick(TickInterval + 0.01f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(healthAfterDeath));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
