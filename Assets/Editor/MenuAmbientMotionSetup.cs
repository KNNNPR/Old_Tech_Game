using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuAmbientMotionSetup
{
    private const string Request = "Temp/Menu/ambient-motion.request";

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested();
        };
        EditorSceneManager.sceneOpened += (_, __) => ApplyRequested();
    }

    private static void ApplyRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MENU.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        ApplyToLoadedMenu();
        File.Delete(Request);
    }

    [MenuItem("Tools/Punch Card/Menu/Apply Camera and Light Sway")]
    public static void ApplyToLoadedMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MENU.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Menu");
        EditorSceneManager.SaveScene(scene, "Temp/Menu/MENU-before-ambient-motion-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        if (Configure(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        File.WriteAllText("Temp/Menu/ambient-motion-applied.txt", "Camera sway and main light pendulum configured in MENU.\n");
    }

    public static bool Configure(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        var keyLight = roots.SelectMany(r => r.GetComponentsInChildren<Light>()).First(l => l.type == LightType.Directional && l.enabled);
        bool changed = false;
        if (!camera.GetComponent<MenuCameraSway>()) { camera.gameObject.AddComponent<MenuCameraSway>(); changed = true; }
        if (!keyLight.GetComponent<MenuLightSway>()) { keyLight.gameObject.AddComponent<MenuLightSway>(); changed = true; }
        return changed;
    }
}
