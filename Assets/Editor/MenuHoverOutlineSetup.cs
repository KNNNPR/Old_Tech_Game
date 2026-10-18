using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class MenuHoverOutlineSetup
{
    [InitializeOnLoadMethod]
    private static void OnImport() => EditorApplication.delayCall += () =>
    {
        EnsureInstalled();
        ApplyToLoadedMenu();
    };

    public static void ApplyToLoadedMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MENU.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Menu/Hover Outline.mat");
        if (!material) return;
        var controllers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ComputerMenuController>(true));
        bool changed = false;
        foreach (var controller in controllers)
        {
            var serialized = new SerializedObject(controller);
            var property = serialized.FindProperty("hoverOutlineMaterial");
            if (property.objectReferenceValue != null) continue;
            if (!changed)
            {
                Directory.CreateDirectory("Temp/Menu");
                EditorSceneManager.SaveScene(scene,"Temp/Menu/MENU-before-hover-binding-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
            }
            property.objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // Install in both quality renderers; the feature only draws the temporary hover meshes.
    public static void EnsureInstalled()
    {
        bool changed = false;
        foreach (string path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (!renderer || renderer.rendererFeatures.Exists(f => f is MenuHoverOutlineFeature)) continue;
            var feature = ScriptableObject.CreateInstance<MenuHoverOutlineFeature>();
            feature.name = "Menu Hover Outline";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            feature.Create();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);

            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; ++i)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            changed = true;
        }
        if (changed) AssetDatabase.SaveAssets();
    }
}
