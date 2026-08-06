using System.Collections.Generic;
using System.Reflection;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// <see cref="ChapterData"/>의 파생 값과 검증 규칙(#64).
    /// 특히 '웨이브에 챕터 밖 적이 섞이지 않는다'는 DoD가 실제로 강제되는지를 본다.
    /// </summary>
    public class ChapterDataTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void MiniBossMinuteMarks_AreDerivedFromStages()
        {
            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData>
            {
                CreateStage(3f),
                CreateStage(6f),
                CreateStage(9f)
            });

            // 마크를 디렉터와 스포너에 따로 입력하던 중복을 없앤 지점 —
            // 차수 목록이 유일한 출처이므로 둘이 어긋날 수 없다.
            Assert.That(chapter.MiniBossMinuteMarks, Is.EqualTo(new[] { 3f, 6f, 9f }));
        }

        [Test]
        public void MiniBossMinuteMarks_SkipNullAndNonPositiveStages()
        {
            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData>
            {
                CreateStage(3f),
                null,
                CreateStage(0f),
                CreateStage(9f)
            });

            Assert.That(chapter.MiniBossMinuteMarks, Is.EqualTo(new[] { 3f, 9f }));
        }

        [Test]
        public void MiniBossCandidates_FallBackToFullRoster_WhenNotSpecified()
        {
            MonsterData wolf = CreateMonsterData("Changgwi");
            MonsterData fox = CreateMonsterData("Maegu");
            var roster = new List<WaveEnemySpawnEntry> { CreateEntry(wolf), CreateEntry(fox) };

            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_enemyPool", roster);
            SetPrivateField(chapter, "_miniBossCandidates", new List<WaveEnemySpawnEntry>());

            // 대부분의 챕터는 후보를 따로 나눌 이유가 없으므로 비워두는 게 기본이다.
            Assert.That(chapter.MiniBossCandidates, Is.SameAs(roster));
        }

        [Test]
        public void IsValid_Fails_WhenWaveUsesEnemyOutsideRoster()
        {
            MonsterData inChapter = CreateMonsterData("Changgwi");
            MonsterData stranger = CreateMonsterData("Imugi");

            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry> { CreateEntry(inChapter) });
            SetPrivateField(chapter, "_waveDatabase", CreateWaveDatabase(inChapter, stranger));

            // #64 DoD: "챕터 진행 중 다른 챕터의 적이 등장하지 않는다"의 실제 강제 지점.
            Assert.That(chapter.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("Imugi"));
        }

        [Test]
        public void IsValid_Passes_WhenEveryWaveEnemyIsInRoster()
        {
            MonsterData wolf = CreateMonsterData("Changgwi");
            MonsterData fox = CreateMonsterData("Maegu");

            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry>
            {
                CreateEntry(wolf),
                CreateEntry(fox)
            });
            SetPrivateField(chapter, "_waveDatabase", CreateWaveDatabase(wolf, fox));

            Assert.That(chapter.IsValid(out string reason), Is.True, reason);
        }

        [Test]
        public void IsValid_Fails_WhenMiniBossCandidateIsOutsideRoster()
        {
            MonsterData wolf = CreateMonsterData("Changgwi");
            MonsterData stranger = CreateMonsterData("Imugi");

            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry> { CreateEntry(wolf) });
            SetPrivateField(chapter, "_waveDatabase", CreateWaveDatabase(wolf));
            SetPrivateField(chapter, "_miniBossCandidates", new List<WaveEnemySpawnEntry> { CreateEntry(stranger) });

            // 후보 목록이 격리 검증을 우회하는 뒷문이 되면 안 된다.
            Assert.That(chapter.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("Imugi"));
        }

        [Test]
        public void IsValid_Fails_WhenMiniBossMarksAreNotAscending()
        {
            ChapterData chapter = CreateValidChapter();
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData>
            {
                CreateStage(6f),
                CreateStage(3f)
            });

            // 마크가 뒤섞이면 MiniBossSpawner가 먼저 등록된 차수만 쓰고 나머지를 조용히 버린다.
            Assert.That(chapter.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("오름차순"));
        }

        [Test]
        public void IsValid_Fails_WhenMiniBossMarkIsAtOrAfterBossMark()
        {
            ChapterData chapter = CreateValidChapter();
            SetPrivateField(chapter, "_bossMinuteMark", 10f);
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData> { CreateStage(10f) });

            // 보스 진입 후에는 마크가 발행되지 않으므로 이 차수는 영영 실행되지 않는다.
            Assert.That(chapter.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("보스 마크"));
        }

        [Test]
        public void IsValid_Fails_WhenBossMarkIsSetButPrefabIsMissing()
        {
            ChapterData chapter = CreateValidChapter();
            SetPrivateField(chapter, "_bossPrefab", (EnemyHealth)null);

            // 이대로 두면 10분에 아무 일도 일어나지 않고 런이 빈 채로 계속된다.
            Assert.That(chapter.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("보스 프리팹"));
        }

        [Test]
        public void IsValid_Passes_WhenBossMarkIsZeroAndPrefabIsMissing()
        {
            ChapterData chapter = CreateValidChapter();
            SetPrivateField(chapter, "_bossPrefab", (EnemyHealth)null);
            SetPrivateField(chapter, "_bossMinuteMark", 0f);
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData>());

            // 보스 없는 챕터(튜토리얼 등)도 성립해야 한다.
            Assert.That(chapter.IsValid(out string reason), Is.True, reason);
        }

        [Test]
        public void IsValid_Fails_WhenRosterIsEmpty()
        {
            ChapterData chapter = CreateValidChapter();
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry>());

            // 풀이 비면 미니 보스 마크만 울리고 아무것도 소환되지 않는다.
            Assert.That(chapter.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("적 풀"));
        }

        /// <summary>보스·미니보스·로스터·웨이브가 모두 채워진, 검증을 통과하는 기준 챕터.</summary>
        private ChapterData CreateValidChapter()
        {
            MonsterData wolf = CreateMonsterData("Changgwi");

            ChapterData chapter = CreateChapter();
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry> { CreateEntry(wolf) });
            SetPrivateField(chapter, "_waveDatabase", CreateWaveDatabase(wolf));
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData> { CreateStage(3f) });
            SetPrivateField(chapter, "_bossMinuteMark", 10f);
            SetPrivateField(chapter, "_bossPrefab", CreatePrefab("Boss"));

            return chapter;
        }

        /// <summary>
        /// 보스가 없는 최소 챕터. <c>_bossMinuteMark</c>를 0으로 낮추는 게 핵심이다 —
        /// 기본값 10분을 그대로 두면 보스 프리팹 검사가 먼저 걸려, 정작 보려던 격리 검증까지 도달하지 못한다.
        /// </summary>
        private ChapterData CreateChapter()
        {
            ChapterData chapter = ScriptableObject.CreateInstance<ChapterData>();
            chapter.name = "TestChapter";
            SetPrivateField(chapter, "_bossMinuteMark", 0f);
            _created.Add(chapter);
            return chapter;
        }

        private WaveDatabase CreateWaveDatabase(params MonsterData[] species)
        {
            var wave = new WaveDefinition();
            var entries = new List<WaveEnemySpawnEntry>();
            for (int i = 0; i < species.Length; i++)
            {
                entries.Add(CreateEntry(species[i]));
            }

            SetPrivateField(wave, "_enemies", entries);
            SetPrivateField(wave, "_waveName", "TestWave");

            WaveDatabase database = ScriptableObject.CreateInstance<WaveDatabase>();
            database.name = "TestWaveDatabase";
            SetPrivateField(database, "_waves", new List<WaveDefinition> { wave });
            _created.Add(database);
            return database;
        }

        private WaveEnemySpawnEntry CreateEntry(MonsterData species)
        {
            var entry = new WaveEnemySpawnEntry();
            SetPrivateField(entry, "_monsterData", species);
            return entry;
        }

        private static MiniBossStageData CreateStage(float minuteMark)
        {
            var stage = new MiniBossStageData();
            SetPrivateField(stage, "_spawnMinuteMark", minuteMark);
            return stage;
        }

        /// <summary>MonsterData.IsValid가 프리팹을 요구하므로 항상 프리팹을 붙여 만든다.</summary>
        private MonsterData CreateMonsterData(string displayName)
        {
            MonsterData species = ScriptableObject.CreateInstance<MonsterData>();
            species.name = $"{displayName}_MonsterData";
            SetPrivateField(species, "_displayName", displayName);
            SetPrivateField(species, "_enemyPrefab", CreatePrefab(displayName));
            _created.Add(species);
            return species;
        }

        private EnemyHealth CreatePrefab(string objectName)
        {
            var go = new GameObject(objectName);
            _created.Add(go);
            return go.AddComponent<EnemyHealth>();
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field not found: {fieldName}");
            fieldInfo.SetValue(target, value);
        }
    }
}
