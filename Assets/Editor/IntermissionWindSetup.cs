using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class IntermissionWindSetup
{
    public const string ScenePath = "Assets/Scenes/Intermission.unity";
    private const string Request = "Temp/IntermissionWind/apply-v1.request";
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
        try { Apply(scene); File.Delete(Request); }
        catch (Exception e) { Debug.LogException(e); }
        finally { applying = false; }
    }
    [MenuItem("Tools/Punch Card/Set Up First Intermission PSX Wind")]
    public static void ApplyOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) Apply(scene);
    }
    private static void Apply(Scene scene)
    {
        Directory.CreateDirectory("Temp/IntermissionWind");
        EditorSceneManager.SaveScene(scene, "Temp/IntermissionWind/Intermission-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene); Validate(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/IntermissionWind/applied.txt", "PASS\nFirst Intermission saved with world-space PSX dust, folded scraps, gusts and dithered cigarette smoke. Intermission 2 is untouched.\n");
        SceneView.RepaintAll();
    }

    public static IntermissionWind Configure(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Wind setup is only for the first Intermission.");
        var cinematic = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<IntermissionCinematic>(true)).Single();
        var settings = new SerializedObject(cinematic);
        var opening = (Transform)settings.FindProperty("openingPose").objectReferenceValue;
        var smoke = (ParticleSystem)settings.FindProperty("cigaretteSmoke").objectReferenceValue;
        Vector3 outside = Vector3.ProjectOnPlane(opening.forward, Vector3.up).normalized;
        Vector3 wind = (opening.right + outside * .18f).normalized;
        var root = Child(cinematic.transform, "Intermission PSX wind");
        root.SetPositionAndRotation(opening.position + outside * 3.8f + Vector3.up * .4f, Quaternion.identity);
        root.localScale = Vector3.one;
        var shader = Shader.Find("Old Tech/PSX Wind");
        if (!shader) throw new InvalidOperationException("PSX Wind shader is missing.");
        var gritMaterial = Material("Assets/Materials/Intermission Wind Dust.mat", shader, 0);
        var paperMaterial = Material("Assets/Materials/Intermission Wind Paper.mat", shader, 1);
        var smokeMaterial = Material("Assets/Materials/Intermission PSX Smoke.mat", shader, 2);
        var mesh = PaperMesh();
        var dust = Particles(root, "Pixel grit", gritMaterial, null);
        var paper = Particles(root, "Tumbling paper scraps", paperMaterial, mesh);
        smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
        var noise = smoke.noise; noise.quality = ParticleSystemNoiseQuality.Low;
        var component = root.GetComponent<IntermissionWind>();
        if (!component) component = root.gameObject.AddComponent<IntermissionWind>();
        var serialized = new SerializedObject(component);
        serialized.FindProperty("cinematic").objectReferenceValue = cinematic;
        serialized.FindProperty("overlook").objectReferenceValue = opening;
        serialized.FindProperty("dust").objectReferenceValue = dust;
        serialized.FindProperty("paper").objectReferenceValue = paper;
        serialized.FindProperty("cigaretteSmoke").objectReferenceValue = smoke;
        serialized.FindProperty("direction").vector3Value = wind;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return component;
    }

    private static ParticleSystem Particles(Transform parent, string name, Material material, Mesh mesh)
    {
        bool paper = mesh;
        var child = Child(parent, name);
        child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
        var particles = child.GetComponent<ParticleSystem>();
        if (!particles) particles = child.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false; particles.randomSeed = paper ? 347u : 719u;
        var main = particles.main;
        main.loop = true; main.playOnAwake = false; main.duration = 8; main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = paper ? 10 : 90;
        main.startLifetime = new ParticleSystem.MinMaxCurve(paper ? 5 : 3.5f, paper ? 8 : 6);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(paper ? .12f : .018f, paper ? .22f : .05f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.32f,.33f,.31f,.85f),new Color(.62f,.61f,.53f,.95f));
        main.startRotation3D = paper;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
        var emission = particles.emission; emission.enabled = true; emission.rateOverTime = paper ? .45f : 7;
        var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(paper ? 8 : 10, paper ? 2.6f : 4, 14);
        var rotation = particles.rotationOverLifetime; rotation.enabled = paper; rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-1.1f,1.4f);
        rotation.y = new ParticleSystem.MinMaxCurve(-1.4f,1.8f);
        rotation.z = new ParticleSystem.MinMaxCurve(-2,2.3f);
        var noise = particles.noise; noise.enabled = true; noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = paper ? .15f : .07f; noise.frequency = .35f; noise.scrollSpeed = .3f;
        var color = particles.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(1,.75f),new GradientAlphaKey(0,1) });
        color.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = paper ? ParticleSystemRenderMode.Mesh : ParticleSystemRenderMode.Stretch;
        if (paper) renderer.mesh = mesh;
        else { renderer.lengthScale = 1; renderer.velocityScale = .035f; renderer.cameraVelocityScale = 0; }
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.maxParticleSize = .035f;
        return particles;
    }

    private static Mesh PaperMesh()
    {
        const string path = "Assets/Meshes/Intermission Wind Paper.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh) return mesh;
        mesh = new Mesh { name = "Folded low poly paper scrap" };
        mesh.vertices = new[] { new Vector3(-.5f,-.3f,0),new Vector3(0,-.3f,.16f),new Vector3(.5f,-.3f,0),new Vector3(-.5f,.3f,0),new Vector3(0,.3f,.16f),new Vector3(.5f,.3f,0) };
        mesh.uv = new[] { new Vector2(0,0),new Vector2(.5f,0),new Vector2(1,0),new Vector2(0,1),new Vector2(.5f,1),new Vector2(1,1) };
        mesh.triangles = new[] { 0,3,1,1,3,4,1,4,2,2,4,5 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }
    private static Material Material(string path, Shader shader, float kind)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
        material.shader = shader; material.SetFloat("_Kind",kind); EditorUtility.SetDirty(material);
        return material;
    }
    private static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (!child) { child = new GameObject(name).transform; child.SetParent(parent,false); }
        return child;
    }
    public static void Validate(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Wrong Intermission scene.");
        var wind = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<IntermissionWind>(true)).Single();
        var settings = new SerializedObject(wind);
        foreach (var name in new[] { "cinematic","overlook","dust","paper","cigaretteSmoke" })
            if (!settings.FindProperty(name).objectReferenceValue) throw new InvalidOperationException("Missing wind reference: " + name);
        foreach (var particles in wind.GetComponentsInChildren<ParticleSystem>())
            if (particles.main.simulationSpace != ParticleSystemSimulationSpace.World) throw new InvalidOperationException("Wind debris must stay in world space.");
    }
}
