using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuAudioSetup
{
    public const string ScenePath="Assets/Scenes/MENU.unity";
    private const string Request="Temp/MenuAudio/apply-v1.request";
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
    [MenuItem("Tools/Punch Card/MENU/Set Up Menu Audio")]
    public static void Apply()
    {
        if(applying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        applying=true;var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        try
        {
            if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            Directory.CreateDirectory("Temp/MenuAudio");
            EditorSceneManager.SaveScene(scene,"Temp/MenuAudio/Menu-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/MenuAudio/applied.txt","PASS\nMENU saved with logo and button hover, looping maintheme, button / return / camera sounds, half-volume lever and turnscreenon limited to one second on power switching. All five 2D AudioSources respect master volume. Existing authored transforms preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){Debug.LogException(e);}
        finally
        {
            if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;
        }
    }
    public static MenuAudio Configure(Scene scene)
    {
        if(scene.path!=ScenePath)throw new InvalidOperationException("Menu audio setup only supports MENU.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        var menu=all.Select(t=>t.GetComponent<MainMenuController>()).Single(c=>c);
        var computer=all.Select(t=>t.GetComponent<ComputerMenuController>()).Single(c=>c);
        var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Menu audio");
        if(!root){root=new GameObject("Menu audio");SceneManager.MoveGameObjectToScene(root,scene);}
        var audio=root.GetComponent<MenuAudio>();if(!audio)audio=root.AddComponent<MenuAudio>();
        AudioSource Source(string name,float volume,int priority)
        {
            var child=root.transform.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(root.transform,false);}
            var source=child.GetComponent<AudioSource>();if(!source)source=child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.volume=volume;source.priority=priority;
            source.ignoreListenerVolume=false;source.ignoreListenerPause=false;source.dopplerLevel=0;return source;
        }
        var data=new SerializedObject(audio);
        void Ref(string field,UnityEngine.Object value)=>data.FindProperty(field).objectReferenceValue=value;
        var music=Source("MENU music",.4f,128);music.loop=true;
        Ref("music",music);Ref("interfaceSound",Source("Computer button sounds",.7f,64));Ref("cameraSound",Source("Camera approach sound",.65f,96));
        Ref("hoverSound",Source("Button hover sound",.4f,112));
        Ref("powerSound",Source("Screen power sound",.45f,96));
        foreach(var clip in new[]{("mainTheme","Assets/Sounds/music/maintheme.mp3"),("press","Assets/Sounds/sfx/press_ingame.mp3"),("back","Assets/Sounds/sfx/esc.mp3"),("cameraLookup","Assets/Sounds/sfx/camera_lookup.mp3"),("hover","Assets/Sounds/sfx/hover.wav"),("lever","Assets/Sounds/sfx/lever.wav"),("logoHover","Assets/Sounds/sfx/hover_big_buttons.wav"),("turnScreenOn","Assets/Sounds/sfx/turnscreenon.wav")})
        {
            var asset=AssetDatabase.LoadAssetAtPath<AudioClip>(clip.Item2);
            if(!asset)throw new InvalidOperationException("Menu sound is missing: "+clip.Item2);
            Ref(clip.Item1,asset);if(clip.Item1=="mainTheme")music.clip=asset;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach(var target in new UnityEngine.Object[]{menu,computer})
        {var settings=new SerializedObject(target);settings.FindProperty("menuAudio").objectReferenceValue=audio;settings.ApplyModifiedPropertiesWithoutUndo();}
        var menuData=new SerializedObject(menu);
        foreach(string panel in new[]{"mainPanel","settingsPanel"})
        foreach(var button in ((GameObject)menuData.FindProperty(panel).objectReferenceValue).GetComponentsInChildren<Button>(true))
        {
            var feedback=button.GetComponent<MenuButtonHoverSound>();if(!feedback)feedback=button.gameObject.AddComponent<MenuButtonHoverSound>();
            var settings=new SerializedObject(feedback);settings.FindProperty("menuAudio").objectReferenceValue=audio;
            settings.FindProperty("computer").objectReferenceValue=computer;settings.FindProperty("menu").objectReferenceValue=menu;settings.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var p in poses)
            if(p.Key.localPosition!=p.Value.Item1||p.Key.localRotation!=p.Value.Item2||p.Key.localScale!=p.Value.Item3||p.Key.parent!=p.Value.Item4)throw new InvalidOperationException("MENU authored transform changed: "+p.Key.name);
        return audio;
    }
}
