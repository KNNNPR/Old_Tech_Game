using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class IntermissionMoodSetup
{
    public const string ScenePath = "Assets/Scenes/Intermission.unity";
    public const string Folder = "Assets/Materials/Intermission Mood";
    private const string Request = "Temp/IntermissionMood/apply-v1.request";
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
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("Temp/IntermissionMood");
            EditorSceneManager.SaveScene(scene,"Temp/IntermissionMood/Intermission-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity",true);
            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/IntermissionMood/applied.txt","PASS\nFirst Intermission saved with ochre courtyard smog, warm ambient/directional light, amber windows, slow storm sky and scene-local colour grading. Existing cinematic, wind, poster and PSX vertex twitch are preserved.\n");
            File.Delete(Request); SceneView.RepaintAll();
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { applying = false; }
    }

    public static void Configure(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Mood setup only supports the first Intermission.");
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (Directory.Exists(Folder)) AssetDatabase.ImportAsset(Folder,ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Materials","Intermission Mood");
        }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var buildings = all.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r && r.sharedMaterials.Any(IntermissionBuildingTwitchSetup.IsBuildingMaterial)).ToArray();
        if (buildings.Length < 18) throw new InvalidOperationException("Expected the apartment courtyard in Intermission.");
        Bounds city = buildings[0].bounds;
        foreach (var building in buildings.Skip(1)) city.Encapsulate(building.bounds);
        var warmMaterials = new Dictionary<Material,Material>();
        foreach (var renderer in buildings)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!source || source.shader.name != "Old Tech/PSX Building Twitch") continue;
                if (!warmMaterials.TryGetValue(source,out var material))
                {
                    if (AssetDatabase.GetAssetPath(source).StartsWith(Folder + "/",StringComparison.Ordinal)) material = source;
                    else
                    {
                        string path = Folder + "/Warm building " + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8) + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material) { material = new Material(source); AssetDatabase.CreateAsset(material,path); }
                        material.CopyPropertiesFromMaterial(source);
                    }
                    material.SetColor("_BaseColor",new Color(.97f,.94f,.87f,1));
                    if (material.IsKeywordEnabled("_EMISSION")) material.SetColor("_EmissionColor",new Color(2f,1.15f,.45f,1));
                    material.SetFloat("_PSXPreviewTime",-1);
                    EditorUtility.SetDirty(material);
                    warmMaterials.Add(source,material);
                }
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        var ledge = all.FirstOrDefault(t => t.name == "Plane.015")?.GetComponent<MeshRenderer>();
        if (ledge && ledge.sharedMaterial)
        {
            string path = Folder + "/Foreground concrete.mat";
            var concrete = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!concrete) { concrete = new Material(ledge.sharedMaterial); AssetDatabase.CreateAsset(concrete,path); }
            if (concrete.HasProperty("_BaseColor")) concrete.SetColor("_BaseColor",new Color(.73f,.7f,.61f,1));
            ledge.sharedMaterial = concrete; EditorUtility.SetDirty(concrete);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ledge);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.46f,.42f,.33f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.12f,.115f,.084f);
        RenderSettings.fogDensity = .0085f;
        foreach (var light in all.Select(t => t.GetComponent<Light>()).Where(l => l && l.type == LightType.Directional))
        {
            light.color = new Color(1,.94f,.82f); light.intensity = 1;
            EditorUtility.SetDirty(light);
        }
        var doorLight = all.FirstOrDefault(t => t.name == "Door spill light")?.GetComponent<Light>();
        if (doorLight) { doorLight.color = new Color(1,.7f,.35f); doorLight.intensity = 22; }

        var sky = MaterialAt(Folder + "/Ochre storm sky.mat","Old Tech/Ominous Blood Sky");
        sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture>("Assets/Textures/skybox.jpg"));
        sky.SetColor("_Tint",new Color(.4f,.36f,.27f));
        sky.SetFloat("_Exposure",.95f); sky.SetFloat("_Rotation",0);
        sky.SetFloat("_Drift",.12f); sky.SetFloat("_Distortion",.018f);
        sky.SetFloat("_DistortionSpeed",.045f); sky.SetFloat("_CloudLayer",.12f);
        sky.SetFloat("_PreviewTime",-1); EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        foreach (var camera in all.Select(t => t.GetComponent<Camera>()).Where(c => c && c.CompareTag("MainCamera")))
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.requiresDepthTexture = true;
        }

        // Replace overlapping copies with one scene-local low volume, clear around the player's ledge.
        foreach (var old in all.Where(t => t.name.StartsWith("Lower Tier Fog",StringComparison.OrdinalIgnoreCase)))
            if (old.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Intermission ochre atmosphere");
        if (!root) { root = new GameObject("Intermission ochre atmosphere"); SceneManager.MoveGameObjectToScene(root,scene); }
        var fogTransform = root.transform.Find("Courtyard smog");
        if (!fogTransform)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = "Courtyard smog";
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            fogTransform = box.transform; fogTransform.SetParent(root.transform,false);
        }
        const float fogTop = 6, fogBottom = -85;
        fogTransform.SetPositionAndRotation(new Vector3(city.center.x,(fogTop+fogBottom)*.5f,city.center.z),Quaternion.identity);
        fogTransform.localScale = new Vector3(city.size.x+50,fogTop-fogBottom,city.size.z+70);
        var fog = MaterialAt(Folder + "/Courtyard ochre smog.mat","Old Tech/Low Lying Fog");
        fog.SetColor("_FogColor",new Color(.23f,.215f,.165f,1));
        fog.SetFloat("_Density",.035f); fog.SetFloat("_TopFade",.55f); fog.SetFloat("_EdgeFade",.14f);
        EditorUtility.SetDirty(fog);
        var fogRenderer = fogTransform.GetComponent<MeshRenderer>(); fogRenderer.sharedMaterial = fog;
        fogRenderer.shadowCastingMode = ShadowCastingMode.Off; fogRenderer.receiveShadows = false;
        fogRenderer.lightProbeUsage = LightProbeUsage.Off; fogRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Ochre grading.asset");
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,Folder + "/Ochre grading.asset"); }
        T Component<T>() where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var item)) return item;
            item = profile.Add<T>(false); AssetDatabase.AddObjectToAsset(item,profile);
            return item;
        }
        var balance = Component<WhiteBalance>(); balance.temperature.Override(12); balance.tint.Override(-3);
        var color = Component<ColorAdjustments>();
        color.colorFilter.Override(new Color(1,.98f,.91f)); color.saturation.Override(-6); color.contrast.Override(-2); color.postExposure.Override(0);
        var bloom = Component<Bloom>(); bloom.intensity.Override(.2f); bloom.threshold.Override(1); bloom.scatter.Override(.42f);
        var volume = root.GetComponent<Volume>(); if (!volume) volume = root.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 40; volume.sharedProfile = profile;
        foreach (var component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);

        // The runtime HUD supplies prompts; this unconfigured edit-mode label shouldn't sit over the city.
        foreach (var text in all.Select(t => t.GetComponent<TMP_Text>()).Where(t => t && t.GetComponentInParent<InteractionHUD>() && string.Equals(t.text,"New Text",StringComparison.OrdinalIgnoreCase)))
            text.text = "";
        AssetDatabase.SaveAssets();
    }

    private static Material MaterialAt(string path,string shaderName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var shader = Shader.Find(shaderName);
            if (!shader) throw new InvalidOperationException("Missing atmosphere shader: " + shaderName);
            material = new Material(shader); AssetDatabase.CreateAsset(material,path);
        }
        return material;
    }
}
