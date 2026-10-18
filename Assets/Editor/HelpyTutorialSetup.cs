using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HelpyTutorialSetup
{
    private const string ScenePath = "Assets/Scenes/TUTORIAL.unity";
    private const string Request = "Temp/Tutorial/entrance-bypass-v4.request";
    private static bool applying;
    private static double nextCheck;

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.update += () => { if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;ApplyRequested(); };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested();
        };
        EditorSceneManager.sceneOpened += (_,__) => ApplyRequested();
    }
    private static void ApplyRequested()
    {
        if (applying || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        var active=SceneManager.GetActiveScene();
        applying=true;
        try
        {
            if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            Apply(); File.Delete(Request);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if(opened && scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            applying=false;
        }
    }

    [MenuItem("Tools/Punch Card/Tutorial/Apply Helpy Introduction")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Tutorial");
        EditorSceneManager.SaveScene(scene,"Temp/Tutorial/TUTORIAL-before-intro-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        Configure(scene);
        Validate(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Tutorial/entrance-bypass-v4-applied.txt","TUTORIAL right-door entry follows three waypoints around the computer with player-width clearance, and faces Helpy's landing point at arrival. Saved and validated.\n");
    }

    public static void ApplyBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("ApplyBatch is for an offline Unity editor only.");
        applying=true;
        try
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Apply();
            if(File.Exists(Request))File.Delete(Request);
        }
        finally { applying=false; }
    }

    public static HelpyTutorialIntro Configure(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var intro = roots.SelectMany(r=>r.GetComponentsInChildren<HelpyTutorialIntro>(true)).FirstOrDefault();
        if (!intro)
        {
            var root = new GameObject("Helpy tutorial introduction");
            SceneManager.MoveGameObjectToScene(root,scene);
            intro = root.AddComponent<HelpyTutorialIntro>();
        }
        var all = roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var player = all.Select(t=>t.GetComponent<FirstPersonController>()).First(p=>p);
        var camera = all.Select(t=>t.GetComponent<Camera>()).First(c=>c && c.CompareTag("MainCamera"));
        var robot = roots.Single(r=>string.Equals(r.name,"robot_helper",StringComparison.OrdinalIgnoreCase));
        var hud = all.Select(t=>t.GetComponent<InteractionHUD>()).FirstOrDefault(h=>h);
        var serialized = new SerializedObject(intro);
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
        serialized.FindProperty("playerCamera").objectReferenceValue = camera;
        serialized.FindProperty("robot").objectReferenceValue = robot.transform;
        var face = robot.GetComponentsInChildren<Renderer>(true).Single(f=>f.name=="Cube.001");
        var faceMesh=face.GetComponent<MeshFilter>() ? face.GetComponent<MeshFilter>().sharedMesh : face.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        serialized.FindProperty("faceNormal").vector3Value = faceMesh.normals[0];
        serialized.FindProperty("playerHUD").objectReferenceValue = hud ? hud.GetComponentInParent<Canvas>() : null;
        serialized.FindProperty("subtitleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var door=all.Where(t=>t.name=="Door"&&t.GetComponent<Renderer>()).OrderByDescending(t=>t.GetComponent<Renderer>().bounds.center.x).First();
        Bounds doorway=door.GetComponent<Renderer>().bounds;
        var centre=Marker(intro.transform,"02 - Room centre",player.transform.position,Quaternion.LookRotation(Vector3.right));
        Vector3 startPosition=new Vector3(doorway.max.x+1.3f,centre.position.y,doorway.center.z);
        Quaternion entryRotation=Quaternion.LookRotation(centre.position-startPosition,Vector3.up);
        var start=Marker(intro.transform,"01 - Outside Door",startPosition,entryRotation);
        start.SetPositionAndRotation(startPosition,entryRotation);
        var hinge=Marker(intro.transform,"Door hinge",new Vector3(doorway.center.x,doorway.center.y,doorway.min.z),Quaternion.identity);
        hinge.SetPositionAndRotation(new Vector3(doorway.center.x,doorway.center.y,doorway.min.z),Quaternion.identity);
        serialized.FindProperty("entranceStart").objectReferenceValue=start;
        serialized.FindProperty("roomCentre").objectReferenceValue=centre;
        serialized.FindProperty("entranceDoor").objectReferenceValue=door;
        serialized.FindProperty("doorHinge").objectReferenceValue=hinge;
        serialized.FindProperty("doorOpenAngle").floatValue=-90f;
        ConfigureEntranceRoute(serialized,intro.transform,all,player,start,centre);
        serialized.FindProperty("appearance").objectReferenceValue=Clip("appear_anim");
        serialized.FindProperty("idle").objectReferenceValue=Clip("move_robot");
        var route=serialized.FindProperty("movement");route.arraySize=4;
        string[] movements={"move_to","move_to2","move_to3","move_to4"};
        for(int i=0;i<4;i++)route.GetArrayElementAtIndex(i).objectReferenceValue=Clip(movements[i]);
        serialized.FindProperty("tube").objectReferenceValue=PunchCardTubeSetup.Configure(scene);
        var slot=all.Select(t=>t.GetComponent<ItemSlot>()).Single(s=>s);
        var lever=all.Select(t=>t.GetComponent<FeedLever>()).Single(l=>l);
        serialized.FindProperty("cardReader").objectReferenceValue=slot;
        serialized.FindProperty("cardLever").objectReferenceValue=lever;
        serialized.FindProperty("secondsPerCharacter").floatValue=.075f;
        serialized.FindProperty("minimumLineDuration").floatValue=4f;
        serialized.FindProperty("autoAdvance").boolValue=true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var sway = intro.GetComponent<HelpyIdleSway>();
        if(sway)sway.enabled=false;
        // Refresh these existing references as well; the tutorial must be playable after its cinematic.
        var input=new SerializedObject(player.GetComponent<PlayerInteractor>());
        input.FindProperty("playerCamera").objectReferenceValue=camera;
        input.FindProperty("controller").objectReferenceValue=player;
        input.FindProperty("holdPoint").objectReferenceValue=all.Single(t=>t.name=="HoldPoint");
        input.ApplyModifiedPropertiesWithoutUndo();
        var controller=new SerializedObject(player);
        controller.FindProperty("cameraPivot").objectReferenceValue=all.Single(t=>t.name=="CameraPivot");
        controller.ApplyModifiedPropertiesWithoutUndo();
        var reader=new SerializedObject(slot);reader.FindProperty("anchor").objectReferenceValue=all.Single(t=>t.name=="PunchCardAnchor");reader.ApplyModifiedPropertiesWithoutUndo();
        var transport=new SerializedObject(lever);transport.FindProperty("slot").objectReferenceValue=slot;transport.FindProperty("handle").objectReferenceValue=lever.transform;transport.ApplyModifiedPropertiesWithoutUndo();
        return intro;
    }

    public static void Validate(Scene scene)
    {
        var intro=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HelpyTutorialIntro>(true)).Single();
        var settings=new SerializedObject(intro);
        foreach(var name in new[]{"playerCamera","player","interactor","robot","entranceStart","roomCentre","entranceDoor","doorHinge","appearance","idle","tube","cardReader","cardLever"})
            if(!settings.FindProperty(name).objectReferenceValue)throw new InvalidOperationException("Missing TUTORIAL reference: "+name);
        var clips=settings.FindProperty("movement");
        if(clips.arraySize!=4)throw new InvalidOperationException("Helpy needs four ordered movement clips.");
        for(int i=0;i<4;i++)if(!clips.GetArrayElementAtIndex(i).objectReferenceValue)throw new InvalidOperationException("Missing Helpy movement clip "+i);
        var route=settings.FindProperty("entranceRoute");
        if(route.arraySize==0)throw new InvalidOperationException("The tutorial entry needs a route around the computer.");
        for(int i=0;i<route.arraySize;i++)if(!route.GetArrayElementAtIndex(i).objectReferenceValue)throw new InvalidOperationException("Missing tutorial entry waypoint "+i);
    }

    private static void ConfigureEntranceRoute(SerializedObject settings,Transform root,Transform[] all,FirstPersonController player,Transform start,Transform centre)
    {
        var computer=all.Single(t=>t.name=="MysticComputer");
        var character=player.GetComponent<CharacterController>();
        float radius=character?character.radius:.5f;
        float clearance=radius+.4f;
        Vector3 delta=centre.position-start.position;
        var ray=new Ray(start.position,delta.normalized);
        var blockers=computer.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger).Where(c=>
        {
            var padded=c.bounds;
            padded.Expand(new Vector3(clearance*2f,character?character.height:2f,clearance*2f));
            return padded.IntersectRay(ray,out float distance)&&distance<delta.magnitude;
        }).ToArray();
        if(blockers.Length==0)throw new InvalidOperationException("Cannot identify the computer collider blocking the tutorial entry.");
        Bounds obstacle=blockers[0].bounds;
        foreach(var blocker in blockers.Skip(1))obstacle.Encapsulate(blocker.bounds);
        float side=obstacle.max.x+clearance,front=obstacle.max.z+clearance;
        Vector3[] positions=
        {
            new Vector3(side,centre.position.y,start.position.z),
            new Vector3(side,centre.position.y,front),
            new Vector3(Mathf.Min(centre.position.x,obstacle.min.x-clearance),centre.position.y,front)
        };
        string[] names={"03 - Along computer side","04 - Around computer corner","05 - Clear computer front"};
        var route=settings.FindProperty("entranceRoute");route.arraySize=positions.Length;
        float length=0;Vector3 previous=start.position;
        for(int i=0;i<positions.Length;i++)
        {
            var point=Marker(root,names[i],positions[i],Quaternion.identity);
            point.position=positions[i];
            route.GetArrayElementAtIndex(i).objectReferenceValue=point;
            length+=Vector3.Distance(previous,positions[i]);previous=positions[i];
        }
        length+=Vector3.Distance(previous,centre.position);
        settings.FindProperty("entryWalkSeconds").floatValue=Mathf.Max(5.2f,length/2.2f);
        start.rotation=Quaternion.LookRotation(positions[0]-start.position,Vector3.up);
    }
    private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/"+name+".anim") ?? throw new InvalidOperationException("Missing Helpy clip: "+name);
    private static Transform Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
    {
        var marker=parent.Find(name);
        if(!marker){marker=new GameObject(name).transform;marker.SetParent(parent,false);marker.SetPositionAndRotation(position,rotation);}
        return marker;
    }
}
