using UnityEditor;
using UnityEngine;

public class FixTextureImport
{
    public static void Execute()
    {
        string[] textures = new[]
        {
            "Assets/Art/VFX/Textures/InkBarrierRing.png",
            "Assets/Art/VFX/Textures/InkTrail.png",
            "Assets/Art/VFX/Textures/InkSplatter.png",
            "Assets/Art/VFX/Textures/InkBurst.png"
        };

        foreach (string path in textures)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[FixTextureImport] Importer not found: {path}");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log($"[FixTextureImport] Sprite 설정 완료: {path}");
        }

        AssetDatabase.Refresh();
    }
}
