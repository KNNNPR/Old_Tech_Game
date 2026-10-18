using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class IntermissionCinematicSetup
{
    private const string ScenePath = "Assets/Scenes/Intermission.unity";
    private const string Request = "Temp/Intermission/apply.request";
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested(); };
        EditorSceneManager.sceneOpened += (_, __) => ApplyRequested();
    }
    private static void ApplyRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        try { Apply(); File.Delete(Request); }
        catch (Exception error) { Debug.LogException(error); }
    }
    [MenuItem("Tools/Punch Card/Set Up Intermission Cinematic")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Intermission");
        EditorSceneManager.SaveScene(scene, "Temp/Intermission/before-cinematic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Intermission/applied.txt", "Cigarette break, elevator monologue, ashtray placement, door approach and fade configured.\n");
    }

    public static IntermissionCinematic Configure(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Find(string name) => all.Single(t => t.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var ashtray = Find("ashtray").GetComponent<Renderer>();
        var elevator = Find("elevator").GetComponent<Renderer>();
        var originalCigarette = Find("cigarette").GetComponent<MeshRenderer>();
        var door = Find("Door").GetComponent<Renderer>();
        var camera = all.Select(t => t.GetComponent<Camera>()).Single(c => c && c.CompareTag("MainCamera"));
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Intermission Cinematic");
        if (!root) { root = new GameObject("Intermission Cinematic"); SceneManager.MoveGameObjectToScene(root, scene); }
        var director = root.GetComponent<IntermissionCinematic>();
        if (!director) director = root.AddComponent<IntermissionCinematic>();
        var a = ashtray.bounds.center;
        Vector3 openingPosition = a + new Vector3(-1.12f, 1.05f, 0);
        var opening = Marker(root.transform, "01 - City overlook", openingPosition, Quaternion.LookRotation(a + Vector3.up * .65f - openingPosition));
        var lift = Marker(root.transform, "02 - Closed elevator", new Vector3(elevator.transform.position.x, a.y + .5f, 2.9f), Quaternion.identity);
        var ash = Marker(root.transform, "03 - Ashtray focus", a + Vector3.up * .06f, Quaternion.identity);
        var rest = Marker(root.transform, "04 - Cigarette in ashtray", originalCigarette.bounds.center, Quaternion.identity);
        var doorAim = Marker(root.transform, "07 - Door focus", door.bounds.center, Quaternion.identity);
        var corner = Marker(root.transform, "05 - Walk corner", new Vector3(openingPosition.x - .75f, openingPosition.y, door.bounds.center.z), Quaternion.identity);
        var stop = Marker(root.transform, "06 - Door stopping point", new Vector3(door.bounds.max.x + .85f, openingPosition.y, door.bounds.center.z), Quaternion.identity);
        var doorLight = root.transform.Find("Door spill light");
        if (!doorLight) { doorLight = new GameObject("Door spill light", typeof(Light)).transform;doorLight.SetParent(root.transform, false); }
        doorLight.position = new Vector3(door.bounds.max.x + 1.2f, door.bounds.max.y - .35f, door.bounds.center.z);
        var spill = doorLight.GetComponent<Light>();
        spill.type = LightType.Point;spill.color = new Color(.64f, .73f, .67f);spill.intensity = 28;spill.range = 4.2f;spill.shadows = LightShadows.None;

        var cigarette = root.transform.Find("Cinematic Cigarette");
        if (!cigarette)
        {
            cigarette = new GameObject("Cinematic Cigarette").transform;
            cigarette.SetParent(root.transform, false);
            cigarette.SetPositionAndRotation(rest.position, rest.rotation);
            var body = new GameObject("Cigarette model", typeof(MeshFilter), typeof(MeshRenderer));
            body.GetComponent<MeshFilter>().sharedMesh = originalCigarette.GetComponent<MeshFilter>().sharedMesh;
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = originalCigarette.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            body.transform.SetPositionAndRotation(originalCigarette.transform.position, originalCigarette.transform.rotation);
            body.transform.localScale = originalCigarette.transform.lossyScale;
            body.transform.SetParent(cigarette, true);
        }
        originalCigarette.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(originalCigarette);

        var emberMaterial = Material("Assets/Materials/Intermission Ember.mat", "Universal Render Pipeline/Unlit");
        emberMaterial.SetColor("_BaseColor", new Color(2, .16f, .01f));
        EditorUtility.SetDirty(emberMaterial);AssetDatabase.SaveAssetIfDirty(emberMaterial);
        float halfLength = originalCigarette.bounds.extents.z;
        var tip = cigarette.Find("Ember");
        if (!tip)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Ember";UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            tip = sphere.transform;tip.SetParent(cigarette, false);
            tip.localPosition = new Vector3(0, 0, halfLength);
            tip.localScale = new Vector3(.019f, .019f, .005f);
        }
        var ember = tip.GetComponent<MeshRenderer>();
        ember.sharedMaterial = emberMaterial;ember.shadowCastingMode = ShadowCastingMode.Off;
        var lightTransform = cigarette.Find("Ember light");
        if (!lightTransform)
        {
            lightTransform = new GameObject("Ember light", typeof(Light)).transform;
            lightTransform.SetParent(cigarette, false);lightTransform.localPosition = tip.localPosition;
        }
        var light = lightTransform.GetComponent<Light>();
        light.type = LightType.Point;light.color = new Color(1, .2f, .04f);light.range = .3f;light.intensity = .08f;light.shadows = LightShadows.None;

        var smokeTransform = cigarette.Find("Cigarette smoke");
        if (!smokeTransform) { smokeTransform = new GameObject("Cigarette smoke", typeof(ParticleSystem)).transform;smokeTransform.SetParent(cigarette, false); }
        smokeTransform.localPosition = tip.localPosition + new Vector3(0, .008f, 0);
        smokeTransform.rotation = Quaternion.LookRotation(Vector3.up);
        var particles = smokeTransform.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;main.playOnAwake = false;main.simulationSpace = ParticleSystemSimulationSpace.World;main.useUnscaledTime = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.03f, .065f);
        main.startSize = new ParticleSystem.MinMaxCurve(.011f, .024f);
        main.startColor = new Color(.67f, .7f, .72f, .7f);main.maxParticles = 90;
        var emission = particles.emission;emission.enabled = true;emission.rateOverTime = 22;
        var shape = particles.shape;shape.enabled = true;shape.shapeType = ParticleSystemShapeType.Cone;shape.angle = 13;shape.radius = .004f;
        var velocity = particles.velocityOverLifetime;velocity.enabled = true;velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.012f, .014f);velocity.y = new ParticleSystem.MinMaxCurve(.04f, .075f);velocity.z = new ParticleSystem.MinMaxCurve(-.008f, .012f);
        var noise = particles.noise;noise.enabled = true;noise.strength = .014f;noise.frequency = .8f;noise.scrollSpeed = .25f;noise.quality = ParticleSystemNoiseQuality.Medium;
        var size = particles.sizeOverLifetime;size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .45f), new Keyframe(.3f, 1), new Keyframe(1, 1.7f)));
        var color = particles.colorOverLifetime;color.enabled = true;
        var gradient = new Gradient();gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.7f, .75f, .8f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.19f, .18f), new GradientAlphaKey(.09f, .65f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var smokeRenderer = particles.GetComponent<ParticleSystemRenderer>();
        smokeRenderer.sharedMaterial = Material("Assets/Materials/Intermission Smoke.mat", "Old Tech/Cigarette Smoke");
        smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard;smokeRenderer.sortMode = ParticleSystemSortMode.Distance;
        smokeRenderer.shadowCastingMode = ShadowCastingMode.Off;smokeRenderer.receiveShadows = false;

        var serialized = new SerializedObject(director);
        void Reference(string name, UnityEngine.Object value) => serialized.FindProperty(name).objectReferenceValue = value;
        Reference("playerCamera", camera);Reference("openingPose", opening);Reference("elevatorFocus", lift);Reference("ashtrayFocus", ash);
        Reference("cigaretteRest", rest);Reference("walkCorner", corner);Reference("doorStop", stop);Reference("doorFocus", doorAim);
        Reference("cigarette", cigarette);Reference("cigaretteSmoke", particles);Reference("ember", ember);Reference("emberLight", light);
        Reference("subtitleFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"));
        var hud = all.Select(t => t.GetComponent<InteractionHUD>()).FirstOrDefault(h => h);
        Reference("playerHUD", hud ? hud.GetComponent<Canvas>() : null);
        var suspended = all.SelectMany(t => t.GetComponents<Behaviour>()).Where(b => b is FirstPersonController || b is PlayerInteractor || b is PunchCardTubeSpawner || b is PhotoPrinterSpawner).ToArray();
        var behaviours = serialized.FindProperty("gameplayToSuspend");behaviours.arraySize = suspended.Length;
        for (int i = 0; i < suspended.Length; i++) behaviours.GetArrayElementAtIndex(i).objectReferenceValue = suspended[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        camera.transform.SetPositionAndRotation(opening.position, opening.rotation);camera.fieldOfView = 58;camera.nearClipPlane = .03f;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        return director;
    }
    private static Transform Marker(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var marker = parent.Find(name);
        if (!marker) { marker = new GameObject(name).transform;marker.SetParent(parent, false); }
        marker.SetPositionAndRotation(position, rotation);
        return marker;
    }
    private static Material Material(string path, string shaderName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        var shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException("Missing cinematic shader: " + shaderName);
        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
