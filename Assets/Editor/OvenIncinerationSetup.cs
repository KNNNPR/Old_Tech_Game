using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class OvenIncinerationSetup
{
    public const string ScenePath = "Assets/Scenes/scene_1_Kirill.unity";
    private const string Request = "Temp/Oven/apply-v1.request";
    private static bool applying;
    private static double nextCheck;

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.update += () =>
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            ApplyRequested();
        };
    }

    private static void ApplyRequested()
    {
        if (applying || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        applying = true;
        try
        {
            Apply(scene);
            File.Delete(Request);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { applying = false; }
    }

    [MenuItem("Tools/Punch Card/Set Up Kirill Oven Fire")]
    public static void ApplyOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) Apply(scene);
    }

    private static void Apply(Scene scene)
    {
        Directory.CreateDirectory("Temp/Oven");
        EditorSceneManager.SaveScene(scene, "Temp/Oven/scene_1_Kirill-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene);
        Validate(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Oven/applied.txt", "PASS\nscene_1_Kirill saved: card-only incinerate animation, mouth flames / sparks / flickering light. Empty and repeated presses do not restart it.\n");
        SceneView.RepaintAll();
    }

    public static OvenIncineration Configure(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var oven = all.Single(t => t.name.Equals("Oven", StringComparison.OrdinalIgnoreCase));
        var bin = oven.GetComponentInChildren<SortBin>(true);
        if (!bin) throw new InvalidOperationException("Oven SortBin is missing.");
        var slot = bin.GetComponent<ItemSlot>();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/incinerate.anim");
        if (!clip) throw new InvalidOperationException("incinerate.anim is missing.");
        var parts = new[] { oven.Find("OvenToothTop"), oven.Find("OvenToothBottom") };
        if (parts.Any(p => !p)) throw new InvalidOperationException("The clip's two oven teeth are missing.");
        var top = parts[0].GetComponent<Renderer>().bounds;
        var bottom = parts[1].GetComponent<Renderer>().bounds;
        Vector3 outward = Vector3.ProjectOnPlane(slot.transform.forward, Vector3.up).normalized;
        Vector3 mouth = (top.center + bottom.center) * .5f;
        mouth += outward * .18f;
        float width = Mathf.Min(top.size.x, bottom.size.x) * .86f;

        var root = Child(oven, "Incineration effects");
        root.SetPositionAndRotation(mouth, Quaternion.LookRotation(outward, Vector3.up));
        var scale = oven.lossyScale;
        root.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        var shader = Shader.Find("OldTech/Oven Fire");
        if (!shader) throw new InvalidOperationException("Oven Fire shader is missing.");
        const string materialPath = "Assets/Materials/Oven Fire.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        material.shader = shader;
        EditorUtility.SetDirty(material);
        var flames = Particles(root, "Mouth flames", material, width, false, outward);
        var sparks = Particles(root, "Flying embers", material, width, true, outward);
        var lightTransform = Child(root, "Fire light");
        lightTransform.localPosition = Vector3.forward * .4f;
        var light = lightTransform.GetComponent<Light>();
        if (!light) light = lightTransform.gameObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1, .27f, .035f);
        light.range = 4.5f; light.intensity = 0; light.shadows = LightShadows.None; light.enabled = false;
        var animator = oven.GetComponent<Animator>();
        if (animator) animator.enabled = false;
        var effect = oven.GetComponent<OvenIncineration>();
        if (!effect) effect = oven.gameObject.AddComponent<OvenIncineration>();
        var settings = new SerializedObject(effect);
        settings.FindProperty("bin").objectReferenceValue = bin;
        settings.FindProperty("incinerate").objectReferenceValue = clip;
        settings.FindProperty("ovenAnimator").objectReferenceValue = animator;
        settings.FindProperty("flames").objectReferenceValue = flames;
        settings.FindProperty("sparks").objectReferenceValue = sparks;
        settings.FindProperty("fireLight").objectReferenceValue = light;
        var animated = settings.FindProperty("animatedParts"); animated.arraySize = parts.Length;
        for (int i = 0; i < parts.Length; i++) animated.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        var binSettings = new SerializedObject(bin);
        var duration = binSettings.FindProperty("suckDuration");
        duration.floatValue = Mathf.Max(duration.floatValue, clip.length);
        binSettings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return effect;
    }

    private static ParticleSystem Particles(Transform parent, string name, Material material, float width, bool sparks, Vector3 outward)
    {
        var t = Child(parent, name);
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        var particles = t.GetComponent<ParticleSystem>();
        if (!particles) particles = t.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false; main.loop = true; main.duration = 1.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = sparks ? 40 : 150;
        main.startLifetime = new ParticleSystem.MinMaxCurve(sparks ? .25f : .35f, sparks ? .55f : .65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, sparks ? 1.5f : .7f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(sparks ? .018f : .22f, sparks ? .035f : .48f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(sparks ? .08f : .55f, sparks ? .18f : .95f);
        main.startSizeZ = 1;
        main.startColor = Color.white;
        main.gravityModifier = sparks ? .65f : 0;
        var emission = particles.emission; emission.enabled = true; emission.rateOverTime = sparks ? 18 : 105;
        var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, .12f, .08f);
        var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.16f, .16f);
        velocity.y = new ParticleSystem.MinMaxCurve(sparks ? 2.4f : .9f, sparks ? 3.5f : 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(Mathf.Min(outward.z*.7f,outward.z*1.2f),Mathf.Max(outward.z*.7f,outward.z*1.2f));
        var noise = particles.noise; noise.enabled = !sparks; noise.strength = .12f; noise.frequency = 2; noise.scrollSpeed = 1.5f;
        var color = particles.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(new Color(.8f,.25f,.08f),1) },
            new[] { new GradientAlphaKey(.4f,0),new GradientAlphaKey(.65f,.18f),new GradientAlphaKey(.32f,.65f),new GradientAlphaKey(0,1) });
        color.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return particles;
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        return child;
    }

    public static void Validate(Scene scene)
    {
        var effect = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OvenIncineration>(true)).Single();
        var settings = new SerializedObject(effect);
        foreach (var name in new[] { "bin", "incinerate", "flames", "sparks", "fireLight" })
            if (!settings.FindProperty(name).objectReferenceValue) throw new InvalidOperationException("Missing oven reference: " + name);
        var bin = (SortBin)settings.FindProperty("bin").objectReferenceValue;
        var buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PressButton>(true));
        if (!buttons.Any(b => Enumerable.Range(0,b.onPressed.GetPersistentEventCount()).Any(i => b.onPressed.GetPersistentTarget(i) == bin && b.onPressed.GetPersistentMethodName(i) == "Process")))
            throw new InvalidOperationException("The oven button is not connected to its SortBin.");
    }
}
