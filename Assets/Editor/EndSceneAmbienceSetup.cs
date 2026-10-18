using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class EndSceneAmbienceSetup
{
    private const string Folder = "Assets/Materials/TheEnd/";

    public static void Configure(Scene scene, MeshRenderer sea)
    {
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var viewer = objects.Single(t => t.GetComponent<Camera>() && t.CompareTag("MainCamera"));
        var screen = objects.Single(t => t.name == "Monitor" && t.GetComponent<MeshRenderer>()).GetComponent<MeshRenderer>();
        var cards = objects.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r && r.name.StartsWith("Normal_punchcard", StringComparison.Ordinal) && r.bounds.size.y > 20).OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
        if (cards.Length == 0) throw new InvalidOperationException("No giant punch cards in THE_END.");
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "THE_END Ambience");
        if (!root)
        {
            root = new GameObject("THE_END Ambience");
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        var vertices = sea.GetComponent<MeshFilter>().sharedMesh.vertices;
        var bounds = new Bounds(sea.transform.TransformPoint(vertices[0]), Vector3.zero);
        foreach (var vertex in vertices) bounds.Encapsulate(sea.transform.TransformPoint(vertex));
        float waterline = bounds.max.y;

        var mist = root.transform.Find("Low drifting blood mist");
        if (!mist)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Low drifting blood mist";
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            mist = cube.transform;
            mist.SetParent(root.transform, false);
        }
        mist.SetPositionAndRotation(new Vector3(bounds.center.x, waterline + 1.1f, bounds.center.z), Quaternion.identity);
        mist.localScale = new Vector3(bounds.size.x, 3f, bounds.size.z);
        var fogMaterial = MaterialAt("Drifting Blood Mist.mat", "Old Tech/Drifting Blood Mist");
        fogMaterial.SetColor("_FogColor", new Color(0.38f, 0.20f, 0.21f, 1));
        fogMaterial.SetFloat("_Density", 0.11f);
        fogMaterial.SetFloat("_Opacity", 0.3f);
        var mistRenderer = mist.GetComponent<MeshRenderer>();
        mistRenderer.sharedMaterial = fogMaterial;
        NoLighting(mistRenderer);
        viewer.GetComponent<Camera>().GetUniversalAdditionalCameraData().requiresDepthTexture = true;

        string screenPath = Folder + "Beacon CRT.mat";
        var screenMaterial = AssetDatabase.LoadAssetAtPath<Material>(screenPath);
        if (!screenMaterial)
        {
            screenMaterial = new Material(screen.sharedMaterial) { name = "Beacon CRT" };
            AssetDatabase.CreateAsset(screenMaterial, screenPath);
        }
        screenMaterial.SetColor("_Phosphor", new Color(0.35f, 6f, 1.3f, 1));
        screenMaterial.SetFloat("_Power", 1);
        Undo.RecordObject(screen, "THE_END green CRT beacon");
        screen.sharedMaterial = screenMaterial;
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        foreach (var text in objects.Where(t => t.name == "MonitorCanvas").SelectMany(t => t.GetComponentsInChildren<TMP_Text>(true)))
        {
            Undo.RecordObject(text, "THE_END green screen text");
            text.color = new Color(0.25f, 1f, 0.48f, 1);
        }

        Vector3 facing = Vector3.ProjectOnPlane(viewer.position - screen.bounds.center, Vector3.up).normalized;
        var lightObject = Child(root.transform, "Green screen spill");
        var light = lightObject.GetComponent<Light>();
        if (!light) light = lightObject.gameObject.AddComponent<Light>();
        lightObject.SetPositionAndRotation(screen.bounds.center + facing * 0.45f + Vector3.up * 0.08f, Quaternion.LookRotation(facing));
        light.type = LightType.Point;
        light.color = new Color(0.12f, 1f, 0.30f, 1);
        light.intensity = 3.2f;
        light.range = 7f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        var waterMaterial = sea.sharedMaterial;
        waterMaterial.SetVector("_BeaconPosition", screen.bounds.center);
        waterMaterial.SetVector("_BeaconForward", facing);
        waterMaterial.SetVector("_BeaconSize", new Vector4(0.9f, 0.7f, 0, 0));
        waterMaterial.SetColor("_BeaconColor", new Color(0.04f, 1.6f, 0.26f, 1));
        waterMaterial.SetFloat("_BeaconReflection", 1.1f);

        var ashFloor = Child(root.transform, "Ash waterline");
        ashFloor.SetPositionAndRotation(new Vector3(bounds.center.x, waterline, bounds.center.z), Quaternion.identity);
        var ashObject = Child(root.transform, "Falling ash");
        ashObject.SetPositionAndRotation(new Vector3(viewer.position.x, waterline + 7.8f, viewer.position.z), Quaternion.identity);
        var ash = ashObject.GetComponent<ParticleSystem>();
        if (!ash) ash = ashObject.gameObject.AddComponent<ParticleSystem>();
        ConfigureAsh(ash, ashFloor);

        var controller = root.GetComponent<EndSceneAmbience>();
        if (!controller) controller = root.AddComponent<EndSceneAmbience>();
        var settings = new SerializedObject(controller);
        var cardReferences = settings.FindProperty("giantCards");
        var pivots = settings.FindProperty("cardPivots");
        cardReferences.arraySize = pivots.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
        {
            cardReferences.GetArrayElementAtIndex(i).objectReferenceValue = cards[i].transform;
            pivots.GetArrayElementAtIndex(i).vector3Value = new Vector3(cards[i].bounds.center.x, waterline, cards[i].bounds.center.z);
        }
        settings.FindProperty("screenLight").objectReferenceValue = light;
        settings.FindProperty("screen").objectReferenceValue = screen;
        settings.FindProperty("sea").objectReferenceValue = sea;
        settings.FindProperty("ash").objectReferenceValue = ash;
        settings.FindProperty("viewer").objectReferenceValue = viewer;
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var material in new[] { fogMaterial, screenMaterial, waterMaterial }) EditorUtility.SetDirty(material);
    }

    private static void ConfigureAsh(ParticleSystem ash, Transform waterline)
    {
        ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ash.useAutoRandomSeed = false;
        ash.randomSeed = 61060;
        var main = ash.main;
        main.loop = true; main.duration = 60; main.prewarm = true;
        main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.startLifetime = new ParticleSystem.MinMaxCurve(40, 60);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.095f);
        main.startColor = Color.white;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = ash.emission; emission.enabled = true; emission.rateOverTime = 3.6f;
        var shape = ash.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(60, 0.2f, 60);
        var velocity = ash.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.035f, 0.095f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.26f, -0.17f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        var rotation = ash.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        rotation.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.4f);
        rotation.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0.75f, 0.75f), new GradientAlphaKey(0, 1) });
        var color = ash.colorOverLifetime; color.enabled = true; color.color = gradient;
        var collision = ash.collision; collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes; collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.SetPlane(0, waterline); collision.lifetimeLoss = 1; collision.bounce = 0; collision.radiusScale = 0.1f;
        var renderer = ash.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = MaterialAt("Black Ash.mat", "Old Tech/Drifting Ash");
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.maxParticleSize = 0.035f;
        NoLighting(renderer);
    }

    public static void Validate(Scene scene)
    {
        var root = scene.GetRootGameObjects().Single(r => r.name == "THE_END Ambience");
        var motion = root.GetComponent<EndSceneAmbience>();
        var settings = new SerializedObject(motion);
        foreach (var name in new[] { "screenLight", "screen", "sea", "ash", "viewer" })
            if (!settings.FindProperty(name).objectReferenceValue) throw new InvalidOperationException("Missing THE_END ambience reference: " + name);
        var cards = settings.FindProperty("giantCards");
        if (cards.arraySize == 0 || cards.arraySize != settings.FindProperty("cardPivots").arraySize) throw new InvalidOperationException("Giant card sway is not connected.");
        for (int i = 0; i < cards.arraySize; i++) if (!cards.GetArrayElementAtIndex(i).objectReferenceValue) throw new InvalidOperationException("Missing giant card.");
        var ash = root.GetComponentInChildren<ParticleSystem>();
        if (!ash.main.prewarm || !ash.collision.enabled || !ash.collision.GetPlane(0)) throw new InvalidOperationException("Ash must prewarm and disappear at the waterline.");
        if (root.GetComponentsInChildren<Collider>().Length != 0) throw new InvalidOperationException("Atmosphere effects must not block the player.");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            if (!renderer.sharedMaterial || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new InvalidOperationException("Atmosphere material failed to compile.");
    }

    private static void NoLighting(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
    private static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        return child;
    }
    private static Material MaterialAt(string name, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader not compiled: " + shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + name);
        if (!material) { material = new Material(shader) { name = name.Replace(".mat", "") }; AssetDatabase.CreateAsset(material, Folder + name); }
        return material;
    }
}
