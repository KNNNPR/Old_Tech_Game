using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IntermissionBuildingTwitchSetup
{
    public const string ScenePath = "Assets/Scenes/Intermission.unity";
    private const string ShaderName = "Old Tech/PSX Building Twitch";
    private const string Folder = "Assets/Materials/Intermission Buildings";
    private const string Request = "Temp/BuildingTwitch/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.update += () =>
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            ApplyRequested();
        };
    }
    private static void ApplyRequested()
    {
        if (applying || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        applying = true;
        try { Apply(scene); File.Delete(Request); }
        catch (Exception e) { Debug.LogException(e); }
        finally { applying = false; }
    }
    [MenuItem("Tools/Punch Card/Set Up Intermission Building Twitch")]
    public static void ApplyOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) Apply(scene);
    }
    private static void Apply(Scene scene)
    {
        Directory.CreateDirectory("Temp/BuildingTwitch");
        EditorSceneManager.SaveScene(scene,"Temp/BuildingTwitch/Intermission-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity",true);
        int count = Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/BuildingTwitch/applied.txt",$"PASS\n{count} building/window renderers in the first Intermission use scene-specific PSX materials: 240-line vertex snapping with 8 Hz subtle poly twitch. Original material textures, emission, transforms, meshes and colliders are preserved.\n");
        SceneView.RepaintAll();
    }

    public static bool IsBuildingMaterial(Material material)
    {
        if (!material) return false;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/khruschevka.png");
        return material.shader.name == ShaderName || material.name.StartsWith("khruschevka",StringComparison.OrdinalIgnoreCase)
            || (texture && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == texture);
    }
    public static int Configure(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Only the first Intermission may be configured here.");
        var shader = Shader.Find(ShaderName);
        if (!shader) throw new InvalidOperationException("PSX building shader is missing.");
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (Directory.Exists(Folder)) AssetDatabase.ImportAsset(Folder,ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Materials","Intermission Buildings");
        }
        var replacements = new Dictionary<Material,Material>();
        int count = 0;
        foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!IsBuildingMaterial(source)) continue;
                if (!replacements.TryGetValue(source,out var clone))
                {
                    if (source.shader == shader) clone = source;
                    else
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
                        string path = Folder + "/" + source.name + " - " + guid.Substring(0,8) + " - " + localId + ".mat";
                        clone = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!clone) { clone = new Material(shader); AssetDatabase.CreateAsset(clone,path); }
                        clone.shader = shader;
                        clone.CopyPropertiesFromMaterial(source);
                        clone.shaderKeywords = source.shaderKeywords;
                        clone.enableInstancing = source.enableInstancing;
                        clone.globalIlluminationFlags = source.globalIlluminationFlags;
                    }
                    clone.SetVector("_PSXTwitch",new Vector4(.38f,8,240,1));
                    clone.SetFloat("_PSXPreviewTime",-1);
                    EditorUtility.SetDirty(clone);
                    replacements.Add(source,clone);
                }
                materials[i] = clone; changed = true;
            }
            if (!changed) continue;
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("No Khrushchyovka building materials found.");
        AssetDatabase.SaveAssets();
        return count;
    }
}
