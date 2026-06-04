using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Mukseon.Gameplay.Combat;

/// <summary>
/// DarknessOverlay 프리팹을 Assets/Prefabs/Enemies/ 경로에 생성한다.
/// 생성된 프리팹을 EodukshiniEnemy 인스펙터의 Overlay Prefab 필드에 할당하면 된다.
/// </summary>
public class CreateDarknessOverlay
{
    private const string PrefabSavePath = "Assets/Prefabs/Enemies/DarknessOverlay.prefab";

    [MenuItem("Tools/Mukseon/Create Darkness Overlay Prefab")]
    public static void Execute()
    {
        // 임시 씬 오브젝트 생성
        GameObject canvasGO = new GameObject("DarknessOverlayCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject overlayGO = new GameObject("DarknessOverlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlayGO.AddComponent<CanvasGroup>();
        DarknessOverlay overlay = overlayGO.AddComponent<DarknessOverlay>();

        Sprite inkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/VFX/Textures/InkSplatter.png");
        if (inkSprite != null)
        {
            var so = new SerializedObject(overlay);
            so.FindProperty("_inkSplatterSprite").objectReferenceValue = inkSprite;
            so.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[CreateDarknessOverlay] InkSplatter.png를 찾지 못했습니다. 프리팹 생성 후 수동으로 연결하세요.");
        }

        // 프리팹으로 저장 후 임시 씬 오브젝트 제거
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, PrefabSavePath);
        Object.DestroyImmediate(canvasGO);

        if (prefab != null)
        {
            Debug.Log($"[CreateDarknessOverlay] 프리팹 저장 완료: {PrefabSavePath}\n" +
                      "EodukshiniEnemy 인스펙터의 Overlay Prefab 필드에 이 프리팹을 할당하세요.");
            Selection.activeObject = prefab;
        }
        else
        {
            Debug.LogError("[CreateDarknessOverlay] 프리팹 저장에 실패했습니다.");
        }
    }
}
