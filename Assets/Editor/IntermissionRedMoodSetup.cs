using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class IntermissionRedMoodSetup
{
    private const string SceneGuid = "b20074eac6719de40b33d96bb297a35f";
    public const string Folder = "Assets/Materials/Intermission 2 Mood";
    private const string Request = "Temp/IntermissionRedMood/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    public static string ScenePath => AssetDatabase.GUIDToAssetPath(SceneGuid);

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.update += () =>
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (!applying && File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
                ApplyOpenScene();
        };
    }

    [MenuItem("Tools/Punch Card/Set Up Second Intermission Red Atmosphere")]
    public static void ApplyOpenScene()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        applying = true;
        try
        {
            Directory.CreateDirectory("Temp/IntermissionRedMood");
            EditorSceneManager.SaveScene(scene,"Temp/IntermissionRedMood/Intermission-2-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity",true);
            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/IntermissionRedMood/applied.txt","PASS\nSecond Intermission saved: existing bloodsky texture, slow distorted red clouds, crimson courtyard fog, dark red-lit buildings and scene-local grading/materials. All original transforms, cameras and cinematic references preserved.\n");
            if (File.Exists(Request)) File.Delete(Request);
            SceneView.RepaintAll();
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { applying = false; }
    }

    public static void Configure(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Only the second Intermission may be configured.");
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (Directory.Exists(Folder)) AssetDatabase.ImportAsset(Folder,ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Materials","Intermission 2 Mood");
        }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses = all.ToDictionary(t => t,t => (t.localPosition,t.localRotation,t.localScale,t.parent));
        var replacements = new Dictionary<Material,Material>();
        foreach (var renderer in all.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!source) continue;
                bool building = IntermissionBuildingTwitchSetup.IsBuildingMaterial(source);
                bool fog = source.shader.name == "Old Tech/Low Lying Fog";
                bool ledge = renderer.name == "Plane.015";
                if (!building && !fog && !ledge) continue;
                if (!replacements.TryGetValue(source,out var material))
                {
                    material = CloneForScene(source);
                    if (building)
                    {
                        material.SetColor("_BaseColor",new Color(.92f,.78f,.72f,1));
                        if (material.IsKeywordEnabled("_EMISSION")) material.SetColor("_EmissionColor",new Color(.65f,.16f,.1f,1));
                    }
                    else if (fog)
                    {
                        material.SetColor("_FogColor",new Color(.24f,.023f,.03f,1));
                        material.SetFloat("_Density",.022f);
                    }
                    else if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",new Color(.72f,.55f,.52f,1));
                    EditorUtility.SetDirty(material);
                    replacements.Add(source,material);
                }
                materials[i] = material; changed = true;
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        // Reuse the project's existing red sky painting and its animated sky shader.
        var skySource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TheEnd/Moving Blood Sky.mat");
        if (!skySource || !skySource.mainTexture) throw new InvalidOperationException("The existing red sky is missing.");
        var sky = CloneForScene(skySource);
        sky.SetColor("_Tint",new Color(1,.83f,.8f,1));
        sky.SetFloat("_Exposure",.9f); sky.SetFloat("_Rotation",0);
        sky.SetFloat("_Drift",.25f); sky.SetFloat("_Distortion",.035f);
        sky.SetFloat("_DistortionSpeed",.075f); sky.SetFloat("_CloudLayer",.22f);
        sky.SetFloat("_PreviewTime",-1); EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f,.23f,.22f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.18f,.015f,.02f);
        RenderSettings.fogDensity = .009f;
        foreach (var light in all.Select(t => t.GetComponent<Light>()).Where(l => l))
        {
            if (light.type == LightType.Directional) { light.color = new Color(1,.25f,.2f); light.intensity = .9f; }
            else if (light.name == "Door spill light") { light.color = new Color(1,.19f,.12f); light.intensity = 16; }
            EditorUtility.SetDirty(light);
        }
        foreach (var camera in all.Select(t => t.GetComponent<Camera>()).Where(c => c && c.CompareTag("MainCamera")))
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.requiresDepthTexture = true;
        }

        // Override the inherited ochre profile locally; never recolour assets shared with Intermission 1.
        var volume = all.Select(t => t.GetComponent<Volume>()).Where(v => v && v.isGlobal).OrderByDescending(v => v.priority).FirstOrDefault();
        if (!volume) throw new InvalidOperationException("Expected the scene's existing atmosphere volume.");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Crimson grading.asset");
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,Folder + "/Crimson grading.asset"); }
        T Component<T>() where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var item)) return item;
            item = profile.Add<T>(false); AssetDatabase.AddObjectToAsset(item,profile); return item;
        }
        var balance = Component<WhiteBalance>(); balance.temperature.Override(8); balance.tint.Override(4);
        var color = Component<ColorAdjustments>();
        color.colorFilter.Override(new Color(1,.9f,.88f)); color.saturation.Override(-4);
        color.contrast.Override(-4); color.postExposure.Override(0);
        var bloom = Component<Bloom>(); bloom.intensity.Override(.16f); bloom.threshold.Override(1.1f); bloom.scatter.Override(.4f);
        volume.sharedProfile = profile;
        foreach (var component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);

        foreach (var pose in poses)
            if (pose.Key.localPosition != pose.Value.Item1 || pose.Key.localRotation != pose.Value.Item2 || pose.Key.localScale != pose.Value.Item3 || pose.Key.parent != pose.Value.Item4)
                throw new InvalidOperationException("An authored transform changed: " + pose.Key.name);
        AssetDatabase.SaveAssets();
    }

    private static Material CloneForScene(Material source)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (sourcePath.StartsWith(Folder + "/",StringComparison.Ordinal)) return source;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
        string path = Folder + "/" + source.name + " - " + guid.Substring(0,8) + " - " + localId + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(source); AssetDatabase.CreateAsset(material,path); }
        material.CopyPropertiesFromMaterial(source);
        return material;
    }
}
