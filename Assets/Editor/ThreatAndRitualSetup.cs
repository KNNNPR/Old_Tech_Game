using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-scoped additions; never replaces whole scenes or authored layouts.</summary>
public static class ThreatAndRitualSetup
{
    private const string Request = "Temp/ThreatAndRitual/apply-v1.request";
    public static readonly string[] Paths = { "Assets/Scenes/scene_1_Kirill.unity", "Assets/Scenes/THE_END.unity", "Assets/Scenes/DELIRIUM.unity" };
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update += () => {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;
            if(File.Exists(Request))Apply();
        };
    }
    [MenuItem("Tools/Punch Card/Set Up Sorting Threat And Ritual Audio")]
    public static void Apply()
    {
        if(applying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        applying=true;var active=SceneManager.GetActiveScene();Directory.CreateDirectory("Temp/ThreatAndRitual");
        try
        {
            foreach(string path in Paths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene,"Temp/ThreatAndRitual/"+scene.name+"-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            var build=EditorBuildSettings.scenes.ToList();string defeat="Assets/Scenes/GAME_OVER.unity";
            var entry=build.FirstOrDefault(s=>s.path==defeat);if(entry==null)build.Add(new EditorBuildSettingsScene(defeat,true));else entry.enabled=true;
            EditorBuildSettings.scenes=build.ToArray();AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/ThreatAndRitual/applied.txt","PASS\nKirill: fatal second sorting error, authored balcony door / synchronized knocks, side shot and abrupt GAME_OVER; photo hang and third-shift heartbeat / breakdown. THE_END alone: slime footsteps. DELIRIUM: one bite per click, error + splash + metal cues, accelerating heartbeat, void and quiet wind. All new sounds master-controlled; only void ambience is 2D. Authored transforms preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){File.WriteAllText("Temp/ThreatAndRitual/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static void Configure(Scene scene)
    {
        if(!Paths.Contains(scene.path))throw new InvalidOperationException("Unsupported scene: "+scene.path);
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        if(scene.name=="scene_1_Kirill")ConfigureKirill(scene,all);
        else if(scene.name=="THE_END")
        {
            var room=all.Select(t=>t.GetComponent<GameplaySceneAudio>()).Single(c=>c);
            var steps=(AudioSource)new SerializedObject(room).FindProperty("footsteps").objectReferenceValue;
            if(!steps)throw new InvalidOperationException("THE_END footsteps source is missing.");
            steps.clip=Clip("slimestep.mp3");steps.spatialBlend=1;steps.ignoreListenerVolume=false;EditorUtility.SetDirty(steps);
        }
        else ConfigureDelirium(scene,all);
        foreach(var p in poses)
            if(p.Key.localPosition!=p.Value.Item1||p.Key.localRotation!=p.Value.Item2||p.Key.localScale!=p.Value.Item3||p.Key.parent!=p.Value.Item4)
                throw new InvalidOperationException("Moved an authored transform: "+p.Key.name);
    }
    private static void ConfigureKirill(Scene scene,Transform[] all)
    {
        var player=all.Select(t=>t.GetComponent<FirstPersonController>()).Single(c=>c);
        var interactor=player.GetComponent<PlayerInteractor>();var camera=interactor.PlayerCamera;
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/door_balcony.anim");
        var balcony=all.Where(t=>t.name=="Door").Single(t=>t.GetComponent<Animator>()&&t.GetComponent<Animator>().runtimeAnimatorController
            &&t.GetComponent<Animator>().runtimeAnimatorController.animationClips.Any(c=>c==clip));
        // Its authored controller loops by default. The death director samples it once on demand.
        balcony.GetComponent<Animator>().enabled=false;
        var root=Root(scene,"Sorting failure cinematic");var director=Ensure<SortingFailureCinematic>(root.gameObject);
        var doorBounds=balcony.GetComponent<Renderer>().bounds;
        var computer=all.Single(t=>t.name=="MysticComputer").GetComponent<Renderer>().bounds;
        Vector3 towardRoom=Vector3.ProjectOnPlane(computer.center-doorBounds.center,Vector3.up).normalized;
        Vector3 side=Vector3.Cross(Vector3.up,towardRoom);
        Vector3 eye=doorBounds.center+towardRoom*4.5f+side*7.5f;eye.y=camera.transform.position.y;
        Vector3 aim=Vector3.Lerp(doorBounds.center,computer.center,.5f)+Vector3.up*.3f;
        var viewMarker=Marker(root,"Balcony side view",eye,Quaternion.LookRotation(aim-eye,Vector3.up));
        var doorAim=Marker(root,"Balcony look target",doorBounds.center+Vector3.up*.4f,Quaternion.identity);
        var data=new SerializedObject(director);
        Set(data,"sorting",all.Select(t=>t.GetComponent<SortingManager>()).Single(c=>c));Set(data,"shift",all.Select(t=>t.GetComponent<ShiftManager>()).Single(c=>c));
        Set(data,"interactor",interactor);Set(data,"controller",player);Set(data,"view",camera);Set(data,"balconyDoor",balcony);Set(data,"doorAim",doorAim);Set(data,"sidePose",viewMarker);Set(data,"doorMotion",clip);
        var knock=Source(balcony,"Threat door knocks",Clip("new/doorbang.wav"),.5f);knock.minDistance=6;knock.maxDistance=35;
        var open=Source(balcony,"Threat door opening",Clip("autodoor.mp3"),.5f);open.minDistance=6;open.maxDistance=35;
        Set(data,"knockAudio",knock);Set(data,"openingAudio",open);
        var euler=AnimationUtility.GetCurveBindings(clip).First(b=>b.propertyName.IndexOf("Euler",StringComparison.OrdinalIgnoreCase)>=0&&b.propertyName.EndsWith(".z"));
        var keys=AnimationUtility.GetEditorCurve(clip,euler).keys;
        var peaks=Enumerable.Range(1,keys.Length-2).Where(i=>keys[i].value>keys[i-1].value+1&&keys[i].value>keys[i+1].value+1).ToArray();
        if(peaks.Length==0)throw new InvalidOperationException("No door knocks found in door_balcony.anim.");
        var times=data.FindProperty("knockTimes");times.arraySize=peaks.Length;
        for(int i=0;i<peaks.Length;i++)times.GetArrayElementAtIndex(i).floatValue=keys[peaks[i]].time;
        data.FindProperty("openingStart").floatValue=keys[peaks[peaks.Length-1]+1].time;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach(var pin in all.Select(t=>t.GetComponent<PhotoClothespin>()).Where(c=>c))Ref(pin,"hangAudio",Source(pin.transform,"Photo hang audio",Clip("new/hang.wav"),.45f));
        foreach(var faint in all.Select(t=>t.GetComponent<PlayerFaint>()).Where(c=>c))
        {
            var heart=Source(camera.transform,"Third shift heartbeat",Clip("new/heartbeat.mp3"),.4f);heart.loop=true;
            Ref(faint,"heartbeatAudio",heart);Ref(faint,"breakdownAudio",Source(camera.transform,"Third shift breakdown",Clip("breakdown.mp3"),.42f));
        }
    }
    private static void ConfigureDelirium(Scene scene,Transform[] all)
    {
        var director=all.Select(t=>t.GetComponent<DeliriumSequence>()).Single(c=>c);
        var data=new SerializedObject(director);var oven=(Transform)data.FindProperty("oven").objectReferenceValue;
        var camera=(Camera)data.FindProperty("view").objectReferenceValue;var button=(PressButton)data.FindProperty("button").objectReferenceValue;
        var bite=Ensure<DeliriumOvenBite>(oven.gameObject);var biteData=new SerializedObject(bite);
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/incinerate.anim");
        var bindings=AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("m_LocalPosition.")).ToArray();
        Vector3 Position(string name,float time)
        {
            float Value(string axis)=>AnimationUtility.GetEditorCurve(clip,bindings.Single(b=>b.path==name&&b.propertyName=="m_LocalPosition."+axis)).Evaluate(time);
            return new Vector3(Value("x"),Value("y"),Value("z"));
        }
        var keys=bindings.SelectMany(b=>AnimationUtility.GetEditorCurve(clip,b).keys.Select(k=>k.time)).Distinct().OrderBy(t=>t).ToArray();
        float Gap(float time)=>(Position("OvenToothTop",time)-Position("OvenToothBottom",time)).sqrMagnitude;
        int closure=Enumerable.Range(1,keys.Length-2).First(i=>Gap(keys[i])<Gap(keys[i-1])-1e-9f&&Gap(keys[i])<Gap(keys[i+1])-1e-9f);
        Set(biteData,"upper",oven.Find("OvenToothTop"));Set(biteData,"lower",oven.Find("OvenToothBottom"));
        biteData.FindProperty("upperClosedOffset").vector3Value=Position("OvenToothTop",keys[closure])-Position("OvenToothTop",keys[closure-1]);
        biteData.FindProperty("lowerClosedOffset").vector3Value=Position("OvenToothBottom",keys[closure])-Position("OvenToothBottom",keys[closure-1]);
        Set(biteData,"metalAudio",Source(oven,"Ritual metal bite",Clip("metal_bite.mp3"),.6f));biteData.ApplyModifiedPropertiesWithoutUndo();
        Set(data,"bite",bite);Set(data,"splashAudio",Source(oven,"Ritual splash",Clip("splash.mp3"),.35f));
        Set(data,"heartbeatAudio",Source(camera.transform,"Ritual heartbeat",Clip("new/heartbeat.mp3"),.35f));
        var ambient=Source(director.transform,"Void ambience",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/music/void.wav"),.3f);ambient.loop=true;ambient.spatialBlend=0;Set(data,"voidAudio",ambient);
        var wind=Source(oven,"Void wind",Clip("wind.wav"),.1f);wind.loop=true;wind.minDistance=25;wind.maxDistance=100;Set(data,"windAudio",wind);
        data.ApplyModifiedPropertiesWithoutUndo();
        var press=Source(button.transform,"Ritual button error",Clip("new/error.mp3"),.5f);Ref(button,"audioSource",press);Ref(button,"clickClip",press.clip);
        foreach(var room in all.Select(t=>t.GetComponent<GameplaySceneAudio>()).Where(c=>c))
        {
            var old=(AudioSource)new SerializedObject(room).FindProperty("ambient").objectReferenceValue;
            if(old){old.clip=null;old.playOnAwake=false;EditorUtility.SetDirty(old);}
        }
    }
    private static Transform Root(Scene scene,string name)
    {
        var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name==name);if(root)return root.transform;
        root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);return root.transform;
    }
    private static Transform Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
    {
        var t=parent.Find(name);if(t)return t;
        t=new GameObject(name).transform;t.SetParent(parent,false);t.SetPositionAndRotation(position,rotation);return t;
    }
    private static AudioClip Clip(string path)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/sfx/"+path);if(!clip)throw new InvalidOperationException("Missing audio: "+path);return clip;
    }
    private static AudioSource Source(Transform parent,string name,AudioClip clip,float gain)
    {
        if(!clip)throw new InvalidOperationException("Missing audio for "+name);
        var t=parent.Find(name);
        if(!t){t=new GameObject(name).transform;t.SetParent(parent,false);var renderer=parent.GetComponent<Renderer>();if(renderer)t.position=renderer.bounds.center;}
        var source=Ensure<AudioSource>(t.gameObject);source.clip=clip;source.volume=gain;source.playOnAwake=false;source.loop=false;
        source.spatialBlend=1;source.minDistance=2;source.maxDistance=18;source.dopplerLevel=0;source.ignoreListenerVolume=false;source.rolloffMode=AudioRolloffMode.Linear;
        EditorUtility.SetDirty(source);return source;
    }
    private static T Ensure<T>(GameObject target) where T:Component {var component=target.GetComponent<T>();return component?component:target.AddComponent<T>();}
    private static void Set(SerializedObject target,string name,UnityEngine.Object value)=>target.FindProperty(name).objectReferenceValue=value;
    private static void Ref(UnityEngine.Object target,string name,UnityEngine.Object value)
    {var data=new SerializedObject(target);Set(data,name,value);data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(target);}
}
