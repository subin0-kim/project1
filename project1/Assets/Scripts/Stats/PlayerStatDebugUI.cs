using System;
using UnityEngine;

namespace Mukseon.Gameplay.Stats
{
    [DisallowMultipleComponent]
    public class PlayerStatDebugUI : MonoBehaviour
    {
        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField]
        private bool _showInBuild;

        [SerializeField]
        private Vector2 _anchorPosition = new Vector2(12f, 12f);

        [SerializeField]
        private float _panelWidth = 320f;

        [SerializeField]
        private float _lineHeight = 22f;

        private void Awake()
        {
            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR
            if (!_showInBuild)
            {
                return;
            }
#endif

            if (_playerStatSystem == null)
            {
                return;
            }

            Array allStatTypes = Enum.GetValues(typeof(StatType));
            float panelHeight = (allStatTypes.Length + 1f) * _lineHeight + 20f;
            Rect panelRect = new Rect(_anchorPosition.x, _anchorPosition.y, _panelWidth, panelHeight);

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Player Stat Debug");

            foreach (StatType statType in allStatTypes)
            {
                if (!_playerStatSystem.TryGetRuntimeStat(statType, out RuntimeStat runtimeStat))
                {
                    continue;
                }

                GUILayout.Label($"{statType}: {runtimeStat.Value:0.##} (Base {runtimeStat.BaseValue:0.##}, Mods {runtimeStat.ModifierCount})");
            }

            GUILayout.EndArea();
        }
    }
}
