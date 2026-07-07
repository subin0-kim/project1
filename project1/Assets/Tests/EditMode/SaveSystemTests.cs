using System.Collections.Generic;
using System.IO;
using Mukseon.Core.Persistence;
using NUnit.Framework;

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

        // ---- 저장 안정성: 원자적 교체 / 임시 파일 잔존 없음 ----

        [Test]
        public void Save_OverwritesExistingFile_AndLeavesNoTempFile()
        {
            SaveData first = SaveData.CreateDefault();
            first.TotalGold = 100;
            _storage.Save(first);

            SaveData second = SaveData.CreateDefault();
            second.TotalGold = 999;
            _storage.Save(second); // 기존 파일 위에 다시 저장 → File.Replace 경로

            SaveData loaded = _storage.Load();
            Assert.That(loaded.TotalGold, Is.EqualTo(999));
            Assert.That(File.Exists(_tempPath + ".tmp"), Is.False, "임시 파일이 남아있으면 안 된다.");
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
            service.Save();

            var reloaded = new SaveService(_storage);
            reloaded.Load();

            Assert.That(changedCount, Is.EqualTo(1), "Save() 시 OnChanged가 1회 발행돼야 한다.");
            Assert.That(reloaded.Current.TotalSpirit, Is.EqualTo(7));
            Assert.That(reloaded.Current.UnlockedSkills, Does.Contain("skill.unlocked"));
        }
    }
}
