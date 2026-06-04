using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Mukseon.Gameplay.Combat;

public class CreateDarknessOverlay
{
    [MenuItem("Tools/Mukseon/Create Darkness Overlay")]
    public static void Execute()
    {
        // 기존 DarknessOverlayCanvas 제거
        GameObject existing = GameObject.Find("DarknessOverlayCanvas");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        // Canvas 생성
        GameObject canvasGO = new GameObject("DarknessOverlayCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // DarknessOverlay 오브젝트 (Canvas 하위)
        GameObject overlayGO = new GameObject("DarknessOverlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlayGO.AddComponent<CanvasGroup>();
        DarknessOverlay overlay = overlayGO.AddComponent<DarknessOverlay>();

        // InkSplatter 스프라이트 연결
        Sprite inkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/VFX/Textures/InkSplatter.png");
        if (inkSprite != null)
        {
            var so = new SerializedObject(overlay);
            so.FindProperty("_inkSplatterSprite").objectReferenceValue = inkSprite;
            so.ApplyModifiedProperties();
            Debug.Log("[CreateDarknessOverlay] InkSplatter 스프라이트 연결 완료.");
        }
        else
        {
            Debug.LogWarning("[CreateDarknessOverlay] InkSplatter.png를 찾지 못했습니다.");
        }

        // 씬 저장 마킹
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[CreateDarknessOverlay] DarknessOverlayCanvas 생성 완료.");
    }
}
