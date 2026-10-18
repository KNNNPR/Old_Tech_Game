using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuTerminalStyleSetup
{
    private const string Request = "Temp/Menu/terminal-style.request";

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested();
        };
    }

    private static void ApplyRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { Apply(); File.Delete(Request); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void ConfigureCanvas(Canvas canvas)
    {
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = new Color(.23f,.85f,.38f);
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = Mathf.Max(text.characterSpacing,1.5f);
            EditorUtility.SetDirty(text);
        }
        foreach (var feedback in canvas.GetComponentsInChildren<MenuButtonFeedback>(true))
        {
            feedback.UseTerminalPalette();
            EditorUtility.SetDirty(feedback);
        }
    }

    [MenuItem("Tools/Punch Card/Menu/Apply Green Terminal Style")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Temp/Menu");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Menu/Computer CRT.mat");
        if (!material) throw new InvalidOperationException("Menu CRT material is missing.");
        foreach (string path in new[] { "Assets/Scenes/MENU.unity", "Assets/Scenes/scene_1.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            // The saved scenes are already patched; refresh any loaded editor copy as well.
            if (!scene.IsValid() || !scene.isLoaded) continue;
            EditorSceneManager.SaveScene(scene,"Temp/Menu/" + scene.name + "-before-terminal-style-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity",true);
            var roots = scene.GetRootGameObjects();
            if (scene.name == "MENU")
            {
                var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
                    .Single(c => c.name == "Computer screen interface");
                ConfigureCanvas(canvas);
            }
            else
            {
                var screen = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true))
                    .Single(r => r.name == "Monitor");
                Undo.RecordObject(screen,"Use menu CRT on main computer");
                var materials = screen.sharedMaterials;
                materials[0] = material;
                screen.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        File.WriteAllText("Temp/Menu/terminal-style-applied.txt","MENU computer text uses the green terminal palette. Main scene monitor uses Computer CRT.\n");
    }
}
