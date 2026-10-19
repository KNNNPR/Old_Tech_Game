using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SecondIntermissionSetup
{
    private const string Request="Temp/SecondIntermission/apply-v1.request";
    private const string Folder="Assets/Materials/Intermission 2 Collapse";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.update+=()=>
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;
            nextCheck=EditorApplication.timeSinceStartup+1;
            if(File.Exists(Request))ApplyRequested();
        };
    }
    [MenuItem("Tools/Punch Card/Set Up Second Intermission Collapse Cinematic")]
    public static void ApplyRequested()
    {
        if(applying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string path=IntermissionRedMoodSetup.ScenePath;
        if(string.IsNullOrEmpty(path))return;
        var scene=SceneManager.GetSceneByPath(path);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        var active=SceneManager.GetActiveScene();
        applying=true;
        try
        {
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            Directory.CreateDirectory("Temp/SecondIntermission");
            EditorSceneManager.SaveScene(scene,"Temp/SecondIntermission/Intermission-2-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/SecondIntermission/applied.txt","PASS\nSecond Intermission saved: nine Operator C lines, red sky word, smoking, optional street screams, whole Cube.049 column collapse with dust/debris, close tracking, dropped cigarette, recoil, panic run and fade. Authored starting transforms and first Intermission preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){Debug.LogException(e);}
        finally
        {
            if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            applying=false;
        }
    }
    public static IntermissionCinematic Configure(Scene scene)
    {
        if(scene.path!=IntermissionRedMoodSetup.ScenePath)throw new InvalidOperationException("Only second Intermission may be configured.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var original=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        var director=all.Select(t=>t.GetComponent<IntermissionCinematic>()).Single(c=>c);
        var data=new SerializedObject(director);
        var opening=(Transform)data.FindProperty("openingPose").objectReferenceValue;
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Scenes/house_fall.anim");
        if(!clip)throw new InvalidOperationException("The authored house_fall animation is missing.");
        var animator=all.Select(t=>t.GetComponent<Animator>()).Single(a=>a&&a.runtimeAnimatorController&&a.runtimeAnimatorController.animationClips.Contains(clip));
        var target=animator.transform;
        var parts=target.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.transform).OrderBy(Depth).ToArray();
        if(parts.Length<3)throw new InvalidOperationException("Could not identify the full Cube.049 building column.");
        var bounds=parts[0].GetComponent<MeshRenderer>().bounds;
        foreach(var part in parts.Skip(1))bounds.Encapsulate(part.GetComponent<MeshRenderer>().bounds);
        var collapse=director.GetComponent<IntermissionBuildingCollapse>();
        if(!collapse)collapse=director.gameObject.AddComponent<IntermissionBuildingCollapse>();
        if(!AssetDatabase.IsValidFolder(Folder))
        {
            if(Directory.Exists(Folder))AssetDatabase.ImportAsset(Folder,ImportAssetOptions.ForceSynchronousImport);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Materials","Intermission 2 Collapse");
        }
        var smoke=Dust(director.transform,"Collapse dust",Material("Dust",2),false);
        var debris=Dust(director.transform,"Collapse debris",Material("Debris",0),true);
        smoke.transform.position=debris.transform.position=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        var settings=new SerializedObject(collapse);
        var array=settings.FindProperty("buildingParts");array.arraySize=parts.Length;
        for(int i=0;i<parts.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=parts[i];
        settings.FindProperty("buildingBounds").boundsValue=bounds;
        settings.FindProperty("animatedBuilding").objectReferenceValue=target;
        settings.FindProperty("fallAnimation").objectReferenceValue=clip;
        settings.FindProperty("buildingAnimator").objectReferenceValue=animator;
        settings.FindProperty("dustCloud").objectReferenceValue=smoke;
        settings.FindProperty("debris").objectReferenceValue=debris;
        settings.ApplyModifiedPropertiesWithoutUndo();
        // Its controller enters house_fall immediately; the cinematic now owns the exact start time.
        animator.enabled=false;EditorUtility.SetDirty(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        var audioTransform=director.transform.Find("Street screams cue");
        if(!audioTransform){audioTransform=new GameObject("Street screams cue",typeof(AudioSource)).transform;audioTransform.SetParent(director.transform,false);}
        audioTransform.position=bounds.center+Vector3.up*bounds.size.y*.35f;
        var audio=audioTransform.GetComponent<AudioSource>();audio.playOnAwake=false;audio.loop=false;
        audio.spatialBlend=.65f;audio.minDistance=10;audio.maxDistance=160;audio.volume=.7f;
        data.FindProperty("secondIntermission").boolValue=true;
        data.FindProperty("buildingCollapse").objectReferenceValue=collapse;
        data.FindProperty("streetAudio").objectReferenceValue=audio;
        data.FindProperty("runSpeed").floatValue=2.8f;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach(var pose in original)
            if(pose.Key.localPosition!=pose.Value.Item1||pose.Key.localRotation!=pose.Value.Item2||pose.Key.localScale!=pose.Value.Item3||pose.Key.parent!=pose.Value.Item4)
                throw new InvalidOperationException("Existing transform changed during setup: "+pose.Key.name);
        AssetDatabase.SaveAssets();
        return director;
    }
    private static int Depth(Transform t){int count=0;for(var p=t.parent;p;p=p.parent)count++;return count;}
    private static Material Material(string name,float kind)
    {
        string path=Folder+"/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Old Tech/PSX Wind"));AssetDatabase.CreateAsset(material,path);}
        material.SetFloat("_Kind",kind);EditorUtility.SetDirty(material);return material;
    }
    private static ParticleSystem Dust(Transform parent,string name,Material material,bool debris)
    {
        var child=parent.Find(name);
        if(!child){child=new GameObject(name,typeof(ParticleSystem)).transform;child.SetParent(parent,false);}
        var particles=child.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed=false;particles.randomSeed=debris?317u:952u;
        var main=particles.main;main.loop=false;main.playOnAwake=false;main.duration=2;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;
        main.startLifetime=new ParticleSystem.MinMaxCurve(debris?2:7,debris?4:13);
        main.startSize=new ParticleSystem.MinMaxCurve(debris?.18f:3,debris?.65f:7);
        main.startSpeed=0;main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startColor=debris?new Color(.15f,.12f,.11f,1):new Color(.43f,.35f,.32f,.7f);
        main.gravityModifier=debris?.75f:0;main.maxParticles=debris?150:1000;
        var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
        var size=particles.sizeOverLifetime;size.enabled=!debris;
        size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.55f),new Keyframe(.35f,1),new Keyframe(1,1.8f)));
        var color=particles.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.6f,.58f,.56f),1)},
            new[]{new GradientAlphaKey(debris?1:.5f,0),new GradientAlphaKey(debris?1:.8f,.15f),new GradientAlphaKey(0,1)});
        color.color=gradient;
        var noise=particles.noise;noise.enabled=!debris;noise.strength=.45f;noise.frequency=.3f;noise.scrollSpeed=.25f;noise.quality=ParticleSystemNoiseQuality.Low;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortMode=ParticleSystemSortMode.Distance;
        return particles;
    }
}
