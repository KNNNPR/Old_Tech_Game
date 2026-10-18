using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HelpyReturnDialogueSetup
{
    public const string ScenePath = "Assets/Scenes/HELPYDIALOGUE.unity";
    private const string Request = "Temp/HelpyReturn/landing-v2.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.update += () => { if (EditorApplication.timeSinceStartup < nextCheck) return; nextCheck = EditorApplication.timeSinceStartup + 1; ApplyRequested(); };
        EditorSceneManager.sceneOpened += (_, __) => ApplyRequested();
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested(); };
    }
    private static void ApplyRequested()
    {
        if (applying || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        applying = true;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Directory.CreateDirectory("Temp/HelpyReturn");
            EditorSceneManager.SaveScene(scene,"Temp/HelpyReturn/HELPYDIALOGUE-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Configure(scene); Validate(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HelpyReturn/landing-v2-applied.txt","PASS\nHELPYDIALOGUE saved: existing Helpy and an independent in-room landing pose, appearance / idle / departure, locked camera close-up, nine two-speaker English lines with emphasis, slow retro subtitles and one-line Space skipping. Camera and controls restore afterwards.\n");
            File.Delete(Request); SceneView.RepaintAll();
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            applying = false;
        }
    }

    public static HelpyReturnDialogue Configure(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var player = all.Select(t => t.GetComponent<FirstPersonController>()).Single(p => p);
        var camera = all.Select(t => t.GetComponent<Camera>()).Single(c => c && c.CompareTag("MainCamera"));
        var robot = all.FirstOrDefault(t => string.Equals(t.name,"robot_helper",StringComparison.OrdinalIgnoreCase));
        bool createdRobot = !robot;
        var appearance = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/appear_anim.anim");
        if (!robot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Robot_helper.prefab");
            if (!prefab) throw new InvalidOperationException("Robot_helper prefab is missing.");
            robot = ((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).transform;
        }
        var face = robot.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Cube.001");
        var mesh = face.GetComponent<MeshFilter>() ? face.GetComponent<MeshFilter>().sharedMesh : ((SkinnedMeshRenderer)face).sharedMesh;
        foreach (var skinned in robot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skinned.updateWhenOffscreen = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(skinned);
        }
        var dialogue = all.Select(t => t.GetComponent<HelpyReturnDialogue>()).FirstOrDefault(d => d);
        if (!dialogue)
        {
            var root = new GameObject("Helpy return conversation"); SceneManager.MoveGameObjectToScene(root,scene);
            dialogue = root.AddComponent<HelpyReturnDialogue>();
        }
        foreach (var animator in robot.GetComponentsInChildren<Animator>(true))
        {
            // This encounter samples the authored clips itself; avoid a second root-motion driver.
            animator.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
        var landing = dialogue.transform.Find("Helpy landing pose");
        if (!landing)
        {
            Vector3 start = robot.localPosition, end = robot.localPosition;
            for (int axis = 0; axis < 3; axis++)
            {
                string property = "m_LocalPosition." + "xyz"[axis];
                var curve = AnimationUtility.GetEditorCurve(appearance, EditorCurveBinding.FloatCurve("", typeof(Transform), property));
                if (curve == null) continue;
                start[axis] = curve.Evaluate(0);
                end[axis] = curve.Evaluate(appearance.length);
            }
            // The existing actor is authored at appear_anim's first frame, above the room.
            // Keep its scene pose and store the final landing separately so animation cannot overwrite it.
            Vector3 landingLocal = createdRobot || Vector3.Distance(robot.localPosition,start) < .1f ? end : robot.localPosition;
            landing = new GameObject("Helpy landing pose").transform;
            landing.SetParent(dialogue.transform,false);
            landing.SetPositionAndRotation(robot.parent ? robot.parent.TransformPoint(landingLocal) : landingLocal,robot.rotation);
        }
        var hud = all.Select(t => t.GetComponent<InteractionHUD>()).FirstOrDefault(h => h);
        var settings = new SerializedObject(dialogue);
        settings.FindProperty("player").objectReferenceValue = player;
        settings.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
        settings.FindProperty("playerCamera").objectReferenceValue = camera;
        settings.FindProperty("playerHUD").objectReferenceValue = hud ? hud.GetComponentInParent<Canvas>() : null;
        settings.FindProperty("robot").objectReferenceValue = robot;
        settings.FindProperty("landingPose").objectReferenceValue = landing;
        settings.FindProperty("face").objectReferenceValue = face;
        settings.FindProperty("faceNormal").vector3Value = mesh.normals[0];
        settings.FindProperty("appearance").objectReferenceValue = appearance;
        settings.FindProperty("idle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/move_robot.anim");
        settings.FindProperty("subtitleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        settings.ApplyModifiedPropertiesWithoutUndo();
        return dialogue;
    }
    public static void Validate(Scene scene)
    {
        var dialogue = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HelpyReturnDialogue>(true)).Single();
        var settings = new SerializedObject(dialogue);
        foreach (var name in new[] { "player", "interactor", "playerCamera", "robot", "landingPose", "face", "appearance", "idle", "subtitleFont" })
            if (!settings.FindProperty(name).objectReferenceValue) throw new InvalidOperationException("Missing HELPYDIALOGUE reference: " + name);
        var lines = settings.FindProperty("lines");
        if (lines.arraySize != 9) throw new InvalidOperationException("Expected nine return conversation lines.");
        int[] speakers = { 0,1,0,1,0,0,1,0,0 };
        for (int i = 0; i < lines.arraySize; i++)
            if (lines.GetArrayElementAtIndex(i).FindPropertyRelative("speaker").enumValueIndex != speakers[i] || string.IsNullOrWhiteSpace(lines.GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue)) throw new InvalidOperationException("Invalid dialogue line " + i);
        if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HelpyTutorialIntro>(true)).Any(t => t.enabled)) throw new InvalidOperationException("The tutorial tour must not also run in HELPYDIALOGUE.");
    }
}
