using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 한 챕터(스테이지)의 진행 데이터 전체(#64). <c>wave_stage_system.md</c> "필요한 데이터 항목 &gt; 챕터 데이터" 기준으로
    /// 챕터 이름 · 분 단위 웨이브 목록 · 미니 보스 등장 설정 · 메인 보스 참조를 한 에셋에 모은다.
    ///
    /// 이전에는 같은 정보가 씬의 세 컴포넌트(<see cref="WaveCombatDirector"/> · <see cref="MiniBossSpawner"/> ·
    /// <see cref="BossEncounterDirector"/>)에 나뉘어 직렬화돼 있었다. 챕터를 바꾸려면 세 곳을 동시에 고쳐야 했고,
    /// 특히 미니 보스 분 마크는 디렉터와 스포너 양쪽에 <b>중복 입력</b>돼 어긋나면 마크만 울리고 미니 보스는
    /// 나오지 않는 조용한 버그가 됐다. 여기서는 <see cref="MiniBossMinuteMarks"/>를 차수 목록에서 파생시켜
    /// 그 중복 자체를 없앤다.
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterData", menuName = "Mukseon/Data/Chapter Data")]
    public class ChapterData : ScriptableObject
    {
        [SerializeField]
        private string _chapterId = "chapter.default";

        [SerializeField]
        private string _displayName = "Chapter";

        [Tooltip("이 챕터의 배경(게임플레이 씬) 이름. 비우면 ScreenFlow의 기본 게임플레이 씬을 쓴다.")]
        [SerializeField]
        private string _backgroundSceneName = string.Empty;

        [Header("Waves")]
        [Tooltip("분 단위 웨이브 목록. 보스 마크 전까지 시간 경과로 순차 전환된다.")]
        [SerializeField]
        private WaveDatabase _waveDatabase;

        [Header("Chapter Enemy Pool")]
        [Tooltip("이 챕터의 적 로스터 — 등장 가능한 적의 전체 목록. " +
                 "웨이브에 이 풀 밖의 적이 들어 있으면 IsValid가 실패한다(챕터 간 적 혼입 방지).")]
        [SerializeField]
        private List<WaveEnemySpawnEntry> _enemyPool = new List<WaveEnemySpawnEntry>();

        [Tooltip("미니 보스로 강화될 수 있는 적. 비워두면 로스터 전체가 후보가 된다. " +
                 "기믹형 적(도깨비불·매구 등)처럼 크게 부풀리면 전투가 성립하지 않는 종류를 빼기 위한 목록이며, " +
                 "반드시 로스터의 부분집합이어야 한다.")]
        [SerializeField]
        private List<WaveEnemySpawnEntry> _miniBossCandidates = new List<WaveEnemySpawnEntry>();

        [Header("Mini Boss")]
        [Tooltip("미니 보스 차수별 설정. 등장 분 마크는 각 차수의 SpawnMinuteMark에서 읽는다.")]
        [SerializeField]
        private List<MiniBossStageData> _miniBossStages = new List<MiniBossStageData>();

        [Header("Main Boss")]
        [Tooltip("메인 보스가 등장하는 분 마크. 이 시점에 일반 스포닝이 중단된다. 0 이하이면 보스 없음.")]
        [SerializeField, Min(0f)]
        private float _bossMinuteMark = 10f;

        [SerializeField]
        private BossData _bossData;

        [Tooltip("EnemyHealth + BossHealthComponent를 포함한 보스 프리팹.")]
        [SerializeField]
        private EnemyHealth _bossPrefab;

        // 차수 목록에서 파생되는 값이라 직렬화하지 않는다. 첫 조회 시 만들고 OnValidate에서 버린다.
        private List<float> _miniBossMinuteMarksCache;

        public string ChapterId => string.IsNullOrWhiteSpace(_chapterId) ? name : _chapterId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        /// <summary>배경 씬 이름. 비어 있으면 null을 돌려주어 호출부가 기본 씬으로 폴백하게 한다.</summary>
        public string BackgroundSceneName =>
            string.IsNullOrWhiteSpace(_backgroundSceneName) ? null : _backgroundSceneName;

        public WaveDatabase WaveDatabase => _waveDatabase;

        /// <summary>이 챕터의 적 로스터. 웨이브가 이 밖의 적을 쓰면 <see cref="IsValid"/>가 실패한다.</summary>
        public IReadOnlyList<WaveEnemySpawnEntry> EnemyPool => _enemyPool;

        /// <summary>
        /// 미니 보스 후보. 별도로 지정하지 않았으면 로스터 전체다 —
        /// 대부분의 챕터는 구분할 이유가 없으므로 비워두는 쪽이 기본이다.
        /// </summary>
        public IReadOnlyList<WaveEnemySpawnEntry> MiniBossCandidates =>
            _miniBossCandidates != null && _miniBossCandidates.Count > 0 ? _miniBossCandidates : _enemyPool;

        public IReadOnlyList<MiniBossStageData> MiniBossStages => _miniBossStages;
        public float BossMinuteMark => Mathf.Max(0f, _bossMinuteMark);
        public BossData BossData => _bossData;
        public EnemyHealth BossPrefab => _bossPrefab;

        /// <summary>
        /// 미니 보스 분 마크 목록. <see cref="MiniBossStages"/>의 <c>SpawnMinuteMark</c>에서 파생되므로
        /// 스포너의 차수 설정과 절대 어긋나지 않는다. 0 이하인 차수는 제외한다.
        /// </summary>
        public IReadOnlyList<float> MiniBossMinuteMarks
        {
            get
            {
                if (_miniBossMinuteMarksCache == null)
                {
                    _miniBossMinuteMarksCache = BuildMiniBossMinuteMarks();
                }

                return _miniBossMinuteMarksCache;
            }
        }

        private List<float> BuildMiniBossMinuteMarks()
        {
            var marks = new List<float>();
            if (_miniBossStages == null)
            {
                return marks;
            }

            for (int i = 0; i < _miniBossStages.Count; i++)
            {
                MiniBossStageData stage = _miniBossStages[i];
                if (stage == null || stage.SpawnMinuteMark <= 0f)
                {
                    continue;
                }

                marks.Add(stage.SpawnMinuteMark);
            }

            return marks;
        }

        private void OnValidate()
        {
            // 인스펙터에서 차수를 고치면 파생 캐시를 버려 다음 조회 때 다시 만든다.
            _miniBossMinuteMarksCache = null;
        }

        /// <summary>
        /// 챕터가 플레이 가능한 상태인지 검사한다. 규칙 본문은 <see cref="ChapterValidation"/>에 있다.
        /// </summary>
        public bool IsValid(out string reason)
        {
            return ChapterValidation.Validate(this, out reason);
        }
    }
}
