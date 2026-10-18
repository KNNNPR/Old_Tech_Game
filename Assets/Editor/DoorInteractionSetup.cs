using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DoorInteractionSetup
{
    private const string ScenePath="Assets/Scenes/scene_1.unity";
    private const string Request="Temp/Door/apply.request";

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
        try{Apply();File.Delete(Request);}catch(Exception e){Debug.LogException(e);}
    }
    [MenuItem("Tools/Punch Card/Set Up Door_1 Interaction")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded)return;
        Directory.CreateDirectory("Temp/Door");
        EditorSceneManager.SaveScene(scene,"Temp/Door/scene_1-before-door-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Door/applied.txt","Door_1 opens and closes with the interaction key using door.anim.\n");
    }

    public static AnimatedDoor Configure(Scene scene)
    {
        var door=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Door_1");
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/door.anim");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animation/Door_1.controller");
        if(!clip || !controller)throw new InvalidOperationException("Door animation assets are missing.");
        var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);
        var parameter=controller.parameters.FirstOrDefault(p=>p.name=="DoorProgress");
        if(parameter==null)controller.AddParameter("DoorProgress",AnimatorControllerParameterType.Float);
        else if(parameter.type!=AnimatorControllerParameterType.Float)throw new InvalidOperationException("DoorProgress must be a float.");
        var layers=controller.layers;
        var state=layers[0].stateMachine.states.Select(s=>s.state).Single(s=>s.motion==clip);
        layers[0].stateMachine.defaultState=state;layers[0].defaultWeight=1;controller.layers=layers;
        state.speed=0;state.timeParameter="DoorProgress";state.timeParameterActive=true;
        EditorUtility.SetDirty(clip);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(state);EditorUtility.SetDirty(layers[0].stateMachine);
        AssetDatabase.SaveAssetIfDirty(clip);AssetDatabase.SaveAssetIfDirty(controller);
        var animator=door.GetComponent<Animator>();if(!animator)animator=door.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var interaction=door.GetComponent<AnimatedDoor>();if(!interaction)interaction=door.gameObject.AddComponent<AnimatedDoor>();
        var serialized=new SerializedObject(interaction);
        serialized.FindProperty("motion").objectReferenceValue=clip;
        serialized.FindProperty("stateName").stringValue="Base Layer."+state.name;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if(!door.GetComponent<Collider>())throw new InvalidOperationException("Door_1 needs its collider for interaction.");
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        return interaction;
    }
}
