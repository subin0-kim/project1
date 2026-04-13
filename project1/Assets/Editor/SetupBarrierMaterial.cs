using UnityEditor;
using UnityEngine;

public class SetupBarrierMaterial
{
    public static void Execute()
    {
        string matPath = "Assets/Art/VFX/Materials/InkBarrierRing_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Multiply")
                         ?? Shader.Find("Sprites/Default");
            mat = new Material(shader);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/VFX/Textures/InkBarrierRing.png");
            if (tex != null) mat.mainTexture = tex;
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // BarrierVisual SpriteRenderer에 머티리얼 할당
        var barrier = GameObject.Find("Player/BarrierVisual");
        if (barrier != null)
        {
            var sr = barrier.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sharedMaterial = mat;
                sr.color = Color.white; // Multiply는 흰색이어야 원본 텍스처 그대로 사용
                EditorUtility.SetDirty(sr);
                Debug.Log("[SetupBarrierMaterial] BarrierVisual에 Multiply 머티리얼 적용 완료.");
            }
        }

        AssetDatabase.SaveAssets();
    }
}
