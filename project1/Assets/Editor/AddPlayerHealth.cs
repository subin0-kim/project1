using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;

public class AddPlayerHealth
{
    public static void Execute()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[AddPlayerHealth] Player 오브젝트를 찾을 수 없습니다.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = player.AddComponent<PlayerHealth>();
            Debug.Log("[AddPlayerHealth] PlayerHealth 추가 완료");
        }
        else
        {
            Debug.Log("[AddPlayerHealth] PlayerHealth 이미 존재");
        }

        PlayerStatSystem statSystem = player.GetComponent<PlayerStatSystem>();
        if (statSystem != null && health != null)
        {
            SerializedObject so = new SerializedObject(health);
            so.FindProperty("_playerStatSystem").objectReferenceValue = statSystem;
            so.ApplyModifiedProperties();
            Debug.Log("[AddPlayerHealth] _playerStatSystem 연결 완료");
        }

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[AddPlayerHealth] 씬 저장 완료: " + EditorSceneManager.GetActiveScene().path);
    }
}
