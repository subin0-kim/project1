using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLevelSystem))]
    public class LevelUpPanelPresenter : MonoBehaviour
    {
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField]
        private bool _showExperienceHud = true;

        [SerializeField]
        private Vector2 _hudPosition = new Vector2(12f, 260f);

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }
        }

        private void OnGUI()
        {
            if (_playerLevelSystem == null)
            {
                return;
            }

            if (_showExperienceHud)
            {
                DrawExperienceHud();
            }

            if (_playerLevelSystem.IsSelectionOpen)
            {
                DrawLevelUpPanel();
            }
        }

        private void DrawExperienceHud()
        {
            Rect hudRect = new Rect(_hudPosition.x, _hudPosition.y, 340f, 72f);
            GUILayout.BeginArea(hudRect, GUI.skin.box);
            GUILayout.Label($"Level {_playerLevelSystem.CurrentLevel}");
            GUILayout.Label($"XP {_playerLevelSystem.CurrentExperience:0.##} / {_playerLevelSystem.CurrentThreshold:0.##}");
            GUILayout.EndArea();
        }

        private void DrawLevelUpPanel()
        {
            IReadOnlyList<LevelUpSkillDefinition> choices = _playerLevelSystem.CurrentChoices;
            if (choices == null || choices.Count <= 0)
            {
                return;
            }

            const float panelWidth = 520f;
            const float panelHeight = 360f;
            Rect panelRect = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUILayout.BeginArea(panelRect, GUI.skin.window);
            GUILayout.Label("레벨 업! 스킬 1개를 선택하세요.");
            GUILayout.Space(8f);

            for (int i = 0; i < choices.Count; i++)
            {
                LevelUpSkillDefinition choice = choices[i];
                int nextLevel = _playerLevelSystem.GetSkillLevel(choice.SkillId) + 1;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{choice.DisplayName} Lv.{nextLevel}");
                GUILayout.Label(choice.Description);

                if (GUILayout.Button("선택", GUILayout.Height(32f)))
                {
                    _playerLevelSystem.ApplyChoice(i);
                }

                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }

            GUILayout.EndArea();
        }
    }
}
