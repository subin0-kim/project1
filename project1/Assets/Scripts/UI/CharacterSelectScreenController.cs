using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Persistence;
using Mukseon.Gameplay.Stats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 캐릭터 선택 화면(#36). <see cref="CharacterDatabase"/>를 순회해 카드를 만들고, 세이브의 해금 목록과
    /// 대조해 잠금 여부를 표시한다. 선택 결과는 <see cref="RunContext"/>에 실려 게임플레이 씬으로 넘어간다.
    ///
    /// 스테이지 선택은 이번 범위 밖이다(2·3장 챕터 데이터가 아직 없다 — #64).
    /// </summary>
    public class CharacterSelectScreenController : ScreenControllerBase
    {
        private const int SelectSortingOrder = 700;
        private const float CardWidth = 340f;
        private const float CardHeight = 420f;

        private static class Strings
        {
            public const string Title = "캐릭터 선택";
            public const string Locked = "잠금";
            public const string Back = "뒤로";
            public const string Health = "체력";
            public const string AttackPower = "공격력";
            public const string AttackDamage = "기본 공격";
            public const string Missing = "캐릭터 데이터가 없습니다.";
        }

        [SerializeField, Tooltip("선택 가능한 캐릭터 목록. 비우면 화면이 빈 채로 뜬다.")]
        private CharacterDatabase _characterDatabase;

        protected override int SortingOrder => SelectSortingOrder;

        protected override void BuildUi(VisualElement root)
        {
            VisualElement screen = ScreenUiFactory.Screen(root, ScreenUiFactory.Backdrop);

            Label title = ScreenUiFactory.Text(screen, Strings.Title, 48, ScreenUiFactory.Ink);
            title.style.marginBottom = 48f;

            if (_characterDatabase == null)
            {
                Debug.LogWarning("[CharacterSelectScreenController] CharacterDatabase가 비어 있습니다.", this);
                ScreenUiFactory.Text(screen, Strings.Missing, 24, ScreenUiFactory.InkDim);
                BuildBackButton(screen);
                return;
            }

            if (!_characterDatabase.IsValid(out string reason))
            {
                Debug.LogWarning($"[CharacterSelectScreenController] CharacterDatabase가 유효하지 않습니다: {reason}", this);
            }

            List<string> unlocked = SaveGateway.Current.UnlockedCharacters;
            VisualElement row = ScreenUiFactory.Row(screen);
            row.style.marginBottom = 48f;

            IReadOnlyList<CharacterData> characters = _characterDatabase.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterData character = characters[i];
                if (character == null)
                {
                    continue;
                }

                BuildCard(row, character, unlocked.Contains(character.CharacterId));
            }

            BuildBackButton(screen);
        }

        private void BuildCard(VisualElement parent, CharacterData character, bool isUnlocked)
        {
            // 클로저가 순회 변수를 캡처하지 않도록 지역 복사본을 쓴다(HUD 레벨업 카드와 동일한 패턴).
            CharacterData captured = character;

            Button card = ScreenUiFactory.CardButton(
                parent,
                () => HandleSelect(captured, isUnlocked),
                CardWidth,
                CardHeight);

            card.style.backgroundColor = isUnlocked ? ScreenUiFactory.CardFace : ScreenUiFactory.CardFaceLocked;
            card.SetEnabled(isUnlocked);

            Color nameColor = isUnlocked ? ScreenUiFactory.Ink : ScreenUiFactory.InkDim;
            Label nameLabel = ScreenUiFactory.Text(card, character.DisplayName, 36, nameColor);
            nameLabel.style.marginBottom = 24f;

            PlayerStatsDefinition stats = character.InitialStats;
            float statsWidth = CardWidth - 72f;

            ScreenUiFactory.StatRow(card, Strings.Health, statsWidth).text =
                FormatStat(ReadStat(stats, StatType.MaxHealth));
            ScreenUiFactory.StatRow(card, Strings.AttackPower, statsWidth).text =
                FormatStat(ReadStat(stats, StatType.AttackPower));
            ScreenUiFactory.StatRow(card, Strings.AttackDamage, statsWidth).text =
                FormatStat(character.BaseAttackDamage);

            if (!isUnlocked)
            {
                Label locked = ScreenUiFactory.Text(card, Strings.Locked, 28, ScreenUiFactory.Seal);
                locked.style.marginTop = 32f;
            }
        }

        private void BuildBackButton(VisualElement parent)
        {
            ScreenUiFactory.MenuButton(parent, Strings.Back, () =>
            {
                if (!ScreenFlow.IsTransitioning)
                {
                    ScreenFlow.LoadTitle();
                }
            });
        }

        private void HandleSelect(CharacterData character, bool isUnlocked)
        {
            if (!isUnlocked || ScreenFlow.IsTransitioning)
            {
                return;
            }

            RunContext.SelectedCharacter = character;
            ScreenFlow.LoadGameplay();
        }

        /// <summary>능력치 미리보기용 조회. 정의에 없는 스탯은 0으로 표시된다.</summary>
        private static float ReadStat(PlayerStatsDefinition definition, StatType statType)
        {
            if (definition == null)
            {
                return 0f;
            }

            for (int i = 0; i < definition.Stats.Count; i++)
            {
                StatValueDefinition stat = definition.Stats[i];
                if (stat.StatType == statType)
                {
                    return stat.BaseValue;
                }
            }

            return 0f;
        }

        // 정수면 소수점을 떼고, 아니면 한 자리까지만 보인다(110 / 1.5 처럼).
        private static string FormatStat(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#");
        }
    }
}
