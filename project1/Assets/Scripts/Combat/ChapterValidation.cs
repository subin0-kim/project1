using System.Collections.Generic;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// <see cref="ChapterData"/>의 검증 규칙(#64). 에셋에서 분리한 순수 C# 클래스로,
    /// 챕터의 공개 프로퍼티만 읽는다.
    ///
    /// 여기서 걸리는 항목은 모두 <b>런타임에 조용히 실패하는</b> 종류다 — 웨이브가 비면 적이 아예 안 나오고,
    /// 로스터가 비면 미니 보스 마크만 울리고 끝나며, 보스 프리팹이 없으면 10분에 아무 일도 일어나지 않는다.
    /// 어느 쪽도 예외를 던지지 않으므로 데이터 시점에 잡아야 한다.
    /// </summary>
    public static class ChapterValidation
    {
        public static bool Validate(ChapterData chapter, out string reason)
        {
            if (chapter == null)
            {
                reason = "챕터가 비어 있습니다.";
                return false;
            }

            WaveDatabase waveDatabase = chapter.WaveDatabase;
            if (waveDatabase == null || waveDatabase.Waves == null || waveDatabase.Waves.Count == 0)
            {
                reason = "웨이브 데이터베이스가 비어 있습니다.";
                return false;
            }

            if (!ValidateEnemyPool(chapter.EnemyPool, out reason))
            {
                return false;
            }

            if (!ValidateMiniBossStages(chapter, out reason))
            {
                return false;
            }

            if (chapter.BossMinuteMark > 0f && chapter.BossPrefab == null)
            {
                reason = $"보스 마크({chapter.BossMinuteMark:0.#}분)가 설정됐지만 보스 프리팹이 비어 있습니다.";
                return false;
            }

            HashSet<object> rosterKeys = BuildSpeciesKeys(chapter.EnemyPool);

            if (!ValidateWavesStayInsideRoster(waveDatabase, rosterKeys, out reason))
            {
                return false;
            }

            return ValidateMiniBossCandidates(chapter, rosterKeys, out reason);
        }

        /// <summary>
        /// 종류 판별 키 집합. 키가 문자열로 폴백된 경우에도 <see cref="HashSet{T}"/>의 기본 비교자가
        /// <c>Equals</c>를 쓰므로 값 비교가 된다(#70에서 참조 비교로 깨졌던 것과 같은 이유).
        /// </summary>
        private static HashSet<object> BuildSpeciesKeys(IReadOnlyList<WaveEnemySpawnEntry> entries)
        {
            var keys = new HashSet<object>();
            for (int i = 0; i < entries.Count; i++)
            {
                keys.Add(entries[i].SpeciesKey);
            }

            return keys;
        }

        private static bool ValidateEnemyPool(IReadOnlyList<WaveEnemySpawnEntry> roster, out string reason)
        {
            if (roster == null || roster.Count == 0)
            {
                reason = "챕터 적 풀이 비어 있습니다. 미니 보스가 선택할 적이 없습니다.";
                return false;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                WaveEnemySpawnEntry entry = roster[i];
                if (entry == null)
                {
                    reason = $"적 풀 {i}번 항목이 비어 있습니다.";
                    return false;
                }

                if (!entry.IsValid(out string entryReason))
                {
                    reason = $"적 풀 {i}번 항목이 유효하지 않습니다: {entryReason}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 차수는 시간순으로 <b>엄격히 증가</b>해야 하고 모두 보스 마크보다 앞서야 한다.
        /// 마크가 중복되면 <see cref="MiniBossSpawner"/>가 먼저 등록된 차수만 쓰고 나머지를 조용히 버리며,
        /// 보스 마크 이후의 차수는 영영 발행되지 않는다.
        /// </summary>
        private static bool ValidateMiniBossStages(ChapterData chapter, out string reason)
        {
            IReadOnlyList<MiniBossStageData> stages = chapter.MiniBossStages;
            if (stages == null || stages.Count == 0)
            {
                // 미니 보스 없는 챕터도 성립한다(튜토리얼 등).
                reason = string.Empty;
                return true;
            }

            float bossMinuteMark = chapter.BossMinuteMark;
            float previousMark = 0f;

            for (int i = 0; i < stages.Count; i++)
            {
                MiniBossStageData stage = stages[i];
                if (stage == null)
                {
                    reason = $"미니 보스 {i}번 차수가 비어 있습니다.";
                    return false;
                }

                float mark = stage.SpawnMinuteMark;
                if (mark <= previousMark)
                {
                    reason = $"미니 보스 분 마크는 오름차순이어야 합니다. {i}번 차수={mark:0.#} <= 이전={previousMark:0.#}";
                    return false;
                }

                if (bossMinuteMark > 0f && mark >= bossMinuteMark)
                {
                    reason = $"미니 보스 {i}번 차수({mark:0.#}분)가 보스 마크({bossMinuteMark:0.#}분) 이후입니다. " +
                             "보스 진입 후에는 마크가 발행되지 않습니다.";
                    return false;
                }

                previousMark = mark;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 웨이브에 등장하는 모든 적이 챕터 로스터 안에 있는지 검사한다 —
        /// #64 DoD "챕터 진행 중 다른 챕터의 적이 등장하지 않는다"의 실제 강제 지점.
        /// </summary>
        private static bool ValidateWavesStayInsideRoster(
            WaveDatabase waveDatabase,
            HashSet<object> rosterKeys,
            out string reason)
        {
            IReadOnlyList<WaveDefinition> waves = waveDatabase.Waves;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                WaveDefinition wave = waves[waveIndex];
                if (wave == null || wave.Enemies == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < wave.Enemies.Count; entryIndex++)
                {
                    WaveEnemySpawnEntry entry = wave.Enemies[entryIndex];
                    if (entry == null || !entry.IsValid(out _))
                    {
                        // 엔트리 자체의 유효성은 WaveCombatDirector가 스폰 시점에 경고한다. 여기서는 소속만 본다.
                        continue;
                    }

                    if (!rosterKeys.Contains(entry.SpeciesKey))
                    {
                        reason = $"웨이브 '{wave.WaveName}'의 적 '{entry.DisplayName}'이(가) 챕터 적 풀에 없습니다.";
                        return false;
                    }
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 미니 보스 후보는 로스터의 부분집합이어야 한다. 로스터에 없는 적을 후보로 두면
        /// 챕터에 등장하지 않던 적이 미니 보스로만 튀어나온다 — 격리 검증을 우회하는 뒷문이 된다.
        /// </summary>
        private static bool ValidateMiniBossCandidates(
            ChapterData chapter,
            HashSet<object> rosterKeys,
            out string reason)
        {
            IReadOnlyList<WaveEnemySpawnEntry> candidates = chapter.MiniBossCandidates;

            // 후보를 비워두면 MiniBossCandidates가 로스터 자체를 돌려주므로 검사할 게 없다.
            if (ReferenceEquals(candidates, chapter.EnemyPool))
            {
                reason = string.Empty;
                return true;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                WaveEnemySpawnEntry candidate = candidates[i];
                if (candidate == null)
                {
                    reason = $"미니 보스 후보 {i}번 항목이 비어 있습니다.";
                    return false;
                }

                if (!candidate.IsValid(out string candidateReason))
                {
                    reason = $"미니 보스 후보 {i}번 항목이 유효하지 않습니다: {candidateReason}";
                    return false;
                }

                if (!rosterKeys.Contains(candidate.SpeciesKey))
                {
                    reason = $"미니 보스 후보 '{candidate.DisplayName}'이(가) 챕터 적 로스터에 없습니다.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
