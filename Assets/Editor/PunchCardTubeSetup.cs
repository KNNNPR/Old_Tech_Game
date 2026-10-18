using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PunchCardTubeSetup
{
    private const string ScenePath="Assets/Scenes/scene_1.unity";
    private const string Request="Temp/PunchTube/apply.request";
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall+=ApplyRequested;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)ApplyRequested();};
        EditorSceneManager.sceneOpened+=(_,__)=>ApplyRequested();
    }
    private static void ApplyRequested()
    {
        if(!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded)return;
        try{Apply();File.Delete(Request);}
        catch(Exception e){Debug.LogException(e);}
    }
    [MenuItem("Tools/Punch Card/Set Up Card Tube in scene_1")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded)return;
        Directory.CreateDirectory("Temp/PunchTube");
        EditorSceneManager.SaveScene(scene,"Temp/PunchTube/scene_1-before-tube-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PunchTube/applied.txt","Normal_punchcard prefab routed through PunchCardTube into scene_1.\n");
    }
    public static PunchCardTubeSpawner Configure(Scene scene)
    {
        var tube=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Single(t=>t.name=="PunchCardTube");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Normal_punchcard.prefab");
        if(!prefab || !prefab.GetComponent<Pickupable>() || !prefab.GetComponent<Rigidbody>())throw new InvalidOperationException("Normal_punchcard prefab is missing its pickup components.");
        var spawner=tube.GetComponentInChildren<PunchCardTubeSpawner>(true);
        if(!spawner)
        {
            var root=new GameObject("PunchCardTube Spawner");root.transform.SetParent(tube.transform,false);
            spawner=root.AddComponent<PunchCardTubeSpawner>();
        }
        // Points follow the vertical shaft and the centre of the lower elbow, then clear its solid box collider.
        Vector3[] points=
        {
            new Vector3(13.9961f,12.0f,12.01111f),new Vector3(13.9961f,5.55f,12.01111f),
            new Vector3(13.1961f,4.75025f,12.01111f),new Vector3(12.13f,4.75025f,12.01111f)
        };
        string[] names={"Spawn Inside Tube","Bend Entry","Bend Exit","Release Over Floor"};
        string[] properties={"spawnPoint","bendStart","bendEnd","releasePoint"};
        var settings=new SerializedObject(spawner);
        var prefabs=settings.FindProperty("cardPrefabs");
        if(prefabs.arraySize==0) { prefabs.arraySize=1;prefabs.GetArrayElementAtIndex(0).objectReferenceValue=prefab; }
        for(int i=0;i<points.Length;i++)
        {
            var point=spawner.transform.Find(names[i]);
            if(!point){point=new GameObject(names[i]).transform;point.SetParent(spawner.transform,false);point.position=points[i];}
            settings.FindProperty(properties[i]).objectReferenceValue=point;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }
}
