using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Mukseon.Core;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 챕터 목록(<see cref="ChapterDatabase"/>)과, 선택된 챕터가 전투 시스템 세 곳에
    /// 실제로 전달되는지(#64 DoD)를 본다.
    ///
    /// EditMode에서 AddComponent는 Awake를 부르지 않으므로, 각 디렉터의 <c>ApplyChapterData</c>를
    /// 직접 호출해 검사한다 — 런타임에 이 메서드를 부르는 주체가 Awake다.
    ///
    /// 디렉터는 챕터를 <b>통째로</b> 받거나 통째로 무시하므로, 배선을 보는 테스트는 모두
    /// <see cref="ChapterData.IsValid"/>를 통과하는 챕터에서 출발한다(<see cref="CreatePlayableChapter"/>).
    /// </summary>
    public class ChapterBindingTests
    {
        /// <summary>
        /// 미완성 챕터를 만났을 때 세 디렉터가 남기는 경고의 공통 부분.
        /// 요점은 "조용히 다른 챕터 콘텐츠로 퇴화하지 않는다"이므로 문구가 아니라 경고의 존재를 본다.
        /// </summary>
        private static readonly Regex IncompleteChapterWarning = new Regex("유효하지 않아");

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // static이라 테스트 간에 새어 나간다. RunContext.Reset()과 같은 이유로 반드시 비운다.
            RunContext.SelectedChapter = null;

            for (int i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void ChapterDatabase_IsInvalid_WhenChapterIdsCollide()
        {
            ChapterDatabase database = CreateDatabase(
                CreateChapter("chapter.1.sillim"),
                CreateChapter("chapter.1.sillim"));

            Assert.That(database.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("chapter.1.sillim"));
        }

        [Test]
        public void ChapterDatabase_FindAndDefaultChapter_ResolveById()
        {
            ChapterData first = CreateChapter("chapter.1.sillim");
            ChapterData second = CreateChapter("chapter.2.cheonjedan");
            ChapterDatabase database = CreateDatabase(first, second);

            Assert.That(database.Find("chapter.2.cheonjedan"), Is.SameAs(second));
            Assert.That(database.Find("chapter.9.missing"), Is.Null);
            Assert.That(database.DefaultChapter, Is.SameAs(first));
        }

        [Test]
        public void ChapterDatabase_DefaultChapter_SkipsNullEntries()
        {
            ChapterData real = CreateChapter("chapter.1.sillim");
            ChapterDatabase database = CreateDatabase(null, real);

            Assert.That(database.DefaultChapter, Is.SameAs(real));
        }

        [Test]
        public void WaveCombatDirector_AppliesWaveDatabaseAndTimelineMarks()
        {
            ChapterData chapter = CreatePlayableChapter("chapter.1.sillim");
            WaveDatabase waves = CreateWaveDatabase();
            SetPrivateField(chapter, "_waveDatabase", waves);
            SetPrivateField(chapter, "_bossMinuteMark", 12f);
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData>
            {
                CreateStage(4f),
                CreateStage(8f)
            });

            WaveCombatDirector director = CreateComponent<WaveCombatDirector>();
            SetPrivateField(director, "_chapterData", chapter);
            Invoke(director, "ApplyChapterData");

            Assert.That(GetPrivateField<WaveDatabase>(director, "_waveDatabase"), Is.SameAs(waves));
            Assert.That(director.BossMinuteMark, Is.EqualTo(12f));
            Assert.That(director.ActiveChapter, Is.SameAs(chapter));

            // 마크는 챕터의 차수에서 파생되므로 스포너의 차수와 항상 짝이 맞는다.
            Assert.That(
                GetPrivateField<List<float>>(director, "_miniBossMinuteMarks"),
                Is.EqualTo(new[] { 4f, 8f }));
        }

        [Test]
        public void WaveCombatDirector_PrefersSelectedChapterOverSerializedOne()
        {
            ChapterData serialized = CreatePlayableChapter("chapter.1.sillim");
            SetPrivateField(serialized, "_bossMinuteMark", 10f);

            ChapterData selected = CreatePlayableChapter("chapter.2.cheonjedan");
            SetPrivateField(selected, "_bossMinuteMark", 15f);

            WaveCombatDirector director = CreateComponent<WaveCombatDirector>();
            SetPrivateField(director, "_chapterData", serialized);
            RunContext.SelectedChapter = selected;

            Invoke(director, "ApplyChapterData");

            // 스테이지 선택 결과가 씬 직렬화 값을 이긴다(#36 캐릭터 해석과 같은 규약).
            Assert.That(director.ActiveChapter, Is.SameAs(selected));
            Assert.That(director.BossMinuteMark, Is.EqualTo(15f));
        }

        [Test]
        public void WaveCombatDirector_LeavesSceneValuesAlone_WhenNoChapterIsSet()
        {
            WaveDatabase sceneWaves = CreateWaveDatabase();
            WaveCombatDirector director = CreateComponent<WaveCombatDirector>();
            SetPrivateField(director, "_waveDatabase", sceneWaves);
            SetPrivateField(director, "_bossMinuteMark", 7f);

            Invoke(director, "ApplyChapterData");

            // 게임플레이 씬 단독 실행(에디터)에서 종전처럼 동작해야 한다.
            Assert.That(GetPrivateField<WaveDatabase>(director, "_waveDatabase"), Is.SameAs(sceneWaves));
            Assert.That(director.BossMinuteMark, Is.EqualTo(7f));
        }

        /// <summary>
        /// 미완성 챕터를 골랐을 때 필드별로 폴백하면 "2장인데 웨이브·보스는 1장"이 되고, 그 상태로도
        /// 게임이 조용히 굴러가 2장이 미완성이라는 걸 플레이로는 알 수 없다. 통째로 무시하고 경고를 남긴다.
        /// </summary>
        [Test]
        public void WaveCombatDirector_IgnoresIncompleteChapter_AndKeepsSceneTimeline()
        {
            WaveDatabase sceneWaves = CreateWaveDatabase();
            WaveCombatDirector director = CreateComponent<WaveCombatDirector>();
            SetPrivateField(director, "_waveDatabase", sceneWaves);
            SetPrivateField(director, "_bossMinuteMark", 10f);

            // 2·3장 골격처럼 아직 웨이브도 로스터도 없는 챕터.
            RunContext.SelectedChapter = CreateChapter("chapter.2.cheonjedan");

            LogAssert.Expect(LogType.Warning, IncompleteChapterWarning);
            Invoke(director, "ApplyChapterData");

            Assert.That(GetPrivateField<WaveDatabase>(director, "_waveDatabase"), Is.SameAs(sceneWaves));
            Assert.That(director.BossMinuteMark, Is.EqualTo(10f));

            // 적용되지 않은 챕터가 ActiveChapter로 남으면 결과 화면·UI가 2장을 진행 중이라고 표시한다.
            Assert.That(director.ActiveChapter, Is.Null);
        }

        [Test]
        public void MiniBossSpawner_IgnoresIncompleteChapter_AndKeepsSceneStages()
        {
            var scenePool = new List<WaveEnemySpawnEntry> { CreateEntry(CreateMonsterData("Changgwi")) };
            var sceneStages = new List<MiniBossStageData> { CreateStage(3f) };

            MiniBossSpawner spawner = CreateComponent<MiniBossSpawner>();
            SetPrivateField(spawner, "_enemyPool", scenePool);
            SetPrivateField(spawner, "_stages", sceneStages);
            SetPrivateField(spawner, "_chapterData", CreateChapter("chapter.2.cheonjedan"));

            LogAssert.Expect(LogType.Warning, IncompleteChapterWarning);
            Invoke(spawner, "ApplyChapterData");

            // 세 디렉터가 같은 판정을 내리므로 디렉터가 씬 값으로 남을 때 스포너도 함께 남아야 한다.
            Assert.That(GetPrivateField<List<WaveEnemySpawnEntry>>(spawner, "_enemyPool"), Is.SameAs(scenePool));
            Assert.That(GetPrivateField<List<MiniBossStageData>>(spawner, "_stages"), Is.SameAs(sceneStages));
        }

        [Test]
        public void MiniBossSpawner_UsesCandidateList_NotFullRoster()
        {
            MonsterData wolf = CreateMonsterData("Changgwi");
            MonsterData gimmick = CreateMonsterData("Dokkaebibul");

            ChapterData chapter = CreatePlayableChapter("chapter.1.sillim");
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry>
            {
                CreateEntry(wolf),
                CreateEntry(gimmick)
            });
            SetPrivateField(chapter, "_miniBossCandidates", new List<WaveEnemySpawnEntry> { CreateEntry(wolf) });
            SetPrivateField(chapter, "_miniBossStages", new List<MiniBossStageData> { CreateStage(3f) });

            MiniBossSpawner spawner = CreateComponent<MiniBossSpawner>();
            SetPrivateField(spawner, "_chapterData", chapter);
            Invoke(spawner, "ApplyChapterData");

            var pool = GetPrivateField<List<WaveEnemySpawnEntry>>(spawner, "_enemyPool");
            Assert.That(pool.Count, Is.EqualTo(1), "기믹 적은 미니 보스 후보에서 빠져야 한다.");
            Assert.That(pool[0].SpeciesKey, Is.SameAs(wolf));

            var stages = GetPrivateField<List<MiniBossStageData>>(spawner, "_stages");
            Assert.That(stages.Count, Is.EqualTo(1));
            Assert.That(stages[0].SpawnMinuteMark, Is.EqualTo(3f));
        }

        [Test]
        public void BossEncounterDirector_AppliesChapterBossPrefab()
        {
            EnemyHealth chapterBoss = CreatePrefab("CorruptedMountainKing");
            ChapterData chapter = CreatePlayableChapter("chapter.1.sillim");
            SetPrivateField(chapter, "_bossPrefab", chapterBoss);

            BossEncounterDirector director = CreateComponent<BossEncounterDirector>();
            SetPrivateField(director, "_chapterData", chapter);
            Invoke(director, "ApplyChapterData");

            Assert.That(GetPrivateField<EnemyHealth>(director, "_bossPrefab"), Is.SameAs(chapterBoss));
        }

        [Test]
        public void BossEncounterDirector_KeepsSceneBoss_WhenChapterIsIncomplete()
        {
            EnemyHealth sceneBoss = CreatePrefab("SceneBoss");
            ChapterData skeleton = CreateChapter("chapter.2.cheonjedan");

            BossEncounterDirector director = CreateComponent<BossEncounterDirector>();
            SetPrivateField(director, "_bossPrefab", sceneBoss);
            SetPrivateField(director, "_chapterData", skeleton);

            LogAssert.Expect(LogType.Warning, IncompleteChapterWarning);
            Invoke(director, "ApplyChapterData");

            // 골격 챕터는 통째로 무시되므로 씬의 멀쩡한 보스가 남는다.
            // (이 경로가 없으면 2장 보스 자리에 1장 산군이 조용히 등장한다.)
            Assert.That(GetPrivateField<EnemyHealth>(director, "_bossPrefab"), Is.SameAs(sceneBoss));
        }

        [Test]
        public void BossEncounterDirector_ClearsSceneBoss_WhenChapterHasNoBossByDesign()
        {
            ChapterData chapter = CreatePlayableChapter("chapter.tutorial");
            SetPrivateField(chapter, "_bossMinuteMark", 0f);
            SetPrivateField(chapter, "_bossPrefab", (EnemyHealth)null);

            BossEncounterDirector director = CreateComponent<BossEncounterDirector>();
            SetPrivateField(director, "_bossPrefab", CreatePrefab("SceneBoss"));
            SetPrivateField(director, "_chapterData", chapter);
            Invoke(director, "ApplyChapterData");

            // 유효하지만 보스가 없는 챕터(마크 0)는 씬 보스를 물려받지 않는다 — 절반만 갈아끼우면
            // 나중에 마크가 생겼을 때 다른 챕터의 보스가 튀어나온다. 마크가 0이라 어차피 보스 페이즈는 없다.
            Assert.That(GetPrivateField<EnemyHealth>(director, "_bossPrefab"), Is.Null);
        }

        /// <summary>
        /// <see cref="ChapterData.IsValid"/>를 통과하는 최소 챕터 — 로스터 1종 + 웨이브 + 보스 프리팹.
        /// 디렉터가 챕터를 통째로 검사하므로, 배선 테스트도 유효한 챕터에서 출발해야 한다.
        /// </summary>
        private ChapterData CreatePlayableChapter(string chapterId)
        {
            ChapterData chapter = CreateChapter(chapterId);
            SetPrivateField(chapter, "_enemyPool", new List<WaveEnemySpawnEntry>
            {
                CreateEntry(CreateMonsterData("Changgwi"))
            });
            SetPrivateField(chapter, "_waveDatabase", CreateWaveDatabase());
            SetPrivateField(chapter, "_bossPrefab", CreatePrefab("Boss"));
            return chapter;
        }

        private ChapterData CreateChapter(string chapterId)
        {
            ChapterData chapter = ScriptableObject.CreateInstance<ChapterData>();
            chapter.name = chapterId;
            SetPrivateField(chapter, "_chapterId", chapterId);
            _created.Add(chapter);
            return chapter;
        }

        private ChapterDatabase CreateDatabase(params ChapterData[] chapters)
        {
            ChapterDatabase database = ScriptableObject.CreateInstance<ChapterDatabase>();
            database.name = "TestChapterDatabase";
            SetPrivateField(database, "_chapters", new List<ChapterData>(chapters));
            _created.Add(database);
            return database;
        }

        private WaveDatabase CreateWaveDatabase()
        {
            WaveDatabase database = ScriptableObject.CreateInstance<WaveDatabase>();
            database.name = "TestWaveDatabase";
            SetPrivateField(database, "_waves", new List<WaveDefinition> { new WaveDefinition() });
            _created.Add(database);
            return database;
        }

        private WaveEnemySpawnEntry CreateEntry(MonsterData species)
        {
            var entry = new WaveEnemySpawnEntry();
            SetPrivateField(entry, "_monsterData", species);
            return entry;
        }

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

        private T CreateComponent<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _created.Add(go);
            return go.AddComponent<T>();
        }

        private static MiniBossStageData CreateStage(float minuteMark)
        {
            var stage = new MiniBossStageData();
            SetPrivateField(stage, "_spawnMinuteMark", minuteMark);
            return stage;
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
            method.Invoke(target, null);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field not found: {fieldName}");
            fieldInfo.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field not found: {fieldName}");
            return (T)fieldInfo.GetValue(target);
        }
    }
}
