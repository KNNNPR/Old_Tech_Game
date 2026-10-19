using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Final terminal audio / credits and the author's slowed entrance animation.</summary>
public static class FinaleSetup
{
    public static readonly string[] Paths={"Assets/Scenes/THE_END.unity","Assets/Scenes/scene_1_Kirill.unity","Assets/Scenes/TUTORIAL.unity"};
    private const string Request="Temp/Finale/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void Watch()
    {EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;if(File.Exists(Request))Apply();};}
    [MenuItem("Tools/Punch Card/Set Up Finale Credits And Authored Entrance")]
    public static void Apply()
    {
        if(applying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        applying=true;var active=SceneManager.GetActiveScene();Directory.CreateDirectory("Temp/Finale");
        try
        {
            PrepareLogo();PreparePauseAssets();
            foreach(string path in Paths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene,"Temp/Finale/"+scene.name+"-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            var build=EditorBuildSettings.scenes.ToList();foreach(string path in new[]{"Assets/Scenes/THE_END.unity","Assets/Scenes/MENU.unity"})
            {var entry=build.FirstOrDefault(s=>s.path==path);if(entry==null)build.Add(new EditorBuildSettingsScene(path,true));else entry.enabled=true;}EditorBuildSettings.scenes=build.ToArray();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/Finale/applied.txt","PASS\nTHE_END: computer hum / approach / back / slider / granted cues; 12 EXIT presses with one error each, rising istolethatsample / heavybreath / accelerating heartbeat. Screamer starts at hand appearance and continues across the abrupt black after its short lunge; no THE END title or master-volume override. Credits scroll down with AppleJollyGarden SVG-derived logo, reference text / roles, credits idk loop, fade to MENU. Tutorial and Kirill entrance use entering_room.anim once at 0.45x; door cues, walking sounds and final Helpy gaze retained. Original scene layout / animation clips preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){File.WriteAllText("Temp/Finale/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static void PrepareLogo()
    {
        const string path="Assets/Textures/Credits/AppleJollyGarden.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer)throw new InvalidOperationException("Missing credits logo.");
        bool changed=importer.textureShape!=TextureImporterShape.Texture2D||importer.mipmapEnabled||!importer.alphaIsTransparency||importer.wrapMode!=TextureWrapMode.Clamp;
        importer.textureShape=TextureImporterShape.Texture2D;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;if(changed)importer.SaveAndReimport();
        if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))throw new InvalidOperationException("Credits logo did not import as a Texture2D: "+string.Join(", ",AssetDatabase.LoadAllAssetsAtPath(path).Select(a=>a.GetType().Name+":"+a.name)));
    }
    public static void PreparePauseAssets()
    {
        if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
        const string path="Assets/Resources/GameplayPauseAssets.asset";
        var asset=AssetDatabase.LoadAssetAtPath<GameplayPauseAssets>(path);
        if(!asset){asset=ScriptableObject.CreateInstance<GameplayPauseAssets>();AssetDatabase.CreateAsset(asset,path);}
        asset.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        asset.hover=Clip("sfx/hover.wav");asset.press=Clip("sfx/press_ingame.mp3");asset.back=Clip("sfx/esc.mp3");EditorUtility.SetDirty(asset);
    }
    public static void Configure(Scene scene)
    {
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        if(scene.name=="THE_END")ConfigureEnd(scene,all);
        foreach(var intro in all.Select(t=>t.GetComponent<HelpyTutorialIntro>()).Where(c=>c))
        {
            var data=new SerializedObject(intro);var entrance=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/entering_room.anim");Set(data,"enteringRoom",entrance);data.FindProperty("enteringRoomSpeed").floatValue=.45f;data.ApplyModifiedPropertiesWithoutUndo();
            var actor=(FirstPersonController)data.FindProperty("player").objectReferenceValue;
            var autoplay=actor.GetComponent<Animator>();
            // The scene's Player controller loops this same root-motion clip. The intro samples it once.
            if(autoplay&&autoplay.runtimeAnimatorController&&autoplay.runtimeAnimatorController.animationClips.Contains(entrance))autoplay.enabled=false;
            var room=all.Select(t=>t.GetComponent<GameplaySceneAudio>()).Single(c=>c);var roomData=new SerializedObject(room);
            var door=(Transform)data.FindProperty("entranceDoor").objectReferenceValue;
            Set(roomData,"entranceDoorOpenAudio",Source(door,"Entrance door creak audio","sfx/dooropen.mp3",.5f));
            Set(roomData,"entranceDoorAudio",Source(door,"Door motion audio","sfx/autodoor.mp3",.5f));roomData.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var p in poses)
            if(p.Key.localPosition!=p.Value.Item1||p.Key.localRotation!=p.Value.Item2||p.Key.localScale!=p.Value.Item3||p.Key.parent!=p.Value.Item4)
                throw new InvalidOperationException("Moved an authored transform: "+scene.name+"/"+p.Key.name);
    }
    private static void ConfigureEnd(Scene scene,Transform[] all)
    {
        var terminal=all.Select(t=>t.GetComponent<ExitTerminal>()).Single(c=>c);var data=new SerializedObject(terminal);
        var player=all.Select(t=>t.GetComponent<PlayerInteractor>()).Single(c=>c);var view=player.PlayerCamera;
        var screen=all.Single(t=>t.name=="Monitor"&&t.GetComponent<Renderer>());
        var computer=all.Single(t=>t.name=="MysticComputer");
        var room=all.Select(t=>t.GetComponent<GameplaySceneAudio>()).Single(c=>c);var roomData=new SerializedObject(room);
        Set(roomData,"computerHum",Source(computer,"Computer ironlungs audio","sfx/ironlungs.mp3",.28f));
        Set(roomData,"cameraMove",Source(view.transform,"Station camera audio","sfx/camera_lookup.mp3",.35f));roomData.ApplyModifiedPropertiesWithoutUndo();
        Set(data,"audioSource",Source(screen,"Terminal feedback audio","sfx/press_ingame.mp3",.35f));Set(data,"grantedClip",Clip("sfx/press_ingame.mp3"));
        Set(data,"errorAudio",Source(screen,"EXIT error audio","sfx/new/error.mp3",.45f));Set(data,"backAudio",Source(view.transform,"Terminal back audio","sfx/esc.mp3",.35f));
        Set(data,"tensionMusic",Source(view.transform,"EXIT tension music","music/istolethatsample.mp3",.055f));
        Set(data,"breathingAudio",Source(view.transform,"EXIT heavy breathing","sfx/heavybreath.mp3",.08f));
        Set(data,"heartbeatAudio",Source(view.transform,"EXIT heartbeat","sfx/new/heartbeat.mp3",.12f));
        Set(data,"screamAudio",Source(view.transform,"Final hand scream","sfx/screamer.mp3",.65f));
        data.FindProperty("dodges").intValue=11;data.FindProperty("handSeconds").floatValue=.28f;data.FindProperty("jumpSeconds").floatValue=.14f;data.FindProperty("flinchAt").floatValue=.65f;
        var credits=terminal.GetComponent<EndCreditsRoll>();if(!credits)credits=terminal.gameObject.AddComponent<EndCreditsRoll>();
        var creditData=new SerializedObject(credits);Set(creditData,"logo",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Credits/AppleJollyGarden.png"));
        Set(creditData,"font",AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"));
        Set(creditData,"music",Source(view.transform,"Credits music","music/credits idk.mp3",.45f));creditData.FindProperty("scrollSeconds").floatValue=36;creditData.FindProperty("returnToMenu").boolValue=true;creditData.ApplyModifiedPropertiesWithoutUndo();
        Set(data,"credits",credits);data.ApplyModifiedPropertiesWithoutUndo();
        var button=(ExtractButton)data.FindProperty("exitButton").objectReferenceValue;var buttonData=new SerializedObject(button);Set(buttonData,"audioSource",null);Set(buttonData,"clickClip",null);buttonData.ApplyModifiedPropertiesWithoutUndo();
        var dial=(CipherDial)data.FindProperty("cipherDial").objectReferenceValue;var dialData=new SerializedObject(dial);Set(dialData,"audioSource",Source(screen,"Screen slider audio","sfx/slider.mp3",.25f));Set(dialData,"stepClip",Clip("sfx/slider.mp3"));dialData.ApplyModifiedPropertiesWithoutUndo();
        foreach(var printer in all.Select(t=>t.GetComponent<PhotoPrinterSpawner>()).Where(c=>c))
        {var p=new SerializedObject(printer);Set(p,"printingAudio",Source(printer.transform,"Photo printing audio","sfx/printing.wav",.35f));p.ApplyModifiedPropertiesWithoutUndo();}
    }
    private static AudioClip Clip(string path)
    {var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/"+path);if(!clip)throw new InvalidOperationException("Missing sound: "+path);return clip;}
    private static AudioSource Source(Transform parent,string name,string clip,float volume)
    {
        var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);if(parent.TryGetComponent<Renderer>(out var renderer))child.position=renderer.bounds.center;}
        var source=child.GetComponent<AudioSource>();if(!source)source=child.gameObject.AddComponent<AudioSource>();
        source.clip=Clip(clip);source.volume=volume;source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.minDistance=2;source.maxDistance=18;source.rolloffMode=AudioRolloffMode.Linear;source.dopplerLevel=0;source.ignoreListenerVolume=false;return source;
    }
    private static void Set(SerializedObject data,string name,UnityEngine.Object value)=>data.FindProperty(name).objectReferenceValue=value;
}
