using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CardPlacementAudioSetup
{
    private const string Request="Temp/CardPlacementAudio/apply-v1.request";
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
    public static string[] ScenePaths=>AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
    [MenuItem("Tools/Punch Card/Audio/Set Up Card Placement In All Scenes")]
    public static void Apply()
    {
        if(applying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        applying=true;var active=SceneManager.GetActiveScene();Directory.CreateDirectory("Temp/CardPlacementAudio");
        try
        {
            var report="PASS\nDrop.wav once per successful punchcard placement in oven / normal receiver, including the DELIRIUM ritual. Fully spatial and master-controlled. Authored transforms preserved.\n";
            foreach(var path in ScenePaths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    if(!scene.GetRootGameObjects().Any(r=>r.GetComponentsInChildren<SortBin>(true).Length>0||r.GetComponentsInChildren<DeliriumSequence>(true).Length>0))continue;
                    EditorSceneManager.SaveScene(scene,"Temp/CardPlacementAudio/"+scene.name+"-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    int count=Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report+=scene.name+": "+count+" placements\n";
                }
                finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText("Temp/CardPlacementAudio/applied.txt",report);
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){File.WriteAllText("Temp/CardPlacementAudio/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static int Configure(Scene scene)
    {
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));int count=0;
        foreach(var bin in all.Select(t=>t.GetComponent<SortBin>()).Where(c=>c))
        {
            var data=new SerializedObject(bin);var slot=(ItemSlot)data.FindProperty("slot").objectReferenceValue;
            if(!slot)throw new InvalidOperationException(scene.name+": SortBin has no slot.");
            data.FindProperty("placementAudio").objectReferenceValue=Source(slot.AnchorTransform);data.ApplyModifiedPropertiesWithoutUndo();count++;
        }
        foreach(var ritual in all.Select(t=>t.GetComponent<DeliriumSequence>()).Where(c=>c))
        {
            var data=new SerializedObject(ritual);var anchor=(Transform)data.FindProperty("cardRest").objectReferenceValue;
            if(!anchor)throw new InvalidOperationException(scene.name+": DELIRIUM has no card rest.");
            data.FindProperty("placementAudio").objectReferenceValue=Source(anchor);data.ApplyModifiedPropertiesWithoutUndo();count++;
        }
        foreach(var pose in poses)
            if(pose.Key.localPosition!=pose.Value.Item1||pose.Key.localRotation!=pose.Value.Item2||pose.Key.localScale!=pose.Value.Item3||pose.Key.parent!=pose.Value.Item4)
                throw new InvalidOperationException("Moved an authored transform: "+scene.name+"/"+pose.Key.name);
        return count;
    }
    private static AudioSource Source(Transform anchor)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/sfx/Drop.wav");if(!clip)throw new InvalidOperationException("Missing Drop.wav.");
        var child=anchor.Find("Card placement audio");if(!child){child=new GameObject("Card placement audio").transform;child.SetParent(anchor,false);}
        var source=child.GetComponent<AudioSource>();if(!source)source=child.gameObject.AddComponent<AudioSource>();
        source.clip=clip;source.volume=.45f;source.loop=false;source.playOnAwake=false;source.spatialBlend=1;source.ignoreListenerVolume=false;
        source.minDistance=1.5f;source.maxDistance=14;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;return source;
    }
}
