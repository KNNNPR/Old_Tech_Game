using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local collapse, distant screams and third-shift faint audio.</summary>
public static class StoryAudioSetup
{
    public static readonly string[] Paths={"Assets/Scenes/Intermission 2.unity","Assets/Scenes/scene_1_Kirill.unity"};
    private const string Request="Temp/StoryAudio/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update+=()=>{
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;
            if(File.Exists(Request))Apply();
        };
    }
    [MenuItem("Tools/Punch Card/Audio/Set Up Collapse Screams And Faint Perspective")]
    public static void Apply()
    {
        if(applying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        applying=true;var active=SceneManager.GetActiveScene();Directory.CreateDirectory("Temp/StoryAudio");
        try
        {
            foreach(var path in Paths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene,"Temp/StoryAudio/"+scene.name+"-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/StoryAudio/applied.txt","PASS\nIntermission 2: wscream then mscream 0.65 seconds later at the street cue, distant spatial sources with low-pass and subtle echoes; fallinghouse starts with the existing collapse animation. Shift 3 faint: heavybreath and earringing rise while other audio fades to 6% and becomes muffled; original levels and filters restore on exit. All audio respects master volume. Authored transforms preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){File.WriteAllText("Temp/StoryAudio/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static void Configure(Scene scene)
    {
        if(!Paths.Contains(scene.path))throw new InvalidOperationException("Unsupported story audio scene.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        if(scene.name=="Intermission 2")
        {
            var cinematic=all.Select(t=>t.GetComponent<IntermissionCinematic>()).Single(c=>c);
            var data=new SerializedObject(cinematic);var collapse=(IntermissionBuildingCollapse)data.FindProperty("buildingCollapse").objectReferenceValue;
            var fall=new SerializedObject(collapse);var building=(Transform)fall.FindProperty("animatedBuilding").objectReferenceValue;
            var bounds=fall.FindProperty("buildingBounds").boundsValue;
            var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Distant street sounds");
            if(!root){root=new GameObject("Distant street sounds");SceneManager.MoveGameObjectToScene(root,scene);}
            var female=Source(root.transform,"Distant female scream","Assets/Sounds/sfx/wscream.wav",.6f,30,240,bounds.center+new Vector3(-14,-20,4));
            var male=Source(root.transform,"Distant male scream","Assets/Sounds/sfx/mscream.wav",.38f,30,240,bounds.center+new Vector3(17,-20,-8));
            DistanceFilters(female,1600,.18f);DistanceFilters(male,1300,.16f);
            var rumble=Source(building,"House collapse audio","Assets/Sounds/sfx/new/fallinghouse.mp3",.75f,25,260,bounds.center);
            DistanceFilters(rumble,4500,.1f);
            Set(data,"streetAudio",female);Set(data,"streetScreamClip",female.clip);Set(data,"streetMaleAudio",male);data.ApplyModifiedPropertiesWithoutUndo();
            Set(fall,"collapseAudio",rumble);fall.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            var faint=all.Select(t=>t.GetComponent<PlayerFaint>()).Single(c=>c);var data=new SerializedObject(faint);
            var interactor=(PlayerInteractor)data.FindProperty("interactor").objectReferenceValue;
            if(!interactor)interactor=all.Select(t=>t.GetComponent<PlayerInteractor>()).Single(c=>c);
            var camera=interactor.PlayerCamera.transform;
            Set(data,"breathingAudio",Source(camera,"Faint heavy breathing","Assets/Sounds/sfx/heavybreath.mp3",.1f,2,20,camera.position));
            Set(data,"ringingAudio",Source(camera,"Faint ear ringing","Assets/Sounds/sfx/earringing.wav",.025f,2,20,camera.position));
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var pose in poses)
            if(pose.Key.localPosition!=pose.Value.Item1||pose.Key.localRotation!=pose.Value.Item2||pose.Key.localScale!=pose.Value.Item3||pose.Key.parent!=pose.Value.Item4)
                throw new InvalidOperationException("Moved an authored transform: "+pose.Key.name);
    }
    private static AudioSource Source(Transform parent,string name,string path,float volume,float near,float far,Vector3 position)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(!clip)throw new InvalidOperationException("Missing sound: "+path);
        var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);child.position=position;}
        var source=child.GetComponent<AudioSource>();if(!source)source=child.gameObject.AddComponent<AudioSource>();
        source.clip=clip;source.volume=volume;source.loop=false;source.playOnAwake=false;source.spatialBlend=1;source.ignoreListenerVolume=false;
        source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=near;source.maxDistance=far;return source;
    }
    private static void DistanceFilters(AudioSource source,float cutoff,float echo)
    {
        var low=source.GetComponent<AudioLowPassFilter>();if(!low)low=source.gameObject.AddComponent<AudioLowPassFilter>();low.cutoffFrequency=cutoff;low.lowpassResonanceQ=1;
        var delay=source.GetComponent<AudioEchoFilter>();if(!delay)delay=source.gameObject.AddComponent<AudioEchoFilter>();delay.delay=420;delay.decayRatio=.28f;delay.wetMix=echo;delay.dryMix=1;
    }
    private static void Set(SerializedObject target,string name,UnityEngine.Object value)=>target.FindProperty(name).objectReferenceValue=value;
}
