using System.Collections.Generic;
using System.IO;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Core.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class SaveSystemTests
    {
        private string _tempPath;
        private JsonSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            // 각 테스트마다 고유한 임시 파일을 사용해 상호 간섭과 실제 세이브 오염을 막는다.
            _tempPath = Path.Combine(Path.GetTempPath(), $"mukseon_save_test_{System.Guid.NewGuid():N}.json");
            _storage = new JsonSaveStorage(_tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }

            string temp = _tempPath + ".tmp";
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        // ---- SaveData 기본값 ----

        [Test]
        public void CreateDefault_UnlocksMudang_AndSetsCurrentVersion()
        {
            SaveData data = SaveData.CreateDefault();

            Assert.That(data.SaveDataVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(data.TotalGold, Is.EqualTo(0));
            Assert.That(data.TotalSpirit, Is.EqualTo(0));
            Assert.That(data.UnlockedCharacters, Does.Contain(SaveData.DefaultUnlockedCharacterId));
            Assert.That(data.UnlockedSkills, Is.Empty);
            Assert.That(data.UpgradeLevels.Count, Is.EqualTo(0));
            Assert.That(data.TutorialCompleted, Is.False);
            Assert.That(data.DirectionDisplayMode, Is.EqualTo((int)DirectionDisplayMode.Both));
            Assert.That(data.DirectionArrowAssist, Is.False);
            Assert.That(data.DirectionColors.Count, Is.EqualTo(0));
        }

        // ---- 라운드트립: 모든 필드 보존 ----

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            SaveData original = new SaveData
            {
                SaveDataVersion = SaveData.CurrentVersion,
                TotalGold = 123456789,
                TotalSpirit = 42,
                UnlockedCharacters = new List<string> { "character.mudang", "character.baksu" },
                UnlockedSkills = new List<string> { "skill.new_a", "skill.new_b" },
                TutorialCompleted = true,
            };
            original.UpgradeLevels.Set("MaxHP", 3);
            original.UpgradeLevels.Set("MagnetRadius", 1);

            _storage.Save(original);
            SaveData loaded = _storage.Load();

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.SaveDataVersion, Is.EqualTo(original.SaveDataVersion));
            Assert.That(loaded.TotalGold, Is.EqualTo(123456789));
            Assert.That(loaded.TotalSpirit, Is.EqualTo(42));
            Assert.That(loaded.UnlockedCharacters, Is.EquivalentTo(original.UnlockedCharacters));
            Assert.That(loaded.UnlockedSkills, Is.EquivalentTo(original.UnlockedSkills));
            Assert.That(loaded.TutorialCompleted, Is.True);
            Assert.That(loaded.UpgradeLevels.GetValueOrDefault("MaxHP"), Is.EqualTo(3));
            Assert.That(loaded.UpgradeLevels.GetValueOrDefault("MagnetRadius"), Is.EqualTo(1));
        }

        // ---- Dictionary(맵) 직렬화 왕복 ----

        [Test]
        public void UpgradeLevels_SerializationRoundTrip_PreservesEntries()
        {
            var map = new SerializableStringIntMap();
            map.Set("a", 10);
            map.Set("b", 20);
            map.Set("a", 15); // 덮어쓰기 확인

            SaveData data = SaveData.CreateDefault();
            data.UpgradeLevels = map;

            _storage.Save(data);
            SaveData loaded = _storage.Load();

            Assert.That(loaded.UpgradeLevels.Count, Is.EqualTo(2));
            Assert.That(loaded.UpgradeLevels.GetValueOrDefault("a"), Is.EqualTo(15));
            Assert.That(loaded.UpgradeLevels.GetValueOrDefault("b"), Is.EqualTo(20));
            Assert.That(loaded.UpgradeLevels.ContainsKey("missing"), Is.False);
        }

        // ---- 방향 색상 오버라이드(#83) 직렬화 왕복 ----

        [Test]
        public void DirectionColors_SerializationRoundTrip_PreservesMapping()
        {
            SaveData data = SaveData.CreateDefault();
            data.DirectionDisplayMode = (int)DirectionDisplayMode.Orb;
            data.DirectionArrowAssist = true;
            data.DirectionColors.SetColor(SwipeDirection.Up, new Color(0.2f, 0.4f, 0.6f));
            data.DirectionColors.SetColor(SwipeDirection.Left, Color.red);
            data.DirectionColors.SetColor(SwipeDirection.Up, Color.green); // 덮어쓰기 확인

            _storage.Save(data);
            SaveData loaded = _storage.Load();

            Assert.That(loaded.DirectionDisplayMode, Is.EqualTo((int)DirectionDisplayMode.Orb));
            Assert.That(loaded.DirectionArrowAssist, Is.True);
            Assert.That(loaded.DirectionColors.Count, Is.EqualTo(2));
            Assert.That(loaded.DirectionColors.TryGetColor(SwipeDirection.Up, out Color up), Is.True);
            Assert.That(ColorUtility.ToHtmlStringRGB(up), Is.EqualTo(ColorUtility.ToHtmlStringRGB(Color.green)));
            Assert.That(loaded.DirectionColors.TryGetColor(SwipeDirection.Right, out _), Is.False);
        }

        [Test]
        public void DirectionColors_None_IsNotStorable()
        {
            var overrides = new DirectionColorOverrides();
            overrides.SetColor(SwipeDirection.None, Color.red);

            Assert.That(overrides.Count, Is.EqualTo(0));
            Assert.That(overrides.TryGetColor(SwipeDirection.None, out _), Is.False);
        }

        // ---- 파일 부재 시 동작 ----

        [Test]
        public void Storage_Load_ReturnsNull_WhenFileMissing()
        {
            Assert.That(_storage.Exists(), Is.False);
            Assert.That(_storage.Load(), Is.Null);
        }

        [Test]
        public void Service_Load_ReturnsDefault_WhenFileMissing()
        {
            var service = new SaveService(_storage);

            SaveData data = service.Load();

            Assert.That(data, Is.Not.Null);
            Assert.That(data.SaveDataVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(data.UnlockedCharacters, Does.Contain(SaveData.DefaultUnlockedCharacterId));
            Assert.That(service.Current, Is.SameAs(data));
        }

        // ---- 마이그레이션 ----

        [Test]
        public void Migrate_UpgradesOldVersion_AndNormalizesNulls()
        {
            SaveData legacy = new SaveData
            {
                SaveDataVersion = 0,
                UpgradeLevels = null,
                UnlockedCharacters = null,
                UnlockedSkills = null,
            };

            SaveData migrated = SaveMigration.Migrate(legacy);

            Assert.That(migrated.SaveDataVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.UpgradeLevels, Is.Not.Null);
            Assert.That(migrated.UnlockedCharacters, Is.Not.Null);
            Assert.That(migrated.UnlockedSkills, Is.Not.Null);
            Assert.That(migrated.UnlockedCharacters, Does.Contain(SaveData.DefaultUnlockedCharacterId));
        }

        [Test]
        public void Migrate_Null_ReturnsDefault()
        {
            SaveData migrated = SaveMigration.Migrate(null);

            Assert.That(migrated, Is.Not.Null);
            Assert.That(migrated.SaveDataVersion, Is.EqualTo(SaveData.CurrentVersion));
        }

        // v1 세이브에는 방향 색상 필드가 없다. 승격 시 '둘 다'로 시작해야 한다 —
        // 정수 0이 그대로 남으면 글로우 전용으로 켜져, 기존 유저의 색 오브가 말없이 사라진다(#83).
        [Test]
        public void Migrate_LegacyV1_StartsWithBothDisplayMode()
        {
            SaveData legacy = SaveData.CreateDefault();
            legacy.SaveDataVersion = 1;
            legacy.DirectionDisplayMode = 0;
            legacy.DirectionArrowAssist = true;
            legacy.DirectionColors.SetColor(SwipeDirection.Up, Color.magenta);

            SaveData migrated = SaveMigration.Migrate(legacy);

            Assert.That(migrated.SaveDataVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.DirectionDisplayMode, Is.EqualTo((int)DirectionDisplayMode.Both));
            Assert.That(migrated.DirectionArrowAssist, Is.False);
            Assert.That(migrated.DirectionColors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Migrate_NullDirectionColors_IsNormalized()
        {
            SaveData data = SaveData.CreateDefault();
            data.DirectionColors = null;

            SaveData migrated = SaveMigration.Migrate(data);

            Assert.That(migrated.DirectionColors, Is.Not.Null);
        }

        // ---- 저장 안정성: 원자적 교체 / 임시 파일 잔존 없음 ----

        [Test]
        public void Save_OverwritesExistingFile_AndLeavesNoTempFile()
        {
            SaveData first = SaveData.CreateDefault();
            first.TotalGold = 100;
            _storage.Save(first);

            SaveData second = SaveData.CreateDefault();
            second.TotalGold = 999;
            bool saved = _storage.Save(second); // 기존 파일 위에 다시 저장 → File.Replace 경로

            SaveData loaded = _storage.Load();
            Assert.That(saved, Is.True);
            Assert.That(loaded.TotalGold, Is.EqualTo(999));
            Assert.That(File.Exists(_tempPath + ".tmp"), Is.False, "임시 파일이 남아있으면 안 된다.");
        }

        [Test]
        public void Storage_Save_ReturnsFalse_ForNullData()
        {
            Assert.That(_storage.Save(null), Is.False);
        }

        // ---- 서비스 저장/로드 통합 + OnChanged ----

        [Test]
        public void Service_SaveThenReload_PersistsChanges_AndRaisesOnChanged()
        {
            var service = new SaveService(_storage);
            service.Load();

            int changedCount = 0;
            service.OnChanged += _ => changedCount++;

            service.Current.TotalSpirit = 7;
            service.Current.UnlockedSkills.Add("skill.unlocked");
            bool saved = service.Save();

            var reloaded = new SaveService(_storage);
            reloaded.Load();

            Assert.That(saved, Is.True);
            Assert.That(changedCount, Is.EqualTo(1), "Save() 성공 시 OnChanged가 1회 발행돼야 한다.");
            Assert.That(reloaded.Current.TotalSpirit, Is.EqualTo(7));
            Assert.That(reloaded.Current.UnlockedSkills, Does.Contain("skill.unlocked"));
        }

        [Test]
        public void Service_Save_DoesNotRaiseOnChanged_WhenStorageFails()
        {
            var failing = new FailingStorage();
            var service = new SaveService(failing);
            service.Load();

            int changedCount = 0;
            service.OnChanged += _ => changedCount++;

            bool saved = service.Save();

            Assert.That(saved, Is.False, "저장소가 실패하면 Save()는 false를 반환해야 한다.");
            Assert.That(changedCount, Is.EqualTo(0), "저장 실패 시 OnChanged가 발행되면 안 된다.");
        }

        /// <summary>저장이 항상 실패하는 테스트용 저장소.</summary>
        private sealed class FailingStorage : ISaveStorage
        {
            public bool Exists() => false;

            public SaveData Load() => null;

            public bool Save(SaveData data) => false;

            public void Delete()
            {
            }
        }
    }
}
