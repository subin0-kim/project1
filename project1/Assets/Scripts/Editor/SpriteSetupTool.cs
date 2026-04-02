using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot tool that imports game sprites and wires them up in the scene / prefabs.
/// Menu: Tools / Mukseon / Apply Game Sprites
/// </summary>
public static class SpriteSetupTool
{
    // ── Paths ───────────────────────────────────────────────────────────────
    const string PlayerRefPath   = "Assets/Art/Characters/Player/Reference/mudang_reference.png";
    const string PlayerIdlePath  = "Assets/Art/Characters/Player/Sprites/mudang_idle_sheet.png";
    const string PlayerAtkPath   = "Assets/Art/Characters/Player/Sprites/mudang_attack_sheet.png";
    const string EnemyRefPath    = "Assets/Art/Characters/Enemies/Reference/zombie_reference.png";
    const string EnemyWalkPath   = "Assets/Art/Characters/Enemies/Sprites/zombie_walk_sheet.png";
    const string EnemyAtkPath    = "Assets/Art/Characters/Enemies/Sprites/zombie_attack_sheet.png";
    const string ScenePath       = "Assets/Scenes/SampleScene.unity";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/Enemies/Dummy_Down.prefab",
        "Assets/Prefabs/Enemies/Dummy_Left.prefab",
        "Assets/Prefabs/Enemies/Dummy_Right.prefab",
        "Assets/Prefabs/Enemies/Dummy_Up.prefab",
    };

    // ── PPU: sized so both characters are ~1 unit tall ──────────────────────
    // mudang_reference  960×1118  → PPU 1118  → 0.86×1.00 units
    // zombie_reference  728×1024  → PPU 1024  → 0.71×1.00 units
    // spritesheets      1024×390  → PPU 390   → each row is 1 unit tall
    const int PlayerRefPPU  = 1118;
    const int EnemyRefPPU   = 1024;
    const int SheetPPU      = 390;

    // ── Entry point ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Mukseon/Apply Game Sprites")]
    public static void ApplySprites()
    {
        // 1. Import reference sprites (Single mode)
        SetupSingleSprite(PlayerRefPath, PlayerRefPPU);
        SetupSingleSprite(EnemyRefPath,  EnemyRefPPU);

        // 2. Import + auto-slice spritesheets (Multiple mode)
        SetupAndSliceSpritesheet(PlayerIdlePath, SheetPPU);
        SetupAndSliceSpritesheet(PlayerAtkPath,  SheetPPU);
        SetupAndSliceSpritesheet(EnemyWalkPath,  SheetPPU);
        SetupAndSliceSpritesheet(EnemyAtkPath,   SheetPPU);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Wire up scene & prefabs
        AssignEnemyPrefabSprites();
        OpenSceneAndAssignPlayer();

        Debug.Log("[SpriteSetupTool] Done – sprites applied. Press Play to see the result!");
    }

    // ── Import helpers ───────────────────────────────────────────────────────

    static void SetupSingleSprite(string path, int ppu)
    {
        var imp = GetImporter(path);
        if (imp == null) return;

        imp.textureType          = TextureImporterType.Sprite;
        imp.spriteImportMode     = SpriteImportMode.Single;
        imp.spritePixelsPerUnit  = ppu;
        imp.filterMode           = FilterMode.Bilinear;
        imp.alphaIsTransparency  = true;
        imp.mipmapEnabled        = false;
        imp.SaveAndReimport();
        Debug.Log($"[SpriteSetupTool] Imported (Single): {path}");
    }

    static void SetupAndSliceSpritesheet(string path, int ppu)
    {
        var imp = GetImporter(path);
        if (imp == null) return;

        imp.textureType          = TextureImporterType.Sprite;
        imp.spriteImportMode     = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit  = ppu;
        imp.filterMode           = FilterMode.Bilinear;
        imp.alphaIsTransparency  = true;
        imp.mipmapEnabled        = false;
        imp.SaveAndReimport();   // first pass so texture is readable

        // Auto-slice by transparency via internal Unity utility (reflection)
        AutoSliceByAlpha(path, imp);
        Debug.Log($"[SpriteSetupTool] Imported + sliced: {path}");
    }

    // Uses UnityEditor's internal GenerateAutomaticSpriteRectangles via reflection.
    // Falls back to a single full-image rect if reflection fails.
    // Writes results back via TextureImporter.spritesheet (no extra package needed).
    static void AutoSliceByAlpha(string path, TextureImporter imp)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) return;

        Rect[] rects = TryGenerateAutoRects(texture);

        if (rects == null || rects.Length == 0)
        {
            Debug.LogWarning($"[SpriteSetupTool] Auto-slice failed for {path}; falling back to single rect.");
            rects = new[] { new Rect(0, 0, texture.width, texture.height) };
        }

        string baseName  = System.IO.Path.GetFileNameWithoutExtension(path);
        var metaList     = new List<SpriteMetaData>();
        for (int i = 0; i < rects.Length; i++)
        {
            metaList.Add(new SpriteMetaData
            {
                rect      = rects[i],
                name      = $"{baseName}_{i:00}",
                pivot     = new Vector2(0.5f, 0f),
                alignment = (int)SpriteAlignment.Custom,
                border    = Vector4.zero,
            });
        }

        // Write sprite rects directly into the .meta file (YAML patch)
        WriteSpritesIntoMeta(path, metaList);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    // Patches the TextureImporter .meta file with the supplied sprite rects.
    // Writes YAML sprite entries into the existing spriteSheet.sprites block.
    static void WriteSpritesIntoMeta(string assetPath, List<SpriteMetaData> frames)
    {
        string metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath)) return;

        string meta = File.ReadAllText(metaPath);

        // Build the sprites YAML block
        var sb = new StringBuilder();
        sb.AppendLine("    sprites:");
        for (int i = 0; i < frames.Count; i++)
        {
            var r = frames[i].rect;
            var p = frames[i].pivot;
            long internalID = 21300000L + i + 1;
            string spriteID = System.Guid.NewGuid().ToString("N");

            sb.AppendLine("    - serializedVersion: 2");
            sb.AppendLine($"      name: {frames[i].name}");
            sb.AppendLine("      rect:");
            sb.AppendLine("        serializedVersion: 2");
            sb.AppendLine($"        x: {r.x}");
            sb.AppendLine($"        y: {r.y}");
            sb.AppendLine($"        width: {r.width}");
            sb.AppendLine($"        height: {r.height}");
            sb.AppendLine($"      alignment: {frames[i].alignment}");
            sb.AppendLine($"      pivot: {{x: {p.x}, y: {p.y}}}");
            sb.AppendLine("      border: {x: 0, y: 0, z: 0, w: 0}");
            sb.AppendLine("      outline: []");
            sb.AppendLine("      physicsShape: []");
            sb.AppendLine("      tessellationDetail: 0");
            sb.AppendLine("      bones: []");
            sb.AppendLine($"      spriteID: {spriteID}");
            sb.AppendLine($"      internalID: {internalID}");
            sb.AppendLine("      vertices: []");
            sb.AppendLine("      indices: ");
            sb.AppendLine("      edges: []");
            sb.AppendLine("      weights: []");
            sb.AppendLine("      secondaryTextures: []");
        }

        // Replace "    sprites: []" with our filled block
        meta = Regex.Replace(meta, @"    sprites: \[\]", sb.ToString().TrimEnd());

        File.WriteAllText(metaPath, meta);
    }

    // Reads sprite pixels via RenderTexture (works on non-readable textures).
    // Strategy 1: column separator by background colour (works for solid-bg sheets).
    // Strategy 2: transparent alpha threshold (works for transparent sheets).
    // Strategy 3: even grid fallback based on detected frame count.
    static Rect[] TryGenerateAutoRects(Texture2D texture)
    {
        try
        {
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readable.GetPixels32();
            int w = texture.width, h = texture.height;
            Object.DestroyImmediate(readable);

            // --- Strategy 1: background-colour column separator ---
            Color32 bg = pixels[0];  // assume top-left is background
            var frames1 = FindFramesByColumnSeparator(pixels, w, h, bg, exactMatch: true);
            if (frames1 != null && frames1.Count > 1) return frames1.ToArray();

            // --- Strategy 2: alpha threshold separator (transparent sheets) ---
            var frames2 = FindFramesByAlphaColumn(pixels, w, h, alphaThreshold: 10);
            if (frames2 != null && frames2.Count > 1) return frames2.ToArray();

            // --- Strategy 3: even grid (power-of-2 column count) ---
            var frames3 = FindFramesByEvenGrid(pixels, w, h);
            if (frames3 != null && frames3.Count > 1) return frames3.ToArray();

            return null;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SpriteSetupTool] Auto-slice exception: {ex.Message}");
            return null;
        }
    }

    static List<Rect> FindFramesByColumnSeparator(Color32[] pixels, int w, int h, Color32 bg, bool exactMatch)
    {
        bool[] isSep = new bool[w];
        for (int x = 0; x < w; x++)
        {
            bool allBg = true;
            for (int y = 0; y < h; y++)
            {
                var p = pixels[y * w + x];
                bool match = exactMatch
                    ? (p.r == bg.r && p.g == bg.g && p.b == bg.b && p.a == bg.a)
                    : (Mathf.Abs(p.r - bg.r) < 15 && Mathf.Abs(p.g - bg.g) < 15 &&
                       Mathf.Abs(p.b - bg.b) < 15 && Mathf.Abs(p.a - bg.a) < 15);
                if (!match) { allBg = false; break; }
            }
            isSep[x] = allBg;
        }
        return GroupNonSeparatorColumns(isSep, w, h);
    }

    static List<Rect> FindFramesByAlphaColumn(Color32[] pixels, int w, int h, byte alphaThreshold)
    {
        bool[] isSep = new bool[w];
        for (int x = 0; x < w; x++)
        {
            bool allTransparent = true;
            for (int y = 0; y < h; y++)
            {
                if (pixels[y * w + x].a > alphaThreshold) { allTransparent = false; break; }
            }
            isSep[x] = allTransparent;
        }
        return GroupNonSeparatorColumns(isSep, w, h);
    }

    static List<Rect> FindFramesByEvenGrid(Color32[] pixels, int w, int h)
    {
        // Try column counts that divide w evenly: prefer 8, 6, 4, 10, 12
        int[] candidates = { 8, 6, 10, 4, 12, 2 };
        foreach (int cols in candidates)
        {
            if (w % cols != 0) continue;
            int cellW = w / cols;
            // Check that every divider column is visually sparse (< 10% filled pixels)
            bool valid = true;
            for (int col = 1; col < cols; col++)
            {
                int x = col * cellW;
                int filled = 0;
                for (int y = 0; y < h; y++)
                    if (pixels[y * w + x].a > 10) filled++;
                if ((float)filled / h > 0.1f) { valid = false; break; }
            }
            if (!valid) continue;
            var rects = new List<Rect>();
            for (int col = 0; col < cols; col++)
                rects.Add(new Rect(col * cellW, 0, cellW, h));
            return rects;
        }
        return null;
    }

    static List<Rect> GroupNonSeparatorColumns(bool[] isSep, int w, int h)
    {
        var frames = new List<Rect>();
        int start = -1;
        for (int x = 0; x < w; x++)
        {
            if (!isSep[x] && start < 0) start = x;
            else if (isSep[x] && start >= 0) { frames.Add(new Rect(start, 0, x - start, h)); start = -1; }
        }
        if (start >= 0) frames.Add(new Rect(start, 0, w - start, h));
        return frames.Count > 0 ? frames : null;
    }

    // ── Scene / Prefab assignment ────────────────────────────────────────────

    static void AssignEnemyPrefabSprites()
    {
        var zombieSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyRefPath);
        if (zombieSprite == null)
        {
            Debug.LogError("[SpriteSetupTool] zombie_reference sprite not found – skipping prefab assignment.");
            return;
        }

        foreach (var prefabPath in EnemyPrefabPaths)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var sr = scope.prefabContentsRoot.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = zombieSprite;
                Debug.Log($"[SpriteSetupTool] Assigned zombie sprite → {prefabPath}");
            }
        }
    }

    static void OpenSceneAndAssignPlayer()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[SpriteSetupTool] Could not open scene: {ScenePath}");
            return;
        }

        var mudangSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerRefPath);
        if (mudangSprite == null)
        {
            Debug.LogError("[SpriteSetupTool] mudang_reference sprite not found – skipping player assignment.");
            return;
        }

        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go.name != "Player") continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            sr.sprite = mudangSprite;
            Debug.Log("[SpriteSetupTool] Assigned mudang sprite → Player (scene)");
            break;
        }

        EditorSceneManager.SaveScene(scene);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    static TextureImporter GetImporter(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
            Debug.LogError($"[SpriteSetupTool] TextureImporter not found: {path}");
        return imp;
    }
}
