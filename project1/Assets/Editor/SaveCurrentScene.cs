using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SaveCurrentScene
{
    public static void Execute()
    {
        // 현재 열린 씬을 원래 경로에 저장
        var scene = EditorSceneManager.GetActiveScene();
        Debug.Log($"[SaveCurrentScene] Active scene: {scene.path}");
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SaveCurrentScene] 씬 저장 완료.");
    }
}
