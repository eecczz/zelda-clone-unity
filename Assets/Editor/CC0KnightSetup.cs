using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class CC0KnightSetup
{
    private const string ModelPath = "Assets/Resources/CC0/KnightCharacter.fbx";
    private const string ControllerPath = "Assets/Resources/CC0/KnightCombat.controller";

    static CC0KnightSetup() => EditorApplication.delayCall += Build;

    [MenuItem("Tools/Broken Edge/Build Knight Animator")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null)
        {
            var importedClips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (var clip in importedClips)
            {
                string lower = clip.name.ToLowerInvariant();
                bool shouldLoop = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run_swordright") || lower.EndsWith("|run");
                if (clip.loopTime != shouldLoop) { clip.loopTime = shouldLoop; clip.loopPose = shouldLoop; changed = true; }
            }
            if (changed)
            {
                importer.clipAnimations = importedClips;
                importer.SaveAndReimport();
            }
        }
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (clips.Length == 0) return;
        Debug.Log("CC0 Knight animation clips: " + string.Join(", ", clips.Select(c => c.name)));
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Bool);
        var sm = controller.layers[0].stateMachine;
        AnimationClip Find(params string[] words) => clips.FirstOrDefault(c => words.Any(w => c.name.ToLowerInvariant().Contains(w))) ?? clips[0];
        var idle = sm.AddState("Idle"); idle.motion = Find("idle"); sm.defaultState = idle;
        var move = sm.AddState("Move"); move.motion = Find("run", "walk");
        var attack = sm.AddState("Attack"); attack.motion = clips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("swordattackjump")) ?? Find("swordattack", "attack"); attack.speed=1.12f;
        var hit = sm.AddState("Hit"); hit.motion = clips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("roll_sword")) ?? Find("roll"); hit.speed=1.45f;
        var death = sm.AddState("Death"); death.motion = Find("death", "die", "dead");
        var toMove = idle.AddTransition(move); toMove.hasExitTime=false; toMove.duration=.12f; toMove.AddCondition(AnimatorConditionMode.Greater,.1f,"Speed");
        var toIdle = move.AddTransition(idle); toIdle.hasExitTime=false; toIdle.duration=.12f; toIdle.AddCondition(AnimatorConditionMode.Less,.1f,"Speed");
        foreach(var s in new[]{idle,move}) {
            var a=s.AddTransition(attack);a.hasExitTime=false;a.duration=.06f;a.AddCondition(AnimatorConditionMode.If,0,"Attack");
            var h=s.AddTransition(hit);h.hasExitTime=false;h.duration=.04f;h.AddCondition(AnimatorConditionMode.If,0,"Hit");
            var d=s.AddTransition(death);d.hasExitTime=false;d.duration=.08f;d.AddCondition(AnimatorConditionMode.If,0,"Death");
        }
        var attackOut=attack.AddTransition(idle);attackOut.hasExitTime=true;attackOut.exitTime=.88f;attackOut.duration=.12f;
        var hitOut=hit.AddTransition(idle);hitOut.hasExitTime=true;hitOut.exitTime=.9f;hitOut.duration=.1f;
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
    }
}
