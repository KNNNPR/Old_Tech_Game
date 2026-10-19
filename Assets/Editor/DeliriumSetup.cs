using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class DeliriumSetup
{
    public const string Folder="Assets/Materials/Delirium";
    public static string ScenePath=>AssetDatabase.GUIDToAssetPath("d39d306ce0756b34891b4cc904c717dd");
    private const string Request="Temp/Delirium/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.update+=()=>
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;
            nextCheck=EditorApplication.timeSinceStartup+1;
            if(File.Exists(Request))Apply();
        };
    }
    [MenuItem("Tools/Punch Card/Set Up DELIRIUM")]
    public static void Apply()
    {
        if(applying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        applying=true;
        try
        {
            if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Directory.CreateDirectory("Temp/Delirium");
            EditorSceneManager.SaveScene(scene,"Temp/Delirium/DELIRIUM-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Configure(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/Delirium/applied.txt","PASS\nDELIRIUM saved with elevated oven view, shimmering gradient demands, shortened PLACE THE CARD prompt and small star / bead surface glints restricted to spotlight pools. Void sky, card drift, safety colliders, fall respawn and ten-press ending preserved. Existing authored transforms preserved except the requested oven view marker.\n");
            if(File.Exists(Request))File.Delete(Request);
        }
        catch(Exception e){Debug.LogException(e);}
        finally
        {
            if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);applying=false;
        }
    }
    public static DeliriumSequence Configure(Scene scene)
    {
        if(scene.path!=ScenePath)throw new InvalidOperationException("DELIRIUM setup only supports its own scene.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses=all.Where(t=>!(t.name=="Locked oven view"&&t.parent&&t.parent.name=="DELIRIUM sequence"))
            .ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale,t.parent));
        var player=all.Select(t=>t.GetComponent<FirstPersonController>()).Single(p=>p);
        var interactor=player.GetComponent<PlayerInteractor>();var camera=player.GetComponentInChildren<Camera>();
        var oven=all.Single(t=>t.name=="Oven");var button=all.Single(t=>t.name=="OvenAlarm").GetComponent<PressButton>();
        var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="DELIRIUM sequence");
        if(!root){root=new GameObject("DELIRIUM sequence");SceneManager.MoveGameObjectToScene(root,scene);}
        var director=root.GetComponent<DeliriumSequence>();if(!director)director=root.AddComponent<DeliriumSequence>();
        if(!AssetDatabase.IsValidFolder(Folder))
        {
            if(Directory.Exists(Folder))AssetDatabase.ImportAsset(Folder,ImportAssetOptions.ForceSynchronousImport);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Materials","Delirium");
        }
        var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Void sky.mat");
        if(!sky){sky=new Material(Shader.Find("Old Tech/Void Sky"));AssetDatabase.CreateAsset(sky,Folder+"/Void sky.mat");}
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.07f,.065f);
        RenderSettings.fog=false;
        camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=Color.black;camera.nearClipPlane=.03f;
        var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=true;
        var cardTransforms=all.Where(t=>Regex.IsMatch(t.name,@"^Normal_punchcard(?: \(\d+\))?$",RegexOptions.IgnoreCase)).ToArray();
        int Index(Transform t){var m=Regex.Match(t.name,@"\((\d+)\)");return m.Success?int.Parse(m.Groups[1].Value):0;}
        var road=cardTransforms.Where(t=>Index(t)<=2).OrderBy(Index).ToArray();
        if(road.Length!=3)throw new InvalidOperationException("Expected the three fixed punch card road pieces 0, 1 and 2.");
        foreach(var card in cardTransforms)
        {
            int index=Index(card);
            if(index>2)
            {
                var drift=card.GetComponent<DeliriumCardDrift>();if(!drift)drift=card.gameObject.AddComponent<DeliriumCardDrift>();
                var data=new SerializedObject(drift);data.FindProperty("gentle").boolValue=index==3;
                data.FindProperty("phase").floatValue=index*1.371f;data.FindProperty("speed").floatValue=.7f+(index%5)*.14f;data.ApplyModifiedPropertiesWithoutUndo();
                if(index>3)foreach(var collider in card.GetComponents<Collider>())collider.enabled=false;
                var renderer=card.GetComponent<Renderer>();
                var floating=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Floating punchcards.mat");
                if(!floating){floating=new Material(renderer.sharedMaterial);AssetDatabase.CreateAsset(floating,Folder+"/Floating punchcards.mat");}
                floating.SetFloat("_ShadowFloor",.13f);floating.SetFloat("_AmbientStrength",.6f);
                floating.SetTexture("_EmissionMap",floating.GetTexture("_BaseMap"));
                floating.SetColor("_EmissionColor",new Color(.035f,.03f,.025f));
                renderer.sharedMaterial=floating;EditorUtility.SetDirty(floating);
            }
            else
            {
                var renderer=card.GetComponent<MeshRenderer>();var source=renderer.sharedMaterial;
                string path=Folder+"/Glossy punchcard road.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(Shader.Find("Old Tech/Retro Cel Glossy"));material.CopyPropertiesFromMaterial(source);AssetDatabase.CreateAsset(material,path);}
                material.shader=Shader.Find("Old Tech/Retro Cel Glossy");
                material.SetFloat("_ShadeSteps",4);material.SetFloat("_AmbientStrength",.28f);material.SetFloat("_ShadowFloor",.035f);
                material.SetFloat("_BandDither",.32f);material.SetColor("_ShadowTint",new Color(.47f,.32f,.34f));
                material.SetFloat("_Metallic",.28f);material.SetFloat("_Smoothness",.86f);material.SetFloat("_Cull",0);
                material.SetFloat("_SpecularHighlights",1);material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.enableInstancing=true;renderer.sharedMaterial=material;EditorUtility.SetDirty(material);
                foreach(var collider in card.GetComponents<Collider>()){collider.enabled=true;collider.isTrigger=false;}
            }
        }
        foreach(var old in oven.GetComponentsInChildren<SortBin>(true))old.enabled=false;
        foreach(var old in oven.GetComponentsInChildren<ItemSlot>(true))old.enabled=false;
        foreach(var old in oven.GetComponents<OvenIncineration>())old.enabled=false;
        foreach(var old in oven.GetComponents<Animator>())old.enabled=false;
        foreach(var old in player.GetComponents<PlayerFaint>())old.enabled=false;
        foreach(var particles in oven.GetComponentsInChildren<ParticleSystem>(true)){particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=particles.main;main.playOnAwake=false;}
        var top=oven.Find("OvenToothTop").GetComponent<Renderer>().bounds;
        var bottom=oven.Find("OvenToothBottom").GetComponent<Renderer>().bounds;
        Vector3 outward=Vector3.ProjectOnPlane(oven.up,Vector3.up).normalized;
        Vector3 mouth=(top.center+bottom.center)*.5f+outward*.22f;mouth.y=(top.min.y+bottom.max.y)*.5f;
        var rest=Marker(root.transform,"Card between teeth",mouth,Quaternion.LookRotation(-outward,Vector3.up));
        Vector3 aim=Vector3.Lerp(mouth,button.GetComponent<Renderer>().bounds.center,.55f);
        Vector3 eye=aim+outward*2.55f+Vector3.up*2.15f+Vector3.Cross(Vector3.up,outward)*.25f;
        var focus=Marker(root.transform,"Locked oven view",eye,Quaternion.LookRotation(aim-eye,Vector3.up));
        var spawn=root.transform.Find("Return to beginning");
        if(!spawn)spawn=Marker(root.transform,"Return to beginning",player.transform.position,player.transform.rotation);
        var offering=root.transform.Find("Delirium offering")?.GetComponent<Pickupable>();
        if(!offering)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Normal_punchcard.prefab");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Delirium offering";instance.transform.SetParent(root.transform,true);
            instance.transform.position=camera.transform.position;offering=instance.GetComponent<Pickupable>();
        }
        var heldSettings=new SerializedObject(offering);heldSettings.FindProperty("holdPositionOffset").vector3Value=new Vector3(.02f,.02f,.22f);
        heldSettings.FindProperty("holdRotationOffset").vector3Value=new Vector3(-65,0,12);heldSettings.ApplyModifiedPropertiesWithoutUndo();
        var cardRenderer=offering.GetComponent<Renderer>();
        var cardMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Offering card.mat");
        if(!cardMaterial){cardMaterial=new Material(cardRenderer.sharedMaterial);AssetDatabase.CreateAsset(cardMaterial,Folder+"/Offering card.mat");}
        cardMaterial.SetFloat("_Cull",0);cardMaterial.SetFloat("_ShadowFloor",.14f);
        cardMaterial.SetTexture("_EmissionMap",cardMaterial.GetTexture("_BaseMap"));
        cardMaterial.SetColor("_EmissionColor",new Color(.16f,.14f,.1f));
        cardRenderer.sharedMaterial=cardMaterial;EditorUtility.SetDirty(cardMaterial);
        var input=new SerializedObject(interactor);input.FindProperty("controller").objectReferenceValue=player;input.ApplyModifiedPropertiesWithoutUndo();
        var settings=new SerializedObject(director);
        void Ref(string name,UnityEngine.Object value)=>settings.FindProperty(name).objectReferenceValue=value;
        Ref("interactor",interactor);Ref("controller",player);Ref("view",camera);Ref("oven",oven);Ref("cardRest",rest);Ref("focusPose",focus);Ref("respawnPose",spawn);
        Ref("offering",offering);Ref("button",button);Ref("buttonRenderer",button.GetComponent<Renderer>());
        Ref("font",AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"));
        settings.FindProperty("fallLimit").floatValue=road.Min(t=>t.GetComponent<Renderer>().bounds.min.y)-5;
        settings.ApplyModifiedPropertiesWithoutUndo();
        for(int i=button.onPressed.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onPressed,i);
        UnityEventTools.AddPersistentListener(button.onPressed,director.RegisterPress);
        var press=new SerializedObject(button);press.FindProperty("buttonCap").objectReferenceValue=button.transform;
        press.FindProperty("pressOffset").vector3Value=button.transform.parent.InverseTransformVector(button.transform.forward*.035f);
        press.FindProperty("pressTime").floatValue=.075f;press.FindProperty("cooldown").floatValue=.35f;press.ApplyModifiedPropertiesWithoutUndo();
        BuildSafety(root.transform,road,spawn,player.GetComponent<CharacterController>());
        BuildGlints(root.transform,road,all.Select(t=>t.GetComponent<Light>()).Where(l=>l&&l.type==LightType.Spot&&l.enabled).ToArray());
        foreach(var pose in poses)
            if(pose.Key.localPosition!=pose.Value.Item1||pose.Key.localRotation!=pose.Value.Item2||pose.Key.localScale!=pose.Value.Item3||pose.Key.parent!=pose.Value.Item4)
                throw new InvalidOperationException("Existing authored transform changed: "+pose.Key.name);
        AssetDatabase.SaveAssets();return director;
    }
    private static Transform Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
    {
        var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);}
        child.SetPositionAndRotation(position,rotation);return child;
    }
    private static void BuildSafety(Transform parent,Transform[] road,Transform spawn,CharacterController character)
    {
        var group=parent.Find("Road safety colliders");if(!group){group=new GameObject("Road safety colliders").transform;group.SetParent(parent,false);}
        Vector3 Surface(Transform t)
        {
            var mesh=t.GetComponent<MeshFilter>().sharedMesh.bounds;Vector3 normal=t.up.y>=0?t.up:-t.up;
            return t.TransformPoint(mesh.center)+normal*(mesh.extents.y*t.lossyScale.y);
        }
        Vector3 direction=Vector3.ProjectOnPlane(Surface(road[1])-Surface(road[0]),Vector3.up).normalized;
        float feet=spawn.position.y+character.center.y-character.height*.5f;
        Vector3[] points={spawn.position-direction*1.5f,Surface(road[0]),Surface(road[1]),Surface(road[2])+direction*1.8f};
        points[0].y=feet-.02f;
        const float width=3.4f;
        Vector3[] left=new Vector3[points.Length],right=new Vector3[points.Length];
        for(int i=0;i<points.Length;i++)
        {
            Vector3 before=i>0?Vector3.ProjectOnPlane(points[i]-points[i-1],Vector3.up).normalized:direction;
            Vector3 after=i<points.Length-1?Vector3.ProjectOnPlane(points[i+1]-points[i],Vector3.up).normalized:before;
            Vector3 side=Vector3.Cross(Vector3.up,(before+after).normalized).normalized;
            left[i]=points[i]-side*width*.5f;right[i]=points[i]+side*width*.5f;
        }
        for(int i=0;i<points.Length-1;i++)
        {
            Box(group,"Seam floor "+i,points[i],points[i+1],width,.12f,-.14f);
            Box(group,"Left guard "+i,left[i],left[i+1],.16f,2.5f,1.1f);
            Box(group,"Right guard "+i,right[i],right[i+1],.16f,2.5f,1.1f);
        }
        Box(group,"Start guard",left[0],right[0],.16f,2.5f,1.1f);
        Box(group,"End guard",left[left.Length-1],right[right.Length-1],.16f,2.5f,1.1f);
    }
    private static void Box(Transform parent,string name,Vector3 a,Vector3 b,float width,float height,float raise)
    {
        var t=Marker(parent,name,(a+b)*.5f+Vector3.up*raise,Quaternion.LookRotation((b-a).normalized,Vector3.up));
        var collider=t.GetComponent<BoxCollider>();if(!collider)collider=t.gameObject.AddComponent<BoxCollider>();
        collider.size=new Vector3(width,height,Vector3.Distance(a,b)+.25f);collider.isTrigger=false;
    }
    private static void BuildGlints(Transform parent,Transform[] road,Light[] spotlights)
    {
        var group=parent.Find("Spotlight road glints");
        if(!group){group=new GameObject("Spotlight road glints").transform;group.SetParent(parent,false);}
        var glints=group.GetComponent<DeliriumRoadGlints>();if(!glints)glints=group.gameObject.AddComponent<DeliriumRoadGlints>();
        var colliders=road.Select(t=>t.GetComponent<Collider>()).ToArray();Physics.SyncTransforms();
        var random=new System.Random(60817);
        Vector3[] Points(int perLight)
        {
            var result=new List<Vector3>();
            foreach(var light in spotlights)
            {
                int found=0;
                for(int attempt=0;attempt<perLight*30&&found<perLight;attempt++)
                {
                    float radius=Mathf.Sqrt((float)random.NextDouble()),angle=(float)random.NextDouble()*Mathf.PI*2;
                    float cone=Mathf.Tan(light.spotAngle*.5f*Mathf.Deg2Rad)*.68f;
                    var direction=light.transform.TransformDirection(new Vector3(Mathf.Cos(angle)*radius*cone,Mathf.Sin(angle)*radius*cone,1).normalized);
                    var ray=new Ray(light.transform.position,direction);float distance=float.PositiveInfinity;RaycastHit closest=default;
                    foreach(var collider in colliders)
                        if(collider.Raycast(ray,out var hit,light.range)&&hit.distance<distance&&hit.normal.y>.55f){closest=hit;distance=hit.distance;}
                    if(float.IsPositiveInfinity(distance))continue;
                    result.Add(closest.point+closest.normal*.025f);found++;
                }
            }
            return result.ToArray();
        }
        var stars=Points(14);var beads=Points(20);
        ParticleSystem Emitter(string name,Vector3[] points,bool round)
        {
            var t=group.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(group,false);}
            var system=t.GetComponent<ParticleSystem>();if(!system)system=t.gameObject.AddComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startSpeed=0;main.gravityModifier=0;main.maxParticles=Mathf.Max(1,points.Length);main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.alignment=ParticleSystemRenderSpace.View;renderer.minParticleSize=0;renderer.maxParticleSize=.015f;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            string path=Folder+(round?"/Bead glints.mat":"/Star glints.mat");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Old Tech/Delirium Road Glints"));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_Bead",round?1:0);material.SetColor("_Tint",new Color(4,3.6f,2.8f,1));
            renderer.sharedMaterial=material;EditorUtility.SetDirty(material);return system;
        }
        var data=new SerializedObject(glints);
        data.FindProperty("stars").objectReferenceValue=Emitter("Star glints",stars,false);
        data.FindProperty("beads").objectReferenceValue=Emitter("Bead glints",beads,true);
        void Store(string name,Vector3[] points)
        {
            var array=data.FindProperty(name);array.arraySize=points.Length;
            for(int i=0;i<points.Length;i++)array.GetArrayElementAtIndex(i).vector3Value=points[i];
        }
        Store("starPositions",stars);Store("beadPositions",beads);data.ApplyModifiedPropertiesWithoutUndo();
    }
}
