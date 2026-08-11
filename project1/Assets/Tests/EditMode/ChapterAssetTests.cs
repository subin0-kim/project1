using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEditor;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 저장소에 실제로 커밋된 챕터 에셋을 로드해 검사한다(#64 DoD "3개 챕터에 대한 ChapterData 에셋이 존재한다").
    ///
    /// 코드로 만든 인스턴스가 아니라 에셋을 읽는 게 요점이다 — 참조가 끊기거나 GUID가 어긋나면
    /// 컴파일도 통과하고 다른 테스트도 다 통과하지만 게임만 조용히 비어 있게 된다.
    /// </summary>
    public class ChapterAssetTests
    {
        private const string ChaptersPath = "Assets/Settings/Data/Chapters";
        private const string Chapter1Path = ChaptersPath + "/Chapter1_Sillim.asset";
        private const string DatabasePath = ChaptersPath + "/ChapterDatabase.asset";

        [Test]
        public void Chapter1_Asset_IsFullyAuthoredAndValid()
        {
            ChapterData chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(Chapter1Path);
            Assert.That(chapter, Is.Not.Null, $"챕터 에셋을 찾을 수 없습니다: {Chapter1Path}");

            Assert.That(chapter.IsValid(out string reason), Is.True, reason);
            Assert.That(chapter.WaveDatabase, Is.Not.Null);
            Assert.That(chapter.BossPrefab, Is.Not.Null);
            Assert.That(chapter.BossData, Is.Not.Null);
        }

        [Test]
        public void Chapter1_Asset_KeepsTheTimelineTheSceneUsedToOwn()
        {
            ChapterData chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(Chapter1Path);

            // 씬 세 컴포넌트에 흩어져 있던 값을 그대로 옮겨왔는지 — 여기가 어긋나면 챕터 1의 난이도 곡선이 바뀐다.
            Assert.That(chapter.MiniBossMinuteMarks, Is.EqualTo(new[] { 3f, 6f, 9f }));
            Assert.That(chapter.BossMinuteMark, Is.EqualTo(10f));
            Assert.That(chapter.MiniBossStages.Count, Is.EqualTo(3));
        }

        [Test]
        public void Chapter1_Asset_RosterCoversEveryWaveEnemy_AndExcludesGimmicksFromMiniBoss()
        {
            ChapterData chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(Chapter1Path);

            // 로스터 9종 = 웨이브에 등장하는 전체 종류. IsValid의 격리 검증이 이걸 보장한다.
            Assert.That(chapter.EnemyPool.Count, Is.EqualTo(9));

            // 미니 보스 후보는 7종 — 도깨비불·매구를 뺀 것은 누락이 아니라 의도된 제외다.
            // 도깨비불: Explode()에서 스스로 Kill()을 불러 차수 체력 배율과 무관하게 1.5초 만에 자멸한다.
            //           플레이어가 아무것도 안 해도 미니 보스 보상(골드·영혼·스킬 선택)이 지급된다.
            // 매구: 거리를 벌리며 투사체를 쏘는 적이라 느려져도 스와이프 사거리로 들어오지 않는다 — 처치 기회가 안 생긴다.
            Assert.That(chapter.MiniBossCandidates.Count, Is.EqualTo(7));
        }

        [Test]
        public void ChapterDatabase_Asset_ListsAllThreeChapters()
        {
            ChapterDatabase database = AssetDatabase.LoadAssetAtPath<ChapterDatabase>(DatabasePath);
            Assert.That(database, Is.Not.Null, $"챕터 목록 에셋을 찾을 수 없습니다: {DatabasePath}");

            Assert.That(database.IsValid(out string reason), Is.True, reason);
            Assert.That(database.Chapters.Count, Is.EqualTo(3));
            Assert.That(database.Find("chapter.1.sillim"), Is.Not.Null);
            Assert.That(database.Find("chapter.2.cheonjedan"), Is.Not.Null);
            Assert.That(database.Find("chapter.3.indangsu"), Is.Not.Null);
            Assert.That(database.DefaultChapter.ChapterId, Is.EqualTo("chapter.1.sillim"));
        }

        [Test]
        public void SkeletonChapters_AreKnownIncomplete_ButDoNotBreakTheList()
        {
            ChapterDatabase database = AssetDatabase.LoadAssetAtPath<ChapterDatabase>(DatabasePath);

            // 2·3장은 의도적으로 골격만 있다(적 구성은 후속 기획). 목록 검증은 통과하되
            // 개별 챕터 검증은 실패하는 게 정상이며, 그래서 플레이 가능한 1장이 화면에서 사라지지 않는다.
            //
            // reason까지 못 박는 이유: 실패 사유가 하나씩 채워질 때 무엇이 남았는지 바로 드러나야 한다.
            // 지금 남은 항목은 웨이브 데이터베이스이며, 이걸 채우면 다음 사유(적 풀)로 넘어간다.
            foreach (string chapterId in new[] { "chapter.2.cheonjedan", "chapter.3.indangsu" })
            {
                ChapterData skeleton = database.Find(chapterId);
                Assert.That(skeleton.IsValid(out string reason), Is.False, $"{chapterId}이 완성되면 이 테스트를 갱신할 것.");
                Assert.That(reason, Does.Contain("웨이브"), chapterId);

                // 보스 마크 0 = "보스 없음". 보스 프리팹이 없는 챕터가 마크만 들고 있으면
                // 10분에 BossEncounterDirector가 씬(=1장)의 보스를 그대로 띄운다.
                Assert.That(skeleton.BossMinuteMark, Is.EqualTo(0f), $"{chapterId}: 보스가 없는 챕터는 마크도 0이어야 한다.");
            }

            Assert.That(database.IsValid(out _), Is.True);
        }
    }
}
