using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SceneAtmosphereSetup
{
    public const string ScenePath = "Assets/Scenes/scene_1.unity";
    public const string SkyPath = "Assets/Materials/Cloudy Skybox.mat";
    public const string FogPath = "Assets/Materials/Lower Tier Fog.mat";
    private const string Folder = "Temp/Atmosphere";

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.playModeStateChanged += state =>
        {
            if(state==PlayModeStateChange.EnteredEditMode) ApplyRequested();
        };
        EditorSceneManager.sceneOpened += (_,__) => ApplyRequested();
    }

    private static void ApplyRequested()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded) return;
        if(File.Exists(Folder+"/inspect.request")) { Inspect(); File.Delete(Folder+"/inspect.request"); }
        if(!File.Exists(Folder+"/apply.request")) return;
        try { Apply(); File.Delete(Folder+"/apply.request"); }
        catch(Exception e) { File.WriteAllText(Folder+"/error.txt",e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Old Tech/Atmosphere/Apply Skybox and Lower Fog")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory(Folder);
        EditorSceneManager.SaveScene(scene,Folder+"/scene_1-before-atmosphere-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Folder+"/applied.txt","Cloudy skybox and local low fog applied to scene_1.\n");
        SceneView.RepaintAll();
    }

    public static MeshRenderer Configure(Scene scene)
    {
        var targets=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r=>r.name=="Cube.049" || r.name=="Cube.051" || r.name=="Cube.053").ToArray();
        if(targets.Length!=3) throw new InvalidOperationException("Expected Cube.049, Cube.051 and Cube.053 in scene_1.");
        var texture=AssetDatabase.LoadAssetAtPath<Texture>("Assets/Textures/skybox.jpg");
        if(!texture) throw new InvalidOperationException("skybox.jpg is missing.");
        var importer=AssetImporter.GetAtPath("Assets/Textures/skybox.jpg") as TextureImporter;
        if(importer!=null && importer.wrapModeV!=TextureWrapMode.Clamp)
        {
            importer.wrapModeV=TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        var sky=MaterialAt(SkyPath,"Skybox/Panoramic");
        sky.SetTexture("_MainTex",texture);
        sky.SetColor("_Tint",new Color(.5f,.5f,.5f,1));
        sky.SetFloat("_Exposure",1);sky.SetFloat("_Rotation",0);
        sky.SetFloat("_Mapping",1);sky.SetFloat("_ImageType",0);sky.SetFloat("_Layout",0);
        RenderSettings.skybox=sky;
        foreach(var camera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))
            if(camera.CompareTag("MainCamera")) camera.clearFlags=CameraClearFlags.Skybox;

        Bounds bounds=targets[0].bounds;
        foreach(var target in targets.Skip(1)) bounds.Encapsulate(target.bounds);
        float top=targets.Min(r=>r.bounds.max.y)+4f;
        float bottom=bounds.min.y-45f;
        float height=top-bottom;
        var material=MaterialAt(FogPath,"Old Tech/Low Lying Fog");
        material.SetColor("_FogColor",new Color(.105f,.112f,.12f,1));
        material.SetFloat("_Density",.48f);material.SetFloat("_TopFade",12f/height);material.SetFloat("_EdgeFade",.08f);
        var fog=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Lower Tier Fog");
        if(!fog)
        {
            fog=GameObject.CreatePrimitive(PrimitiveType.Cube);fog.name="Lower Tier Fog";
            UnityEngine.Object.DestroyImmediate(fog.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(fog,scene);
        }
        fog.transform.SetPositionAndRotation(new Vector3(bounds.center.x,(top+bottom)*.5f,bounds.center.z),Quaternion.identity);
        fog.transform.localScale=new Vector3(bounds.size.x+400f,height,bounds.size.z+400f);
        var renderer=fog.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        EditorUtility.SetDirty(sky);EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(sky);AssetDatabase.SaveAssetIfDirty(material);
        return renderer;
    }

    private static Material MaterialAt(string path,string shaderName)
    {
        var shader=Shader.Find(shaderName);
        if(!shader) throw new InvalidOperationException("Shader missing: "+shaderName);
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
        else if(material.shader!=shader) material.shader=shader;
        return material;
    }

    [MenuItem("Tools/Old Tech/Atmosphere/Inspect Scene")]
    public static void Inspect()
    {
        Directory.CreateDirectory(Folder);
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        var report=new StringBuilder();
        report.AppendLine("Scene: "+scene.path);
        foreach(var renderer in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))
            report.AppendLine($"{renderer.name} | centre {renderer.bounds.center:F5} | min {renderer.bounds.min:F5} | max {renderer.bounds.max:F5} | materials {string.Join(",",renderer.sharedMaterials.Select(m=>m?m.name:"null"))}");
        foreach(var camera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))
            report.AppendLine($"CAMERA {camera.name} | pos {camera.transform.position:F5} | rot {camera.transform.eulerAngles:F5}");
        if (SceneView.lastActiveSceneView)
        {
            var camera=SceneView.lastActiveSceneView.camera;
            report.AppendLine($"SCENEVIEW | pos {camera.transform.position:F5} | rot {camera.transform.eulerAngles:F5}");
        }
        File.WriteAllText(Folder+"/source.txt",report.ToString());
    }
}
