using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds Idle + Attack animation clips from the mudang spritesheets
/// and wires them into the existing Player.controller.
/// Menu: Tools / Mukseon / Setup Player Animations
/// </summary>
public static class AnimationSetupTool
{
    const string IdleSheetPath   = "Assets/Art/Characters/Player/Sprites/mudang_idle_sheet.png";
    const string AttackSheetPath = "Assets/Art/Characters/Player/Sprites/mudang_attack_sheet.png";
    const string ControllerPath  = "Assets/Animations/Player.controller";
    const string IdleClipPath    = "Assets/Animations/Player_Idle.anim";
    const string AttackClipPath  = "Assets/Animations/Player_Attack.anim";
    const float  FPS             = 12f;

    static readonly string[] AttackTriggers =
        { "AttackUp", "AttackDown", "AttackLeft", "AttackRight" };

    [MenuItem("Tools/Mukseon/Setup Player Animations")]
    public static void SetupAnimations()
    {
        // 1. Load sprite frames from sheets
        var idleFrames   = LoadSprites(IdleSheetPath);
        var attackFrames = LoadSprites(AttackSheetPath);

        if (idleFrames.Length == 0 || attackFrames.Length == 0)
        {
            Debug.LogError("[AnimationSetupTool] Could not load sprite frames. " +
                           "Make sure spritesheets are imported as Multiple sprites first " +
                           "(run Tools/Mukseon/Apply Game Sprites).");
            return;
        }

        Debug.Log($"[AnimationSetupTool] Loaded {idleFrames.Length} idle frames, " +
                  $"{attackFrames.Length} attack frames.");

        // 2. Create animation clips
        var idleClip   = CreateSpriteClip(idleFrames,   IdleClipPath,   loop: true);
        var attackClip = CreateSpriteClip(attackFrames, AttackClipPath, loop: false);

        // 3. Wire up the Animator Controller
        SetupController(idleClip, attackClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AnimationSetupTool] Done! Player animations are set up. Press Play to test.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static Sprite[] LoadSprites(string path)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        return all.OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }

    static AnimationClip CreateSpriteClip(Sprite[] frames, string savePath, bool loop)
    {
        var clip = new AnimationClip { frameRate = FPS };

        // Loop / no-loop setting
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Build object-reference keyframes for SpriteRenderer.m_Sprite
        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite",
        };

        var keyframes = new ObjectReferenceKeyframe[frames.Length + (loop ? 0 : 1)];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i].time  = i / FPS;
            keyframes[i].value = frames[i];
        }

        // For non-looping clips, hold last frame for one extra frame duration
        if (!loop)
        {
            keyframes[frames.Length].time  = frames.Length / FPS;
            keyframes[frames.Length].value = frames[frames.Length - 1];
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        // Save to disk (overwrite if exists)
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationSetupTool] Updated clip: {savePath}");
            return existing;
        }

        AssetDatabase.CreateAsset(clip, savePath);
        Debug.Log($"[AnimationSetupTool] Created clip: {savePath}");
        return clip;
    }

    static void SetupController(AnimationClip idleClip, AnimationClip attackClip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimationSetupTool] AnimatorController not found at {ControllerPath}");
            return;
        }

        var sm = controller.layers[0].stateMachine;

        // Remove any existing states to start clean
        foreach (var s in sm.states.ToArray())
            sm.RemoveState(s.state);
        foreach (var t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);

        // Add Idle state (default) — no motion: player holds static pose until attacking
        var idleState = sm.AddState("Idle");
        idleState.motion = null;
        sm.defaultState = idleState;

        // Add Attack state
        var attackState = sm.AddState("Attack");
        attackState.motion = attackClip;

        // Any State → Attack (on each attack trigger)
        foreach (var trigger in AttackTriggers)
        {
            // Ensure the parameter exists
            if (!controller.parameters.Any(p => p.name == trigger))
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var t = sm.AddAnyStateTransition(attackState);
            t.hasExitTime       = false;
            t.duration          = 0f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        // Attack → Idle (when clip finishes)
        var backTransition = attackState.AddTransition(idleState);
        backTransition.hasExitTime = true;
        backTransition.exitTime    = 1f;
        backTransition.duration    = 0f;

        EditorUtility.SetDirty(controller);
        Debug.Log("[AnimationSetupTool] Animator Controller states configured.");
    }
}
