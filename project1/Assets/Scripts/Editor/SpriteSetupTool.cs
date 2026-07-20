using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    const string PlayerAtkRUPath = "Assets/Art/Characters/Player/Sprites/mudang_attack_ru_sheet.png"; // 우/상 스와이프
    const string PlayerAtkLDPath = "Assets/Art/Characters/Player/Sprites/mudang_attack_ld_sheet.png"; // 좌/하 스와이프
    const string EnemyRefPath    = "Assets/Art/Characters/Enemies/Reference/zombie_reference.png";
    const string EnemyWalkPath   = "Assets/Art/Characters/Enemies/Sprites/zombie_walk_sheet.png";
    const string EnemyAtkPath    = "Assets/Art/Characters/Enemies/Sprites/zombie_attack_sheet.png";
    const string ScenePath       = "Assets/SampleScene.unity";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/Enemies/Dummy_Down.prefab",
        "Assets/Prefabs/Enemies/Dummy_Left.prefab",
        "Assets/Prefabs/Enemies/Dummy_Right.prefab",
        "Assets/Prefabs/Enemies/Dummy_Up.prefab",
    };

    // mudang_reference  960×1118  → PPU 1118 (레퍼런스 전용, 게임 미사용)
    // zombie_reference  728×1024  → PPU 1024 (레퍼런스 전용, 게임 미사용)
    // 스프라이트시트     1024×390  → PPU 390  (~0.4유닛 높이로 통일)
    const int PlayerRefPPU = 1118;
    const int EnemyRefPPU  = 1024;
    const int SheetPPU     = 390;

    // 슬라이싱 시 노이즈 픽셀(2px 구분선 등)을 무시하는 최소 프레임 크기
    const int MinFrameSize = 20;

    // ── Entry point ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Mukseon/Apply Game Sprites")]
    public static void ApplySprites()
    {
        // 1. 레퍼런스 스프라이트 임포트 (Single 모드 — 게임에 직접 사용하지 않음)
        SetupSingleSprite(PlayerRefPath, PlayerRefPPU);
        SetupSingleSprite(EnemyRefPath,  EnemyRefPPU);

        // 2. 스프라이트시트 임포트 + 2D 자동 슬라이싱
        SetupAndSliceSpritesheet(PlayerAtkRUPath, SheetPPU);
        SetupAndSliceSpritesheet(PlayerAtkLDPath, SheetPPU);
        SetupAndSliceSpritesheet(EnemyWalkPath,  SheetPPU);
        SetupAndSliceSpritesheet(EnemyAtkPath,   SheetPPU);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. 씬·프리팹에 시트 첫 번째 프레임 연결
        AssignEnemyPrefabSprites();
        OpenSceneAndAssignPlayer();

        Debug.Log("[SpriteSetupTool] Done – sprites applied. Press Play to see the result!");
    }

    // ── Import helpers ───────────────────────────────────────────────────────

    static void SetupSingleSprite(string path, int ppu)
    {
        var imp = GetImporter(path);
        if (imp == null) return;

        imp.textureType         = TextureImporterType.Sprite;
        imp.spriteImportMode    = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode          = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled       = false;
        imp.SaveAndReimport();
        Debug.Log($"[SpriteSetupTool] Imported (Single): {path}");
    }

    static void SetupAndSliceSpritesheet(string path, int ppu)
    {
        var imp = GetImporter(path);
        if (imp == null) return;

        imp.textureType         = TextureImporterType.Sprite;
        imp.spriteImportMode    = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode          = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled       = false;
        imp.SaveAndReimport();  // 먼저 임포트해서 텍스처를 읽을 수 있는 상태로 만듦

        AutoSliceByAlpha(path);
        Debug.Log($"[SpriteSetupTool] Imported + sliced: {path}");
    }

    // ── 2D 자동 슬라이싱 ────────────────────────────────────────────────────

    /// <summary>
    /// 알파 채널을 스캔해 열(column)·행(row) 방향의 투명 구분선을 찾고
    /// 2D 프레임 rect를 계산한 뒤 .meta 파일에 기록합니다.
    /// </summary>
    static void AutoSliceByAlpha(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) return;

        Color32[] pixels = ReadPixelsSafe(texture, out int w, out int h);
        if (pixels == null) return;

        // 열·행별 최대 알파값 계산
        int[] colMaxAlpha = new int[w];
        int[] rowMaxAlpha = new int[h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int a = pixels[y * w + x].a;
            if (a > colMaxAlpha[x]) colMaxAlpha[x] = a;
            if (a > rowMaxAlpha[y]) rowMaxAlpha[y] = a;
        }

        List<(int start, int end)> colGroups = FindGroups(colMaxAlpha, MinFrameSize);
        List<(int start, int end)> rowGroups = FindGroups(rowMaxAlpha, MinFrameSize);

        if (colGroups.Count == 0 || rowGroups.Count == 0)
        {
            Debug.LogWarning($"[SpriteSetupTool] Auto-slice found no frames in {path}; skipping.");
            return;
        }

        string baseName = Path.GetFileNameWithoutExtension(path);
        var frames = new List<SpriteMetaData>();
        int idx = 0;
        // 행 우선(row-major) 순서로 프레임 생성 — 시트를 왼→오, 위→아래 순으로 읽음
        foreach (var rg in rowGroups)
        foreach (var cg in colGroups)
        {
            int fw = cg.end - cg.start + 1;
            int fh = rg.end - rg.start + 1;
            // Unity sprite rect 좌표계는 하단 기준(bottom-up) → PNG 상단 기준 변환
            float unityY = h - (rg.start + fh);
            frames.Add(new SpriteMetaData
            {
                name      = $"{baseName}_{idx:00}",
                rect      = new Rect(cg.start, unityY, fw, fh),
                pivot     = new Vector2(0.5f, 0f),
                alignment = (int)SpriteAlignment.Custom,
                border    = Vector4.zero,
            });
            idx++;
        }

        WriteSpritesIntoMeta(path, frames);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        Debug.Log($"[SpriteSetupTool] Sliced {frames.Count} frames " +
                  $"({colGroups.Count} cols × {rowGroups.Count} rows): {path}");
    }

    /// <summary>
    /// 비파괴적으로 픽셀을 읽기 위해 RenderTexture를 경유합니다.
    /// RenderTexture 해제는 finally에서 보장합니다.
    /// </summary>
    static Color32[] ReadPixelsSafe(Texture2D texture, out int w, out int h)
    {
        w = texture.width;
        h = texture.height;

        var rt   = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();

            var pixels = readable.GetPixels32();
            Object.DestroyImmediate(readable);
            return pixels;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SpriteSetupTool] ReadPixelsSafe failed: {ex.Message}");
            return null;
        }
        finally
        {
            // 예외 발생 여부와 무관하게 반드시 해제
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>
    /// 1D 값 배열에서 알파값이 있는(> 0) 연속 구간을 찾습니다.
    /// minSize 이하의 구간(노이즈·구분선)은 무시합니다.
    /// </summary>
    static List<(int start, int end)> FindGroups(int[] values, int minSize)
    {
        var groups = new List<(int, int)>();
        bool inGroup = false;
        int start = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (!inGroup && values[i] > 0) { inGroup = true; start = i; }
            else if (inGroup && values[i] == 0)
            {
                inGroup = false;
                if (i - start >= minSize) groups.Add((start, i - 1));
            }
        }
        if (inGroup && values.Length - start >= minSize)
            groups.Add((start, values.Length - 1));
        return groups;
    }

    // ── Meta file writer ─────────────────────────────────────────────────────

    /// <summary>
    /// .meta 파일의 sprites 블록을 계산된 프레임 rect로 덮어씁니다.
    /// 이미 슬라이스된 상태(재실행)에서도 올바르게 교체됩니다.
    /// </summary>
    static void WriteSpritesIntoMeta(string assetPath, List<SpriteMetaData> frames)
    {
        string metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath)) return;

        string meta = File.ReadAllText(metaPath);

        var sb = new StringBuilder();
        sb.AppendLine("    sprites:");
        for (int i = 0; i < frames.Count; i++)
        {
            var r  = frames[i].rect;
            var p  = frames[i].pivot;
            string spriteID    = System.Guid.NewGuid().ToString("N");
            long   internalID  = 21300000L + i + 1;

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

        // 빈 배열(sprites: [])과 기존 데이터(sprites:\n    - ...) 모두 교체 가능하도록
        // outline: 섹션 직전까지의 sprites 블록 전체를 치환합니다.
        string newMeta = Regex.Replace(
            meta,
            @"    sprites:.*?(?=\n    outline:)",
            sb.ToString().TrimEnd(),
            RegexOptions.Singleline);

        File.WriteAllText(metaPath, newMeta);
    }

    // ── Scene / Prefab assignment ────────────────────────────────────────────

    /// <summary>
    /// 적 프리팹의 SpriteRenderer를 zombie_walk_sheet 첫 번째 프레임으로 설정합니다.
    /// reference PNG가 아닌 실제 게임용 시트 프레임을 사용합니다.
    /// </summary>
    static void AssignEnemyPrefabSprites()
    {
        var firstFrame = LoadFirstSpriteFrame(EnemyWalkPath);
        if (firstFrame == null)
        {
            Debug.LogError("[SpriteSetupTool] zombie_walk_sheet frame 00 not found – skipping prefab assignment.");
            return;
        }

        foreach (var prefabPath in EnemyPrefabPaths)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var sr = scope.prefabContentsRoot.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = firstFrame;
                Debug.Log($"[SpriteSetupTool] Assigned zombie_walk_sheet_00 → {prefabPath}");
            }
        }
    }

    /// <summary>
    /// 씬의 Player SpriteRenderer를 mudang_idle_sheet 첫 번째 프레임으로 설정합니다.
    /// reference PNG가 아닌 실제 게임용 시트 프레임을 사용합니다.
    /// </summary>
    static void OpenSceneAndAssignPlayer()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[SpriteSetupTool] Could not open scene: {ScenePath}");
            return;
        }

        var firstFrame = LoadFirstSpriteFrame(PlayerAtkRUPath);
        if (firstFrame == null)
        {
            Debug.LogError("[SpriteSetupTool] mudang_idle_sheet frame 00 not found – skipping player assignment.");
            return;
        }

        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go.name != "Player") continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            sr.sprite = firstFrame;
            Debug.Log("[SpriteSetupTool] Assigned mudang_idle_sheet_00 → Player (scene)");
            break;
        }

        EditorSceneManager.SaveScene(scene);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 스프라이트시트에서 이름 오름차순 기준 첫 번째 서브 스프라이트를 반환합니다.
    /// </summary>
    static Sprite LoadFirstSpriteFrame(string sheetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .FirstOrDefault();
    }

    static TextureImporter GetImporter(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
            Debug.LogError($"[SpriteSetupTool] TextureImporter not found: {path}");
        return imp;
    }
}
