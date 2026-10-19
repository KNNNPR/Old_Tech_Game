using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Uses the existing project dither pass and converts only remaining smooth opaque surfaces in the two final scenes.</summary>
public static class LateSceneRetroSetup
{
    private const string Request="Temp/Delirium/retro-both-v1.request";
    private const string Folder="Assets/Materials/LateSceneRetro";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update+=()=>
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;
            if(File.Exists(Request))Apply();
        };
    }
    [MenuItem("Tools/Punch Card/Restore DELIRIUM and THE_END Retro Look")]
    public static void Apply()
    {
        if(applying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        applying=true;var active=SceneManager.GetActiveScene();
        try
        {
            Directory.CreateDirectory("Temp/Delirium");
            foreach(string path in new[]{DeliriumSetup.ScenePath,"Assets/Scenes/THE_END.unity"})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                try
                {
                    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    EditorSceneManager.SaveScene(scene,"Temp/Delirium/"+Path.GetFileNameWithoutExtension(path)+"-retro-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText("Temp/Delirium/retro-both-applied.txt","PASS\nDELIRIUM and THE_END cameras explicitly use the existing shared Retro Dither + Palette renderer, postprocessing enabled and antialiasing off. Remaining smooth opaque materials converted to scene-specific cel copies. DELIRIUM road uses cel diffuse plus wet specular; THE_END blood sea uses cel light bands while retaining waves, flow, sky and monitor reflections. Original authored transforms and other scenes preserved.\n");
            if(File.Exists(Request))File.Delete(Request);SceneView.RepaintAll();
        }
        catch(Exception e){Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static void Configure(Scene scene)
    {
        if(scene.path!=DeliriumSetup.ScenePath&&scene.path!="Assets/Scenes/THE_END.unity")throw new InvalidOperationException("Only the two final scenes are supported.");
        foreach(string path in new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset"})
        {
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if(!renderer||renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().Count(f=>f.name=="Retro Dither + Palette"&&f.isActive&&f.passMaterial&&f.passMaterial.GetFloat("_Intensity")>.99f)!=1)
                throw new InvalidOperationException("The shared project dither pass is missing or disabled: "+path);
        }
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach(var camera in all.Select(t=>t.GetComponent<Camera>()).Where(c=>c))
        {
            var data=camera.GetUniversalAdditionalCameraData();data.SetRenderer(0);data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.None;
            camera.allowMSAA=false;PrefabUtility.RecordPrefabInstancePropertyModifications(data);PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }
        foreach(var renderer in all.Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r))
        {
            var materials=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(!source)continue;
                if(source.shader.name=="Old Tech/Blood Sea")
                {source.SetFloat("_CelShadeSteps",4);EditorUtility.SetDirty(source);continue;}
                if(!new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Simple Lit","Standard"}.Contains(source.shader.name))continue;
                if(source.HasProperty("_Surface")&&source.GetFloat("_Surface")>.5f)continue;
                bool road=AssetDatabase.GetAssetPath(source)==DeliriumSetup.Folder+"/Glossy punchcard road.mat";
                if(road)
                {
                    source.shader=Shader.Find("Old Tech/Retro Cel Glossy");SetCel(source);EditorUtility.SetDirty(source);continue;
                }
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id);
                string path=Folder+"/"+guid+"-"+id+".mat";
                var cel=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!cel)
                {
                    if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Materials","LateSceneRetro");
                    cel=new Material(Shader.Find("Old Tech/Retro Cel Lit"));cel.CopyPropertiesFromMaterial(source);SetCel(cel);
                    string tex=source.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                    if(source.HasProperty(tex)){cel.SetTexture("_BaseMap",source.GetTexture(tex));cel.SetTextureScale("_BaseMap",source.GetTextureScale(tex));cel.SetTextureOffset("_BaseMap",source.GetTextureOffset(tex));}
                    if(!source.HasProperty("_BaseColor")&&source.HasProperty("_Color"))cel.SetColor("_BaseColor",source.GetColor("_Color"));
                    AssetDatabase.CreateAsset(cel,path);
                }
                materials[i]=cel;changed=true;
            }
            if(changed){renderer.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        }
        AssetDatabase.SaveAssets();
        if(scene.path=="Assets/Scenes/THE_END.unity")
        {
            // Reacquire after imports / material-layout migration so the new shader property is persisted on the asset.
            var water=AssetDatabase.LoadAssetAtPath<Material>(EndAtmosphereSetup.SeaMaterialPath);
            if(!water)throw new InvalidOperationException("THE_END blood sea material is missing.");
            water.SetFloat("_CelShadeSteps",4);EditorUtility.SetDirty(water);AssetDatabase.SaveAssetIfDirty(water);
            if(water.GetFloat("_CelShadeSteps")!=4)throw new InvalidOperationException("Blood sea cel settings did not persist.");
        }
    }
    private static void SetCel(Material material)
    {
        material.SetFloat("_ShadeSteps",4);material.SetFloat("_BandDither",.32f);material.SetFloat("_AmbientStrength",.28f);
        material.SetFloat("_ShadowFloor",.035f);material.SetColor("_ShadowTint",new Color(.47f,.32f,.34f));material.enableInstancing=true;
    }
}
