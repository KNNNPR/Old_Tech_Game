using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShiftAudioPolishSetup
{
    public static readonly string[] Paths={"Assets/Scenes/scene_1_Kirill.unity","Assets/Scenes/scene_1.unity","Assets/Scenes/TUTORIAL.unity","Assets/Scenes/HELPYDIALOGUE.unity","Assets/Scenes/Intermission.unity","Assets/Scenes/Intermission 2.unity","Assets/Scenes/DELIRIUM.unity"};
    private const string Request="Temp/ShiftAudioPolish/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;if(File.Exists(Request))Apply();};
    }
    [MenuItem("Tools/Punch Card/Audio/Polish Shift And Story Sounds")]
    public static void Apply()
    {
        if(applying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        applying=true;var active=SceneManager.GetActiveScene();Directory.CreateDirectory("Temp/ShiftAudioPolish");
        try
        {
            PrepareVoices();
            foreach(var path in Paths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene,"Temp/ShiftAudioPolish/"+scene.name+"-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/ShiftAudioPolish/applied.txt","PASS\nAll main shifts / tutorial: looping scene-wide 2D ambient. All dialogue hosts: preloaded PCM robot / human samples, correct visible speaker position, reliable spatial radius. Printer printing loop only during a real print, camera_lookup on successful extract / exposure / feed / eject. Oven bite and burning cues spatial / synchronized; ritual metal bites preserved. Fatal door animation unchanged: each knock uses only the first 0.25s of the multi-hit sample at reduced gain; audible autodoor. Intermission 2: quiet istolethatsample at screams rises through collapse, stronger stationary camera tension, fades on black. Both incoming Intermissions: audio fade / black cover followed by descending large-square reveal. Master volume and authored transforms preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){File.WriteAllText("Temp/ShiftAudioPolish/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static void PrepareVoices()
    {
        foreach(string name in new[]{"robovoice.mp3","humanvoice.mp3"})
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath("Assets/Sounds/sfx/"+name);if(!importer)throw new InvalidOperationException("Missing voice: "+name);
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;
            bool changed=importer.defaultSampleSettings.loadType!=settings.loadType||importer.defaultSampleSettings.compressionFormat!=settings.compressionFormat||!importer.defaultSampleSettings.preloadAudioData;
            if(changed){importer.defaultSampleSettings=settings;importer.SaveAndReimport();}
        }
    }
    public static void Configure(Scene scene)
    {
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        if(scene.name=="scene_1_Kirill"||scene.name=="scene_1"||scene.name=="TUTORIAL")
        {
            var room=all.Select(t=>t.GetComponent<GameplaySceneAudio>()).SingleOrDefault(c=>c);
            if(!room){var root=new GameObject("Room audio");SceneManager.MoveGameObjectToScene(root,scene);room=root.AddComponent<GameplaySceneAudio>();}
            var data=new SerializedObject(room);var ambient=(AudioSource)data.FindProperty("ambient").objectReferenceValue;
            if(!ambient)ambient=Source(room.transform,"Room ambient audio","ambient.mp3",.5f);
            ambient.clip=Clip("ambient.mp3");ambient.spatialBlend=0;ambient.volume=.5f;ambient.loop=true;ambient.playOnAwake=false;ambient.ignoreListenerVolume=false;
            Set(data,"ambient",ambient);data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var t in all)
        {
            var intro=t.GetComponent<HelpyTutorialIntro>();var returned=t.GetComponent<HelpyReturnDialogue>();var monologue=t.GetComponent<IntermissionCinematic>();
            if(!intro&&!returned&&!monologue)continue;
            var owner=new SerializedObject(intro?(UnityEngine.Object)intro:returned?(UnityEngine.Object)returned:monologue);
            var view=(Camera)owner.FindProperty("playerCamera").objectReferenceValue;var robot=monologue?null:(Transform)owner.FindProperty("robot").objectReferenceValue;
            if(robot){var face=robot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Cube.001");if(face)robot=face.transform;}
            var voice=t.GetComponent<DialogueVoice>();if(!voice)voice=t.gameObject.AddComponent<DialogueVoice>();voice.enabled=true;
            var voiceData=new SerializedObject(voice);var source=Source(t,"Dialogue voice audio","humanvoice.mp3",.38f);source.minDistance=6;source.maxDistance=35;
            Set(voiceData,"source",source);Set(voiceData,"robotVoice",Clip("robovoice.mp3"));Set(voiceData,"humanVoice",Clip("humanvoice.mp3"));Set(voiceData,"robotSpeaker",robot);Set(voiceData,"operatorSpeaker",view.transform);voiceData.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var printer in all.Select(t=>t.GetComponent<PhotoPrinterSpawner>()).Where(c=>c))
        {
            var data=new SerializedObject(printer);var outlet=(Transform)data.FindProperty("outputPose").objectReferenceValue;
            Set(data,"printingAudio",Source(outlet?outlet:printer.transform,"Photo printing audio","printing.wav",.38f));data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var monitor in all.Select(t=>t.GetComponent<MonitorDisplay>()).Where(c=>c))
        {
            var data=new SerializedObject(monitor);Set(data,"extractionAudio",Source(monitor.transform,"Extract camera sound","camera_lookup.mp3",.35f));
            var legacy=(ItemDispenser)data.FindProperty("printer").objectReferenceValue;data.ApplyModifiedPropertiesWithoutUndo();
            if(legacy){var legacyData=new SerializedObject(legacy);Set(legacyData,"audioSource",Source(legacy.transform,"Printer work audio","printing.wav",.38f));Set(legacyData,"workClip",Clip("printing.wav"));legacyData.ApplyModifiedPropertiesWithoutUndo();}
        }
        foreach(var feed in all.Select(t=>t.GetComponent<FeedLever>()).Where(c=>c))
        {
            var data=new SerializedObject(feed);var slot=(ItemSlot)data.FindProperty("slot").objectReferenceValue;
            Set(data,"transportAudio",Source(slot?slot.AnchorTransform:feed.transform,"Card transport camera sound","camera_lookup.mp3",.35f));data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var station in all.Select(t=>t.GetComponent<PhotoExposureStation>()).Where(c=>c))
        {var data=new SerializedObject(station);Set(data,"handlingAudio",Source(station.transform,"Photo exposure camera sound","camera_lookup.mp3",.3f));data.ApplyModifiedPropertiesWithoutUndo();}
        foreach(var oven in all.Select(t=>t.GetComponent<OvenIncineration>()).Where(c=>c))
        {
            var data=new SerializedObject(oven);var flames=(ParticleSystem)data.FindProperty("flames").objectReferenceValue;
            var anchor=flames?flames.transform:oven.transform;
            Set(data,"fireAudio",Source(anchor,"Oven burning audio","burning.wav",.4f));Set(data,"biteAudio",Source(anchor,"Oven metal bite audio","metal_bite.mp3",.55f));
            data.FindProperty("fireFadeSeconds").floatValue=1;data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var bite in all.Select(t=>t.GetComponent<DeliriumOvenBite>()).Where(c=>c))
        {var data=new SerializedObject(bite);Set(data,"metalAudio",Source(bite.transform,"Ritual metal bite","metal_bite.mp3",.55f));data.ApplyModifiedPropertiesWithoutUndo();}
        foreach(var threat in all.Select(t=>t.GetComponent<SortingFailureCinematic>()).Where(c=>c))
        {
            var data=new SerializedObject(threat);var aim=(Transform)data.FindProperty("doorAim").objectReferenceValue;
            var knock=(AudioSource)data.FindProperty("knockAudio").objectReferenceValue;if(!knock)throw new InvalidOperationException("Missing threat knock source.");
            knock.volume=.25f;knock.minDistance=8;knock.maxDistance=40;data.FindProperty("knockSoundSeconds").floatValue=.25f;
            var opening=Source(aim,"Balcony opening cue","autodoor.mp3",.6f);opening.minDistance=8;opening.maxDistance=45;Set(data,"openingAudio",opening);data.ApplyModifiedPropertiesWithoutUndo();
        }
        if(scene.name=="Intermission 2")
        {
            var cinematic=all.Select(t=>t.GetComponent<IntermissionCinematic>()).Single(c=>c);var data=new SerializedObject(cinematic);var view=(Camera)data.FindProperty("playerCamera").objectReferenceValue;
            var music=Source(view.transform,"House fall tension music","../music/istolethatsample.mp3",.055f);music.loop=true;Set(data,"collapseMusic",music);data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var pose in poses)
            if(pose.Key.localPosition!=pose.Value.Item1||pose.Key.localRotation!=pose.Value.Item2||pose.Key.localScale!=pose.Value.Item3||pose.Key.parent!=pose.Value.Item4)
                throw new InvalidOperationException("Moved an authored transform: "+scene.name+"/"+pose.Key.name);
    }
    private static AudioClip Clip(string name)
    {
        string path=name.StartsWith("../music/")?"Assets/Sounds/music/"+name.Substring(9):"Assets/Sounds/sfx/"+name;
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(!clip)throw new InvalidOperationException("Missing sound: "+path);return clip;
    }
    private static AudioSource Source(Transform parent,string name,string clip,float volume)
    {
        if(!parent)throw new InvalidOperationException("Missing sound anchor for "+name);
        var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);if(parent.TryGetComponent<Renderer>(out var renderer))child.position=renderer.bounds.center;}
        var source=child.GetComponent<AudioSource>();if(!source)source=child.gameObject.AddComponent<AudioSource>();
        source.clip=Clip(clip);source.volume=volume;source.loop=false;source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=25;source.dopplerLevel=0;source.ignoreListenerVolume=false;source.rolloffMode=AudioRolloffMode.Linear;return source;
    }
    private static void Set(SerializedObject data,string name,UnityEngine.Object value)=>data.FindProperty(name).objectReferenceValue=value;
}
