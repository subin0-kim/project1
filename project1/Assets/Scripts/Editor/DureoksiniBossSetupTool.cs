using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

public static class DureoksiniBossSetupTool
{
    private const string ReferencePath = "Assets/Art/Characters/Bosses/Dureoksini/Reference/dureoksini_reference.png";
    private const string SheetPath = "Assets/Art/Characters/Bosses/Dureoksini/Sprites/dureoksini_walk_sheet.png";
    private const string PrefabTemplatePath = "Assets/Prefabs/Enemies/Dummy_Right.prefab";
    private const string BossPrefabPath = "Assets/Prefabs/Enemies/Boss_Dureoksini.prefab";
    private const string BossMonsterDataPath = "Assets/Settings/Data/Monsters/Monster_Dureoksini.asset";
    private const string WaveDatabasePath = "Assets/Settings/SampleWaveDatabase.asset";
    [MenuItem("Tools/Mukseon/Setup Dureoksini Boss")]
    public static void SetupBoss()
    {
        SetupSingleSprite(ReferencePath, 768);
        SetupSpritesheetImport(SheetPath, 390);

        Sprite referenceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ReferencePath);
        if (referenceSprite == null)
        {
            Debug.LogError("[DureoksiniBossSetupTool] Reference sprite not found.");
            return;
        }

        EnemyHealth bossPrefab = CreateOrUpdateBossPrefab(referenceSprite);
        MonsterData monsterData = CreateOrUpdateMonsterData(bossPrefab);
        UpdateWaveDatabase(monsterData, bossPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DureoksiniBossSetupTool] Dureoksini boss setup complete.");
    }

    private static void SetupSingleSprite(string path, int ppu)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void SetupSpritesheetImport(string path, int ppu)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static EnemyHealth CreateOrUpdateBossPrefab(Sprite sprite)
    {
        if (!AssetDatabase.CopyAsset(PrefabTemplatePath, BossPrefabPath) && !File.Exists(BossPrefabPath))
        {
            Debug.LogError("[DureoksiniBossSetupTool] Failed to create boss prefab.");
        }

        using var scope = new PrefabUtility.EditPrefabContentsScope(BossPrefabPath);
        GameObject root = scope.prefabContentsRoot;
        root.name = "Boss_Dureoksini";

        SpriteRenderer spriteRenderer = root.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 6;
        }

        root.transform.localScale = new Vector3(2.2f, 2.2f, 1f);

        BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.size = new Vector2(1.25f, 1.7f);
            collider.offset = new Vector2(0f, 0.85f);
        }

        EnemyHealth health = root.GetComponent<EnemyHealth>();
        if (health != null)
        {
            SerializedObject healthSerialized = new SerializedObject(health);
            healthSerialized.FindProperty("_maxHealth").floatValue = 300f;
            healthSerialized.FindProperty("_swipeDirection").enumValueIndex = (int)SwipeDirection.Right;
            healthSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EnemyMover mover = root.GetComponent<EnemyMover>();
        if (mover != null)
        {
            SerializedObject moverSerialized = new SerializedObject(mover);
            moverSerialized.FindProperty("_movePattern").enumValueIndex = 0;
            moverSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        return AssetDatabase.LoadAssetAtPath<EnemyHealth>(BossPrefabPath);
    }

    private static MonsterData CreateOrUpdateMonsterData(EnemyHealth prefab)
    {
        MonsterData monsterData = AssetDatabase.LoadAssetAtPath<MonsterData>(BossMonsterDataPath);
        if (monsterData == null)
        {
            monsterData = ScriptableObject.CreateInstance<MonsterData>();
            AssetDatabase.CreateAsset(monsterData, BossMonsterDataPath);
        }

        SerializedObject serializedObject = new SerializedObject(monsterData);
        serializedObject.FindProperty("_monsterId").stringValue = "monster.dureoksini";
        serializedObject.FindProperty("_displayName").stringValue = "Dureoksini";
        serializedObject.FindProperty("_isBoss").boolValue = true;
        serializedObject.FindProperty("_enemyPrefab").objectReferenceValue = prefab;
        serializedObject.FindProperty("_swipeDirection").enumValueIndex = (int)SwipeDirection.Right;
        serializedObject.FindProperty("_maxHealth").floatValue = 300f;
        serializedObject.FindProperty("_moveSpeed").floatValue = 0.7f;
        serializedObject.FindProperty("_soulDropCount").intValue = 8;
        serializedObject.FindProperty("_experiencePerOrb").intValue = 5;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(monsterData);
        return monsterData;
    }

    private static void UpdateWaveDatabase(MonsterData monsterData, EnemyHealth prefab)
    {
        WaveDatabase waveDatabase = AssetDatabase.LoadAssetAtPath<WaveDatabase>(WaveDatabasePath);
        if (waveDatabase == null)
        {
            Debug.LogError("[DureoksiniBossSetupTool] WaveDatabase not found.");
            return;
        }

        SerializedObject serializedObject = new SerializedObject(waveDatabase);
        SerializedProperty waves = serializedObject.FindProperty("_waves");
        int bossWaveIndex = waves.arraySize;
        for (int i = 0; i < waves.arraySize; i++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(i);
            if (wave.FindPropertyRelative("_waveName").stringValue.Contains("Dureoksini"))
            {
                bossWaveIndex = i;
                break;
            }
        }

        if (bossWaveIndex == waves.arraySize)
        {
            waves.InsertArrayElementAtIndex(waves.arraySize);
        }

        SerializedProperty bossWave = waves.GetArrayElementAtIndex(bossWaveIndex);
        bossWave.FindPropertyRelative("_waveName").stringValue = "Wave 4 - Dureoksini";
        bossWave.FindPropertyRelative("_durationSeconds").floatValue = 0f;
        bossWave.FindPropertyRelative("_spawnIntervalSeconds").floatValue = 0.1f;
        bossWave.FindPropertyRelative("_maxAliveEnemies").intValue = 1;

        SerializedProperty enemies = bossWave.FindPropertyRelative("_enemies");
        while (enemies.arraySize > 1)
        {
            enemies.DeleteArrayElementAtIndex(enemies.arraySize - 1);
        }

        if (enemies.arraySize == 0)
        {
            enemies.InsertArrayElementAtIndex(0);
        }

        SerializedProperty entry = enemies.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("_enemyType").stringValue = "Boss";
        entry.FindPropertyRelative("_monsterData").objectReferenceValue = monsterData;
        entry.FindPropertyRelative("_enemyPrefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("_count").intValue = 1;
        entry.FindPropertyRelative("_moveSpeed").floatValue = 0.7f;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(waveDatabase);
    }
}
