using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RobotHelperScreenSetup
{
    public const string ModelPath = "Assets/Models/robot_helper.fbx";
    public const string MaterialPath = "Assets/Materials/RobotHelper_CRT.mat";
    private const string RequestPath = "Temp/RobotCRT/apply.request";

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
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { Apply(); File.Delete(RequestPath); }
        catch (Exception e)
        {
            File.WriteAllText("Temp/RobotCRT/apply-error.txt", e.ToString());
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/Old Tech/Apply Retro CRT to Robot Helper")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Temp/RobotCRT");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (!material || importer == null) throw new InvalidOperationException("Robot display assets are missing.");
        if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Robot CRT shader has compilation errors.");

        // Only the apple display material is remapped; the robot's two body materials keep their assignments.
        var source = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.002");
        var remaps = importer.GetExternalObjectMap();
        if (!remaps.TryGetValue(source, out var assigned) || assigned != material)
        {
            importer.AddRemap(source, material);
            importer.SaveAndReimport();
        }

        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Cube.001" || !renderer.TryGetComponent<MeshFilter>(out var filter)
                    || AssetDatabase.GetAssetPath(filter.sharedMesh) != ModelPath) continue;
                count++;
                var materials = renderer.sharedMaterials;
                if (materials.Length != 1 || materials[0] == material) continue;
                if (!changed && !string.IsNullOrEmpty(scene.path))
                    EditorSceneManager.SaveScene(scene, "Temp/RobotCRT/" + scene.name + "-before-CRT-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
                Undo.RecordObject(renderer, "Style robot helper screen");
                materials[0] = material;
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
        }
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        File.WriteAllText("Temp/RobotCRT/applied.txt", $"Robot helper apple screen material remapped in the FBX.\nLoaded screens: {count}\n");
    }
}
