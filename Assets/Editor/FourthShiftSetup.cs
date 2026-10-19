using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class FourthShiftSetup
{
    public static readonly string[] Paths={"Assets/Scenes/DELIRIUM.unity","Assets/Scenes/scene_1_Kirill.unity"};
    private const string Request="Temp/FourthShift/apply-v1.request";
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
    [MenuItem("Tools/Punch Card/Set Up Fourth Shift Awakening And Hand Reading")]
    public static void Apply()
    {
        if(applying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        applying=true;var active=SceneManager.GetActiveScene();Directory.CreateDirectory("Temp/FourthShift");
        try
        {
            foreach(var path in Paths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene,"Temp/FourthShift/"+scene.name+"-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                    Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
            foreach(var path in Paths.Concat(new[]{"Assets/Scenes/THE_END.unity"}))EnableScene(path);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/FourthShift/applied.txt","PASS\nDELIRIUM: wind stops on first press; istolethatsample loops quietly and rises per press; immediate last-press black. Shift 4: waking eyelids near oven, hand inspection, day-only crimson profile / lights / moving READ THE HAND and heartbeat; only the floor's ceramic is replaced with blood_ceramic at runtime. Inserting hand stays interactive; lever commits, camera arrives about one second before the first stroke. Each drill stroke has splash and a blood-particle burst, followed by shaking alternating glances and pull into the monitor / THE_END. Recorder insert.wav applies throughout Kirill. Master volume and authored layout preserved.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){File.WriteAllText("Temp/FourthShift/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;}
    }
    public static void Configure(Scene scene)
    {
        if(!Paths.Contains(scene.path))throw new InvalidOperationException("Unsupported fourth-shift setup scene.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        if(scene.name=="DELIRIUM")
        {
            var ritual=all.Select(t=>t.GetComponent<DeliriumSequence>()).Single(c=>c);
            var data=new SerializedObject(ritual);var view=(Camera)data.FindProperty("view").objectReferenceValue;
            var track=Source(view.transform,"Ritual rising music","Assets/Sounds/music/istolethatsample.mp3",.06f);track.loop=true;
            Set(data,"ritualTrack",track);data.FindProperty("trackQuiet").floatValue=.06f;data.FindProperty("trackLoud").floatValue=.65f;data.ApplyModifiedPropertiesWithoutUndo();
            if(!all.Any(t=>t.GetComponent<SceneExit>()))Root(scene,"Ritual scene exit").gameObject.AddComponent<SceneExit>();
        }
        else ConfigureKirill(scene,all);
        foreach(var pose in poses)
            if(pose.Key.localPosition!=pose.Value.Item1||pose.Key.localRotation!=pose.Value.Item2||pose.Key.localScale!=pose.Value.Item3||pose.Key.parent!=pose.Value.Item4)
                throw new InvalidOperationException("Moved an authored transform: "+pose.Key.name);
    }
    private static void ConfigureKirill(Scene scene,Transform[] all)
    {
        var shifts=all.Select(t=>t.GetComponent<ShiftManager>()).Single(c=>c);
        var player=all.Select(t=>t.GetComponent<FirstPersonController>()).Single(c=>c);var interactor=player.GetComponent<PlayerInteractor>();var view=interactor.PlayerCamera;
        var final=all.Select(t=>t.GetComponent<FinalHandTrigger>()).Single(c=>c);var finalData=new SerializedObject(final);
        var lever=all.Select(t=>t.GetComponent<FeedLever>()).Single(c=>c);
        var slot=(ItemSlot)finalData.FindProperty("computerSlot").objectReferenceValue;
        var screen=(Transform)finalData.FindProperty("screenTarget").objectReferenceValue;
        var oven=all.Single(t=>t.name=="Oven");
        var root=Root(scene,"Fourth shift awakening");var awakening=Ensure<FourthShiftAwakening>(root.gameObject);
        var top=oven.Find("OvenToothTop").GetComponent<Renderer>().bounds;var bottom=oven.Find("OvenToothBottom").GetComponent<Renderer>().bounds;
        var ovenAim=Marker(root,"Wake facing oven",(top.center+bottom.center)*.5f,Quaternion.identity);
        Vector3 card=slot.AnchorTransform.position;
        Vector3 front=Vector3.ProjectOnPlane(card-screen.position,Vector3.up).normalized;
        if(front.sqrMagnitude<.1f)front=Vector3.ProjectOnPlane(view.transform.position-card,Vector3.up).normalized;
        Vector3 eye=card+front*1.3f+Vector3.up*2.1f-Vector3.Cross(Vector3.up,front)*.5f;
        var aim=Marker(root,"Hand reading target",card+Vector3.up*.12f,Quaternion.identity);
        var pose=Marker(root,"Hand reading view",eye,Quaternion.LookRotation(aim.position-eye,Vector3.up));
        var data=new SerializedObject(awakening);
        Set(data,"shifts",shifts);Set(data,"interactor",interactor);Set(data,"controller",player);Set(data,"view",view);Set(data,"ovenAim",ovenAim);Set(data,"finalReading",final);
        Set(data,"heartbeat",Source(view.transform,"Fourth shift heartbeat","Assets/Sounds/sfx/new/heartbeat.mp3",.35f));
        Set(data,"wakingShader",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/WakingVision.shader"));
        Set(data,"font",AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"));
        Set(data,"floor",all.Single(t=>t.name=="Floor").GetComponent<Renderer>());
        Set(data,"ceramic",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ceramic.png"));
        Set(data,"bloodCeramic",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/blood_ceramic.png"));
        data.ApplyModifiedPropertiesWithoutUndo();
        Set(finalData,"lever",lever);Set(finalData,"readingPose",pose);Set(finalData,"punchingAim",aim);Set(finalData,"awakening",awakening);
        Set(finalData,"punchSplash",Source(slot.transform,"Hand punching splash","Assets/Sounds/sfx/splash.mp3",.3f));
        Set(finalData,"punchBlood",BloodParticles(final.transform));
        finalData.FindProperty("shakeSeconds").floatValue=3.2f;finalData.FindProperty("shakeDegrees").floatValue=3.5f;
        finalData.FindProperty("flySeconds").floatValue=1.25f;finalData.FindProperty("fovKick").floatValue=20;finalData.ApplyModifiedPropertiesWithoutUndo();
        var shiftData=new SerializedObject(shifts);var days=shiftData.FindProperty("shifts");
        if(days.arraySize<4)throw new InvalidOperationException("Four shifts are required.");
        var hand=days.GetArrayElementAtIndex(3).FindPropertyRelative("giveToPlayer").objectReferenceValue as GameObject;
        if(!hand||!hand.GetComponent<FinalHand>())throw new InvalidOperationException("Shift 4 must give the hand prefab to the player.");
        foreach(var recorder in all.Select(t=>t.GetComponent<CassetteRecorder>()).Where(c=>c))
        {var recorderData=new SerializedObject(recorder);Set(recorderData,"insertSource",Source(recorder.transform,"Recorder cassette insert","Assets/Sounds/sfx/insert.wav",.5f));recorderData.ApplyModifiedPropertiesWithoutUndo();}
    }
    private static ParticleSystem BloodParticles(Transform parent)
    {
        var t=parent.Find("Hand impact blood");
        if(!t){t=new GameObject("Hand impact blood").transform;t.SetParent(parent,false);t.rotation=Quaternion.LookRotation(Vector3.up);}
        var particles=Ensure<ParticleSystem>(t.gameObject);particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Local;
        main.maxParticles=128;main.startSpeed=new ParticleSystem.MinMaxCurve(1.1f,2.5f);main.startLifetime=new ParticleSystem.MinMaxCurve(.22f,.65f);main.gravityModifier=1.5f;
        main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(.025f,.065f);main.startSizeY=new ParticleSystem.MinMaxCurve(.04f,.1f);main.startSizeZ=1;main.startColor=Color.white;
        var emission=particles.emission;emission.enabled=false;
        var shape=particles.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=55;shape.radius=.07f;
        var colour=particles.colorOverLifetime;colour.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.4f,.2f,.2f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.6f),new GradientAlphaKey(0,1)});colour.color=gradient;
        if(!AssetDatabase.IsValidFolder("Assets/Materials/FourthShift"))AssetDatabase.CreateFolder("Assets/Materials","FourthShift");
        const string path="Assets/Materials/FourthShift/Blood impact.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/HandBloodSpray.shader"));AssetDatabase.CreateAsset(material,path);}
        var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.025f;
        return particles;
    }
    private static AudioSource Source(Transform parent,string name,string path,float volume)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(!clip)throw new InvalidOperationException("Missing sound: "+path);
        var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);}
        var source=Ensure<AudioSource>(child.gameObject);source.clip=clip;source.volume=volume;source.loop=false;source.playOnAwake=false;source.spatialBlend=1;
        source.ignoreListenerVolume=false;source.dopplerLevel=0;source.minDistance=2;source.maxDistance=20;source.rolloffMode=AudioRolloffMode.Linear;EditorUtility.SetDirty(source);return source;
    }
    private static Transform Root(Scene scene,string name)
    {var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name==name);if(root)return root.transform;root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);return root.transform;}
    private static Transform Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
    {var child=parent.Find(name);if(child)return child;child=new GameObject(name).transform;child.SetParent(parent,false);child.SetPositionAndRotation(position,rotation);return child;}
    private static T Ensure<T>(GameObject owner)where T:Component {var component=owner.GetComponent<T>();return component?component:owner.AddComponent<T>();}
    private static void Set(SerializedObject target,string name,UnityEngine.Object value)=>target.FindProperty(name).objectReferenceValue=value;
    private static void EnableScene(string path)
    {var scenes=EditorBuildSettings.scenes.ToList();var entry=scenes.FirstOrDefault(s=>s.path==path);if(entry==null)scenes.Add(new EditorBuildSettingsScene(path,true));else entry.enabled=true;EditorBuildSettings.scenes=scenes.ToArray();}
}
