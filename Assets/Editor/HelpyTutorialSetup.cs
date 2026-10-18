using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HelpyTutorialSetup
{
    private const string ScenePath = "Assets/Scenes/TUTORIAL.unity";
    private const string Request = "Temp/Tutorial/apply.request";

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested();
        };
        EditorSceneManager.sceneOpened += (_,__) => ApplyRequested();
    }
    private static void ApplyRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        try { Apply(); File.Delete(Request); }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MenuItem("Tools/Punch Card/Tutorial/Apply Helpy Introduction")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Tutorial");
        EditorSceneManager.SaveScene(scene,"Temp/Tutorial/TUTORIAL-before-intro-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        var roots = scene.GetRootGameObjects();
        var intro = roots.SelectMany(r=>r.GetComponentsInChildren<HelpyTutorialIntro>(true)).FirstOrDefault();
        if (!intro)
        {
            var root = new GameObject("Helpy tutorial introduction");
            SceneManager.MoveGameObjectToScene(root,scene);
            intro = root.AddComponent<HelpyTutorialIntro>();
        }
        var all = roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var player = all.Select(t=>t.GetComponent<FirstPersonController>()).First(p=>p);
        var camera = all.Select(t=>t.GetComponent<Camera>()).First(c=>c && c.CompareTag("MainCamera"));
        var robot = roots.Single(r=>string.Equals(r.name,"robot_helper",StringComparison.OrdinalIgnoreCase));
        var hud = all.Select(t=>t.GetComponent<InteractionHUD>()).FirstOrDefault(h=>h);
        var serialized = new SerializedObject(intro);
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
        serialized.FindProperty("playerCamera").objectReferenceValue = camera;
        serialized.FindProperty("robot").objectReferenceValue = robot.transform;
        var face = robot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Cube.001");
        serialized.FindProperty("faceNormal").vector3Value = face.sharedMesh.normals[0];
        serialized.FindProperty("playerHUD").objectReferenceValue = hud ? hud.GetComponentInParent<Canvas>() : null;
        serialized.FindProperty("subtitleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var sway = intro.GetComponent<HelpyIdleSway>();
        if (!sway) sway = intro.gameObject.AddComponent<HelpyIdleSway>();
        var swaySettings = new SerializedObject(sway);
        swaySettings.FindProperty("target").objectReferenceValue = robot.transform;
        swaySettings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Tutorial/applied.txt","Helpy introduction and gentle robot sway applied to TUTORIAL only.\n");
    }
}
