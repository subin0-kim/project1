using System.Reflection;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using Mukseon.Gameplay.Stats;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>레벨별 초당 회복량 수치(skill_balance_mvp.md §3)와 클램프를 검증한다(PlayerHealth 불필요).</summary>
    public class HealthRegenSkillLevelTests
    {
        private GameObject _go;
        private HealthRegenSkill _skill;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HealthRegenSkill");
            _skill = _go.AddComponent<HealthRegenSkill>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void NotOwned_HasZeroRegen()
        {
            Assert.That(_skill.Level, Is.EqualTo(0));
            Assert.That(_skill.CurrentRegenPerSecond, Is.EqualTo(0f));
        }

        [Test]
        public void Level1To3_UseBalanceTableValues()
        {
            _skill.ApplyLevel(1);
            Assert.That(_skill.CurrentRegenPerSecond, Is.EqualTo(2f).Within(1e-4f));

            _skill.ApplyLevel(2);
            Assert.That(_skill.CurrentRegenPerSecond, Is.EqualTo(4f).Within(1e-4f));

            _skill.ApplyLevel(3);
            Assert.That(_skill.CurrentRegenPerSecond, Is.EqualTo(7f).Within(1e-4f));
        }

        [Test]
        public void ApplyLevel_ClampsToRange()
        {
            _skill.ApplyLevel(99);
            Assert.That(_skill.Level, Is.EqualTo(HealthRegenSkill.MaxLevel));

            _skill.ApplyLevel(-3);
            Assert.That(_skill.Level, Is.EqualTo(0));
        }

        [Test]
        public void HandleSkillEffectPending_HealthRegenType_UpdatesLevel()
        {
            SkillData skill = MakeSkill(LevelUpSkillEffectType.HealthRegen);
            try
            {
                InvokeHandleSkillEffectPending(_skill, skill, 2);
                Assert.That(_skill.Level, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void HandleSkillEffectPending_OtherType_IsIgnored()
        {
            SkillData skill = MakeSkill(LevelUpSkillEffectType.BarrierRadiusExpand);
            try
            {
                InvokeHandleSkillEffectPending(_skill, skill, 2);
                Assert.That(_skill.Level, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void HandleSkillEffectPending_NullSkill_IsIgnored()
        {
            InvokeHandleSkillEffectPending(_skill, null, 2);
            Assert.That(_skill.Level, Is.EqualTo(0));
        }

        private static SkillData MakeSkill(LevelUpSkillEffectType effectType)
        {
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            FieldInfo field = typeof(SkillData).GetField("_effectType", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "SkillData._effectType field not found");
            field.SetValue(skill, effectType);
            return skill;
        }

        private static void InvokeHandleSkillEffectPending(HealthRegenSkill skill, SkillData data, int nextLevel)
        {
            MethodInfo method = typeof(HealthRegenSkill).GetMethod("HandleSkillEffectPending", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "HealthRegenSkill.HandleSkillEffectPending method not found");
            method.Invoke(skill, new object[] { data, nextLevel });
        }
    }

    /// <summary>PlayerHealth와 결합한 회복 동작(누적·최대치 클램프·사망/미보유 가드)을 검증한다.</summary>
    public class HealthRegenSkillRegenTests
    {
        private GameObject _go;
        private PlayerHealth _playerHealth;
        private HealthRegenSkill _skill;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HealthRegenSkillRegen");
            _go.AddComponent<PlayerStatSystem>();
            _playerHealth = _go.AddComponent<PlayerHealth>();

            // EditMode에서는 Awake가 호출되지 않아 직렬화 기본값/참조가 적용되지 않으므로 직접 세팅한다.
            SetPrivateField(_playerHealth, "_fallbackMaxHealth", 100f);
            _playerHealth.ResetHealth();

            _skill = _go.AddComponent<HealthRegenSkill>();
            // Awake 미호출 → GetComponent 자동 결선이 일어나지 않으므로 참조를 주입한다.
            SetPrivateField(_skill, "_playerHealth", _playerHealth);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void ApplyRegen_HealsRateTimesDeltaTime()
        {
            _playerHealth.TakeDamage(50f); // 100 → 50
            _skill.ApplyLevel(1);          // 2 HP/s

            _skill.ApplyRegen(1f);         // +2
            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(52f).Within(1e-3f));

            _skill.ApplyRegen(0.5f);       // +1
            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(53f).Within(1e-3f));
        }

        [Test]
        public void ApplyRegen_ScalesWithLevel()
        {
            _playerHealth.TakeDamage(60f); // 100 → 40
            _skill.ApplyLevel(3);          // 7 HP/s

            _skill.ApplyRegen(2f);         // +14
            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(54f).Within(1e-3f));
        }

        [Test]
        public void ApplyRegen_DoesNotExceedMaxHealth()
        {
            _playerHealth.TakeDamage(1f);  // 100 → 99
            _skill.ApplyLevel(3);          // 7 HP/s

            _skill.ApplyRegen(10f);        // +70 → 최대치(100)에서 클램프

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(_playerHealth.MaxHealth).Within(1e-3f));
        }

        [Test]
        public void ApplyRegen_NotOwned_DoesNothing()
        {
            _playerHealth.TakeDamage(30f);
            float before = _playerHealth.CurrentHealth;

            _skill.ApplyRegen(5f);         // 레벨 0

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(before));
        }

        [Test]
        public void ApplyRegen_DeadPlayer_DoesNotRevive()
        {
            _playerHealth.TakeDamage(_playerHealth.MaxHealth + 1f); // 사망(0 HP)
            Assert.That(_playerHealth.IsAlive, Is.False);

            _skill.ApplyLevel(3);
            _skill.ApplyRegen(5f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(0f));
        }

        [Test]
        public void ApplyRegen_IgnoresZeroAndNegativeDeltaTime()
        {
            _playerHealth.TakeDamage(20f);
            float before = _playerHealth.CurrentHealth;
            _skill.ApplyLevel(2);

            _skill.ApplyRegen(0f);
            _skill.ApplyRegen(-1f);

            Assert.That(_playerHealth.CurrentHealth, Is.EqualTo(before));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
