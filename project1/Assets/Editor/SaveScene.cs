using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveScene
{
    public static void Execute()
    {
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        UnityEngine.Debug.Log("[SaveScene] 씬 저장 완료: " + EditorSceneManager.GetActiveScene().path);
    }
}
