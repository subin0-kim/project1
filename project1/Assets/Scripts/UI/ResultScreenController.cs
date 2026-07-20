using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 결과 화면(#36). 게임플레이 씬 위에 오버레이로 뜬다 — 결산 데이터가 그 런에 속하고, 재도전이
    /// 곧 씬 리로드라 별도 씬으로 뺄 이유가 없다.
    ///
    /// 정화 실패는 <see cref="GameOverHandler.OnGameOver"/>, 정화 성공은
    /// <see cref="BossEncounterDirector.OnChapterCleared"/>로 들어온다.
    /// (<see cref="WaveCombatDirector.OnAllWavesCompleted"/>는 보스 <b>등장</b> 시점에 발행되므로
    /// 성공 신호가 아니다.)
    ///
    /// 광고 2배 보상과 골드/영혼 결산은 이번 범위 밖이다(광고 SDK·골드 시스템 부재).
    /// </summary>
    public class ResultScreenController : ScreenControllerBase
    {
        // 인게임 HUD(500) 위에 떠야 한다. 화면 전환 페이드(1000)보다는 아래.
        private const int ResultSortingOrder = 600;
        private const float PanelWidth = 560f;

        private static class Strings
        {
            public const string Cleared = "정화 성공";
            public const string Failed = "정화 실패";
            public const string SurvivalTime = "생존 시간";
            public const string Kills = "처치한 몬스터";
            public const string Souls = "모은 혼불";
            public const string Retry = "재도전";
            public const string ToTitle = "타이틀로";
        }

        [SerializeField, Tooltip("비우면 씬에서 찾는다.")]
        private GameOverHandler _gameOverHandler;

        [SerializeField, Tooltip("비우면 씬에서 찾는다. 없으면 정화 성공 경로가 동작하지 않는다.")]
        private BossEncounterDirector _bossEncounterDirector;

        [SerializeField, Tooltip("비우면 씬에서 찾는다. 없으면 결산이 0으로 표시된다.")]
        private RunStatsTracker _runStatsTracker;

        private Label _headline;
        private Label _survivalValue;
        private Label _killsValue;
        private Label _soulsValue;
        private bool _isShown;

        protected override int SortingOrder => ResultSortingOrder;

        protected override void Awake()
        {
            base.Awake();
            SetVisible(false);
        }

        private void OnEnable()
        {
            ResolveSources();

            if (_gameOverHandler != null)
            {
                _gameOverHandler.OnGameOver += HandleGameOver;
            }

            if (_bossEncounterDirector != null)
            {
                _bossEncounterDirector.OnChapterCleared += HandleChapterCleared;
            }
        }

        private void OnDisable()
        {
            if (_gameOverHandler != null)
            {
                _gameOverHandler.OnGameOver -= HandleGameOver;
            }

            if (_bossEncounterDirector != null)
            {
                _bossEncounterDirector.OnChapterCleared -= HandleChapterCleared;
            }

            // 결과 화면이 정지를 건 채 씬이 내려가면 정지 원인이 남는다(#109와 같은 종류의 누수).
            if (_isShown)
            {
                TimeScaleService.SetPause(PauseReason.ResultScreen, false);
                _isShown = false;
            }
        }

        private void ResolveSources()
        {
            if (_gameOverHandler == null)
            {
                _gameOverHandler = SceneObjectFinder.Find<GameOverHandler>();
            }

            if (_bossEncounterDirector == null)
            {
                _bossEncounterDirector = SceneObjectFinder.Find<BossEncounterDirector>();
            }

            if (_runStatsTracker == null)
            {
                _runStatsTracker = SceneObjectFinder.Find<RunStatsTracker>();
            }
        }

        protected override void BuildUi(VisualElement root)
        {
            // 반투명 암막 위에 패널을 올려, 뒤의 전투 화면이 비치되 초점은 결산에 오게 한다.
            VisualElement screen = ScreenUiFactory.Screen(root, new Color(0f, 0f, 0f, 0.72f));

            var panel = new VisualElement();
            panel.style.width = PanelWidth;
            panel.style.paddingTop = 40f;
            panel.style.paddingBottom = 40f;
            panel.style.paddingLeft = 40f;
            panel.style.paddingRight = 40f;
            panel.style.backgroundColor = ScreenUiFactory.CardFace;
            panel.style.alignItems = Align.Center;
            ScreenUiFactory.SetBorderRadius(panel, 10f);
            screen.Add(panel);

            _headline = ScreenUiFactory.Text(panel, string.Empty, 44, ScreenUiFactory.Ink);
            _headline.style.marginBottom = 32f;

            float rowWidth = PanelWidth - 80f;
            _survivalValue = ScreenUiFactory.StatRow(panel, Strings.SurvivalTime, rowWidth);
            _killsValue = ScreenUiFactory.StatRow(panel, Strings.Kills, rowWidth);
            _soulsValue = ScreenUiFactory.StatRow(panel, Strings.Souls, rowWidth);

            VisualElement buttons = ScreenUiFactory.Row(panel);
            buttons.style.marginTop = 32f;

            ScreenUiFactory.MenuButton(buttons, Strings.Retry, HandleRetry);
            ScreenUiFactory.MenuButton(buttons, Strings.ToTitle, HandleToTitle);
        }

        private void HandleGameOver() => Show(Strings.Failed);

        private void HandleChapterCleared() => Show(Strings.Cleared);

        private void Show(string headline)
        {
            if (_isShown)
            {
                return;
            }

            _isShown = true;
            _headline.text = headline;
            _headline.style.color = headline == Strings.Cleared ? ScreenUiFactory.Ink : ScreenUiFactory.Seal;

            RunStats stats = _runStatsTracker != null ? _runStatsTracker.Stats : null;
            _survivalValue.text = RunStats.FormatDuration(stats != null ? stats.SurvivalSeconds : 0f);
            _killsValue.text = (stats != null ? stats.KillCount : 0).ToString();
            _soulsValue.text = (stats != null ? stats.SoulCollected : 0).ToString();

            // 정화 실패는 GameOverHandler가 이미 정지시켰지만, 정화 성공은 아무도 정지시키지 않는다.
            // 결과 화면이 자기 정지를 소유하면 두 경로가 같은 상태로 수렴한다.
            TimeScaleService.SetPause(PauseReason.ResultScreen, true);
            GameplayInputGate.SetSuppression(InputSuppressionReason.ResultScreen, true);

            SetVisible(true);
        }

        private void HandleRetry()
        {
            if (ScreenFlow.IsTransitioning)
            {
                return;
            }

            ScreenFlow.ReloadGameplay();
        }

        private void HandleToTitle()
        {
            if (ScreenFlow.IsTransitioning)
            {
                return;
            }

            ScreenFlow.LoadTitle();
        }
    }
}
