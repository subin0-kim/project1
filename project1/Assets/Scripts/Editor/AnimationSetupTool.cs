using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds Attack animation clips from the mudang spritesheets
/// and wires them into the existing Player.controller.
///
/// mudang_attack_ru_sheet → AttackRU state (우/상 스와이프: AttackRight, AttackUp)
/// mudang_attack_ld_sheet → AttackLD state (좌/하 스와이프: AttackLeft, AttackDown)
///
/// Menu: Tools / Mukseon / Setup Player Animations
/// </summary>
public static class AnimationSetupTool
{
    const string AttackRUSheetPath = "Assets/Art/Characters/Player/Sprites/mudang_attack_ru_sheet.png";
    const string AttackLDSheetPath = "Assets/Art/Characters/Player/Sprites/mudang_attack_ld_sheet.png";
    const string ControllerPath    = "Assets/Animations/Player.controller";
    const string AttackRUClipPath  = "Assets/Animations/Player_Attack_RU.anim";
    const string AttackLDClipPath  = "Assets/Animations/Player_Attack_LD.anim";
    const float  FPS               = 12f;

    // 우/상 스와이프 트리거 → AttackRU 상태
    static readonly string[] RUTriggers = { "AttackRight", "AttackUp" };
    // 좌/하 스와이프 트리거 → AttackLD 상태
    static readonly string[] LDTriggers = { "AttackLeft", "AttackDown" };

    [MenuItem("Tools/Mukseon/Setup Player Animations")]
    public static void SetupAnimations()
    {
        // 1. Load sprite frames from sheets
        var ruFrames = LoadSprites(AttackRUSheetPath);
        var ldFrames = LoadSprites(AttackLDSheetPath);

        if (ruFrames.Length == 0 || ldFrames.Length == 0)
        {
            Debug.LogError("[AnimationSetupTool] Could not load sprite frames. " +
                           "Make sure spritesheets are imported as Multiple sprites first " +
                           "(run Tools/Mukseon/Apply Game Sprites).");
            return;
        }

        Debug.Log($"[AnimationSetupTool] Loaded {ruFrames.Length} RU frames, " +
                  $"{ldFrames.Length} LD frames.");

        // 2. Create animation clips
        var attackRUClip = CreateSpriteClip(ruFrames, AttackRUClipPath, loop: false);
        var attackLDClip = CreateSpriteClip(ldFrames, AttackLDClipPath, loop: false);

        // 3. Wire up the Animator Controller
        SetupController(attackRUClip, attackLDClip);

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

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

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

    static void SetupController(AnimationClip attackRUClip, AnimationClip attackLDClip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimationSetupTool] AnimatorController not found at {ControllerPath}");
            return;
        }

        var sm = controller.layers[0].stateMachine;

        // 기존 상태/트랜지션 제거 후 재구성
        foreach (var s in sm.states.ToArray())
            sm.RemoveState(s.state);
        foreach (var t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);

        // Idle 상태 (기본) — motion null: 공격 입력 전까지 정지 포즈 유지
        var idleState = sm.AddState("Idle");
        idleState.motion = null;
        sm.defaultState = idleState;

        // AttackRU 상태 (우/상 스와이프)
        var attackRUState = sm.AddState("AttackRU");
        attackRUState.motion = attackRUClip;

        // AttackLD 상태 (좌/하 스와이프)
        var attackLDState = sm.AddState("AttackLD");
        attackLDState.motion = attackLDClip;

        // AnyState → AttackRU (AttackRight, AttackUp 트리거)
        foreach (var trigger in RUTriggers)
        {
            EnsureParameter(controller, trigger);
            var t = sm.AddAnyStateTransition(attackRUState);
            t.hasExitTime       = false;
            t.duration          = 0f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        // AnyState → AttackLD (AttackLeft, AttackDown 트리거)
        foreach (var trigger in LDTriggers)
        {
            EnsureParameter(controller, trigger);
            var t = sm.AddAnyStateTransition(attackLDState);
            t.hasExitTime       = false;
            t.duration          = 0f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        // AttackRU → Idle (클립 종료 후)
        var backRU = attackRUState.AddTransition(idleState);
        backRU.hasExitTime = true;
        backRU.exitTime    = 1f;
        backRU.duration    = 0f;

        // AttackLD → Idle (클립 종료 후)
        var backLD = attackLDState.AddTransition(idleState);
        backLD.hasExitTime = true;
        backLD.exitTime    = 1f;
        backLD.duration    = 0f;

        EditorUtility.SetDirty(controller);
        Debug.Log("[AnimationSetupTool] Animator Controller configured: Idle / AttackRU / AttackLD.");
    }

    static void EnsureParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(p => p.name == name))
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }
}
