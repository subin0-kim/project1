using System.IO;
using Mukseon.Gameplay.VFX;
using UnityEditor;
using UnityEngine;

public class SetupVFXAssets
{
    private const string TexturesPath = "Assets/Art/VFX/Textures";
    private const string MaterialsPath = "Assets/Art/VFX/Materials";
    private const string PrefabsPath = "Assets/Art/VFX/Prefabs";

    public static void Execute()
    {
        Directory.CreateDirectory(
            Path.Combine(Application.dataPath, "../", MaterialsPath));
        Directory.CreateDirectory(
            Path.Combine(Application.dataPath, "../", PrefabsPath));
        AssetDatabase.Refresh();

        Texture2D texTrail    = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesPath}/InkTrail.png");
        Texture2D texSplatter = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesPath}/InkSplatter.png");
        Texture2D texBurst    = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesPath}/InkBurst.png");

        Material matTrail    = CreateOrLoadMaterial("InkTrail_Mat",    "Sprites/Default", texTrail);
        Material matSplatter = CreateOrLoadMultiplyMat("InkSplatter_Mat", texSplatter);
        Material matBurst    = CreateOrLoadMultiplyMat("InkBurst_Mat",    texBurst);

        CreateHitEffectPrefab(matSplatter);
        CreateDeathEffectPrefab(matBurst);
        CreateExplosionPrefab(matBurst);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupVFXAssets] VFX 에셋 설정 완료! (Materials + Prefabs)");
    }

    // ── 머티리얼 ──────────────────────────────────────────────────────────

    private static Material CreateOrLoadMaterial(string name, string shaderName, Texture2D tex)
    {
        string path = $"{MaterialsPath}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find(shaderName) ?? Shader.Find("Sprites/Default"));
        if (tex != null) mat.mainTexture = tex;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material CreateOrLoadMultiplyMat(string name, Texture2D tex)
    {
        string path = $"{MaterialsPath}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        // Multiply: 흰 배경→투명, 검정→불투명. Legacy 셰이더가 없으면 Additive로 폴백.
        Shader shader = Shader.Find("Legacy Shaders/Particles/Multiply")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
        mat = new Material(shader);
        if (tex != null) mat.mainTexture = tex;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ── 파티클 프리팹 헬퍼 ────────────────────────────────────────────────

    private static ParticleSystem SetupBasePrefab(
        string goName, Material mat, int sortingOrder,
        float duration, float lifetime,
        ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size,
        Color color, float gravity, int burstMin, int burstMax, float shapeRadius)
    {
        GameObject go = new GameObject(goName);
        var ps = go.AddComponent<ParticleSystem>();
        go.AddComponent<InkParticleEffect>();

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortingLayerName = "Default";
        rend.sortingOrder = sortingOrder;

        var main = ps.main;
        main.duration          = duration;
        main.loop              = false;
        main.startLifetime     = lifetime;
        main.startSpeed        = speed;
        main.startSize         = size;
        main.startColor        = new ParticleSystem.MinMaxGradient(color);
        main.gravityModifier   = gravity;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.playOnAwake       = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstMin, (short)burstMax) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = shapeRadius;

        // 알파 페이드 아웃
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // 크기 수축
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        return ps;
    }

    private static void CreateHitEffectPrefab(Material mat)
    {
        string path = $"{PrefabsPath}/InkHitEffect.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var ps = SetupBasePrefab(
            "InkHitEffect", mat, sortingOrder: 10,
            duration: 0.3f, lifetime: 0.3f,
            speed: new ParticleSystem.MinMaxCurve(1.5f, 3f),
            size:  new ParticleSystem.MinMaxCurve(0.25f, 0.55f),
            color: new Color(0.05f, 0.02f, 0.02f, 0.9f),
            gravity: 0.2f, burstMin: 6, burstMax: 10, shapeRadius: 0.05f);

        PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
        Object.DestroyImmediate(ps.gameObject);
    }

    private static void CreateDeathEffectPrefab(Material mat)
    {
        string path = $"{PrefabsPath}/InkDeathEffect.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var ps = SetupBasePrefab(
            "InkDeathEffect", mat, sortingOrder: 10,
            duration: 0.6f, lifetime: 0.6f,
            speed: new ParticleSystem.MinMaxCurve(2f, 5f),
            size:  new ParticleSystem.MinMaxCurve(0.5f, 1.2f),
            color: new Color(0.05f, 0.02f, 0.02f, 1f),
            gravity: 0.15f, burstMin: 12, burstMax: 18, shapeRadius: 0.1f);

        PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
        Object.DestroyImmediate(ps.gameObject);
    }

    private static void CreateExplosionPrefab(Material mat)
    {
        string path = $"{PrefabsPath}/InkExplosion.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var ps = SetupBasePrefab(
            "InkExplosion", mat, sortingOrder: 10,
            duration: 0.8f, lifetime: 0.8f,
            speed: new ParticleSystem.MinMaxCurve(3f, 7f),
            size:  new ParticleSystem.MinMaxCurve(0.8f, 2f),
            color: new Color(0.05f, 0.02f, 0.02f, 1f),
            gravity: 0.1f, burstMin: 20, burstMax: 28, shapeRadius: 0.2f);

        PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
        Object.DestroyImmediate(ps.gameObject);
    }
}
