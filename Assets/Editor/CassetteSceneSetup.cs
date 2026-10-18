using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CassetteSceneSetup
{
    private const string ScenePath = "Assets/Scenes/scene_1.unity";
    private const string PrefabPath = "Assets/Prefabs/Casette.prefab";
    // A versioned request cannot be consumed by a previously compiled setup script.
    private const string Request = "Temp/Cassette/inspection-v2.request";
    private static double nextRequestCheck;

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.update += PollRequests;
        EditorSceneManager.sceneOpened += (_, __) => ApplyRequested();
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested(); };
    }

    private static void PollRequests()
    {
        if (EditorApplication.timeSinceStartup < nextRequestCheck) return;
        nextRequestCheck = EditorApplication.timeSinceStartup + 1d;
        ApplyRequested();
    }

    private static void ApplyRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        try { Apply(); File.Delete(Request); }
        catch (Exception error) { Debug.LogException(error); }
    }

    [MenuItem("Tools/Punch Card/Set Up Cassette Tube and Recorder in scene_1")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Cassette");
        EditorSceneManager.SaveScene(scene, "Temp/Cassette/scene-before-cassette-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene);
        ValidateInspection(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Cassette/inspection-v2-applied.txt", "Recorder inspection v2 applied and validated in the loaded scene_1: camera rig, six outlined faders and all references connected.\n");
    }

    public static void ValidateInspection(Scene scene)
    {
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var recorder = transforms.Select(t => t.GetComponent<CassetteRecorder>()).Single(r => r);
        if (!recorder.HasInspectionSetup) throw new InvalidOperationException("Recorder inspection view or its references are missing in " + scene.path);
        var sliders = transforms.Select(t => t.GetComponent<RecorderSlider>()).Where(s => s).ToArray();
        if (sliders.Length != 6) throw new InvalidOperationException("Recorder needs six connected sliders.");
        foreach (var slider in sliders)
            if (!new SerializedObject(slider).FindProperty("outlineMaterial").objectReferenceValue)
                throw new InvalidOperationException("Missing recorder slider outline: " + slider.name);
    }

    public static void Configure(Scene scene)
    {
        ConfigurePrefab();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var computer = all.Single(t => t.name.Equals("MysticComputer", StringComparison.OrdinalIgnoreCase));
        var recorder = all.Single(t => t.name.Equals("Recorder", StringComparison.OrdinalIgnoreCase));

        var supply = computer.Find("Cassette Tube Supply");
        if (!supply) supply = WorldChild(computer, "Cassette Tube Supply", computer.position, Quaternion.identity);
        // Centre of the side tube's front lip, with the cassette protruding for pickup.
        var output = supply.Find("Cassette Output Pose");
        Vector3 direction = computer.TransformDirection(new Vector3(-.545f, -.838f, 0f)).normalized;
        if (!output) output = WorldChild(supply, "Cassette Output Pose",
            computer.TransformPoint(new Vector3(.0182964f, -.0064095f, .0047121f)), Quaternion.LookRotation(direction, Vector3.up));
        var start = supply.Find("Cassette Feed Start");
        if (!start) start = WorldChild(supply, "Cassette Feed Start", output.position - direction * .95f, output.rotation);
        var pickup = supply.Find("Cassette Pickup Target");
        if (!pickup) pickup = WorldChild(supply, "Cassette Pickup Target", output.position + direction * .32f, output.rotation);
        var pickupCollider = Ensure<BoxCollider>(pickup.gameObject);
        pickupCollider.size = new Vector3(.8f, .55f, .24f);
        pickupCollider.isTrigger = true;
        pickupCollider.enabled = false;
        var spawner = Ensure<CassetteTubeSpawner>(supply.gameObject);
        var sourceSettings = new SerializedObject(spawner);
        sourceSettings.FindProperty("cassettePrefab").objectReferenceValue = prefab;
        sourceSettings.FindProperty("feedStart").objectReferenceValue = start;
        sourceSettings.FindProperty("outputPose").objectReferenceValue = output;
        sourceSettings.FindProperty("pickupTarget").objectReferenceValue = pickupCollider;
        sourceSettings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);

        var anchor = recorder.Find("Cassette Anchor");
        if (!anchor)
        {
            // The prefab retains the original model's local pose in SCENE, as in the reference.
            var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            var marker = all.Where(t => t.GetComponent<MeshFilter>() && t.GetComponent<MeshFilter>().sharedMesh == mesh)
                .Where(t => Vector3.Distance(t.position, recorder.position) < 2f).SingleOrDefault();
            Vector3 position = marker ? marker.position : recorder.parent.TransformPoint(prefab.transform.localPosition);
            Quaternion rotation = marker ? marker.rotation : recorder.parent.rotation * prefab.transform.localRotation;
            anchor = WorldChild(recorder, "Cassette Anchor", position, rotation);
            if (marker) UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
        var slot = Ensure<CassetteRecorder>(recorder.gameObject);
        var slotSettings = new SerializedObject(slot);
        slotSettings.FindProperty("cassetteAnchor").objectReferenceValue = anchor;
        slotSettings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
        var hit = recorder.Find("Cassette Slot Interaction");
        if (!hit) hit = WorldChild(recorder, "Cassette Slot Interaction", anchor.position - recorder.up * .45f,
            Quaternion.LookRotation(-recorder.up, Vector3.up));
        var hitCollider = Ensure<BoxCollider>(hit.gameObject);
        hitCollider.size = new Vector3(.8f, .55f, .2f);
        hitCollider.isTrigger = true;
        ConfigurePlayback(recorder, slot, all);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigurePlayback(Transform recorder, CassetteRecorder transport, Transform[] all)
    {
        var button = all.Single(t => t.name.Equals("RecorderButton", StringComparison.OrdinalIgnoreCase));
        var big = all.Single(t => t.name.Equals("BigCoil", StringComparison.OrdinalIgnoreCase));
        var small = all.Single(t => t.name.Equals("SmallCoil", StringComparison.OrdinalIgnoreCase));
        Vector3 front = -recorder.up, railUp = recorder.forward;
        var bands = new RecorderSlider[6];
        for (int i = 0; i < bands.Length; i++)
        {
            var knob = all.Single(t => t.name.Equals("RecorderSlider" + (i + 1), StringComparison.OrdinalIgnoreCase));
            bands[i] = Ensure<RecorderSlider>(knob.gameObject);
            // The black texture tracks span y=6.035..6.39; inset the head centres at both ends.
            float currentHeight = Vector3.Dot(knob.position - recorder.position, railUp);
            Vector3 start = knob.parent.InverseTransformPoint(knob.position + railUp * (.15868f - currentHeight));
            Vector3 end = knob.parent.InverseTransformPoint(knob.position + railUp * (.47368f - currentHeight));
            float initial = Mathf.Clamp01(Vector3.Dot(knob.localPosition - start, end - start) / (end - start).sqrMagnitude);
            var settings = new SerializedObject(bands[i]);
            settings.FindProperty("recorder").objectReferenceValue = transport;
            settings.FindProperty("band").intValue = i;
            settings.FindProperty("bandLabel").stringValue = RecorderSlider.ControlLabel(i);
            settings.FindProperty("railStart").vector3Value = start;
            settings.FindProperty("railEnd").vector3Value = end;
            settings.FindProperty("initialValue").floatValue = initial;
            settings.FindProperty("outlineMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Pickup Outline.mat");
            settings.ApplyModifiedPropertiesWithoutUndo();
            var proxy = knob.Find("Fader Interaction");
            if (!proxy) proxy = WorldChild(knob, "Fader Interaction", knob.position + front * .43f, Quaternion.LookRotation(front, railUp));
            var collider = Ensure<BoxCollider>(proxy.gameObject);
            collider.isTrigger = true;
            collider.size = new Vector3(.12f, .075f, .16f);
        }
        var buttonAction = Ensure<RecorderPlayButton>(button.gameObject);
        var buttonSettings = new SerializedObject(buttonAction);
        buttonSettings.FindProperty("recorder").objectReferenceValue = transport;
        buttonSettings.ApplyModifiedPropertiesWithoutUndo();
        var buttonHit = button.Find("Transport Button Interaction");
        if (!buttonHit) buttonHit = WorldChild(button, "Transport Button Interaction", button.position + front * .43f, Quaternion.LookRotation(front, railUp));
        var buttonCollider = Ensure<BoxCollider>(buttonHit.gameObject);
        buttonCollider.isTrigger = true;
        buttonCollider.size = new Vector3(.13f, .11f, .16f);

        var carrier = AudioChild(recorder, "Tape Carrier Audio", true);
        var hidden = AudioChild(recorder, "Hidden Tape Audio", true);
        var click = AudioChild(recorder, "Recorder Transport Audio", false);
        var carrierProcessor = Ensure<RecorderSignalProcessor>(carrier.gameObject);
        var hiddenProcessor = Ensure<RecorderSignalProcessor>(hidden.gameObject);
        var data = new SerializedObject(transport);
        data.FindProperty("bigCoil").objectReferenceValue = big;
        data.FindProperty("smallCoil").objectReferenceValue = small;
        data.FindProperty("playButton").objectReferenceValue = button;
        data.FindProperty("buttonPressOffset").vector3Value = button.parent.InverseTransformVector(recorder.up * .018f);
        var array = data.FindProperty("sliders"); array.arraySize = 6;
        for (int i = 0; i < 6; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = bands[i];
        data.FindProperty("carrierSource").objectReferenceValue = carrier;
        data.FindProperty("hiddenSource").objectReferenceValue = hidden;
        data.FindProperty("buttonSource").objectReferenceValue = click;
        data.FindProperty("buttonClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/sfx/playaudiocard.wav");
        data.FindProperty("carrierProcessor").objectReferenceValue = carrierProcessor;
        data.FindProperty("hiddenProcessor").objectReferenceValue = hiddenProcessor;
        var inspection = Ensure<RecorderInspectionView>(recorder.gameObject);
        var pose = recorder.Find("Recorder Inspection Camera");
        if (!pose)
        {
            Vector3 lookAt = recorder.position - recorder.right * .5f + railUp * .12f + front * .25f;
            Vector3 position = lookAt + front * 1.8f + railUp * .18f;
            pose = WorldChild(recorder, "Recorder Inspection Camera", position, Quaternion.LookRotation(lookAt - position, railUp));
        }
        var viewData = new SerializedObject(inspection);
        viewData.FindProperty("recorder").objectReferenceValue = transport;
        viewData.FindProperty("cameraPose").objectReferenceValue = pose;
        viewData.FindProperty("playButton").objectReferenceValue = buttonAction;
        var viewSliders = viewData.FindProperty("sliders"); viewSliders.arraySize = 6;
        for (int i = 0; i < bands.Length; i++) viewSliders.GetArrayElementAtIndex(i).objectReferenceValue = bands[i];
        viewData.ApplyModifiedPropertiesWithoutUndo();
        data.FindProperty("inspectionView").objectReferenceValue = inspection;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioSource AudioChild(Transform parent, string name, bool loop)
    {
        var child = parent.Find(name);
        if (!child) child = WorldChild(parent, name, parent.GetComponent<Renderer>().bounds.center, Quaternion.identity);
        var source = Ensure<AudioSource>(child.gameObject);
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = .7f;
        source.dopplerLevel = 0f;
        source.minDistance = 1f;
        source.maxDistance = 18f;
        source.volume = 0f;
        // The transport click has its own gain; tape source gains are set when playback starts.
        if (!loop) source.volume = .6f;
        return source;
    }

    public static void ConfigurePrefab()
    {
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var body = Ensure<Rigidbody>(prefab);
            body.mass = .06f;
            body.useGravity = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var item = Ensure<Pickupable>(prefab);
            var settings = new SerializedObject(item);
            settings.FindProperty("displayName").stringValue = "Cassette";
            settings.FindProperty("holdPositionOffset").vector3Value = new Vector3(0f, 0f, .52f);
            settings.FindProperty("holdRotationOffset").vector3Value = new Vector3(-78f, -8f, 5f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            var cassette = Ensure<Cassette>(prefab);
            var recordings = new SerializedObject(cassette);
            SetRecordings(recordings.FindProperty("normalRecordings"), "Assets/Sounds/normalcards");
            SetRecordings(recordings.FindProperty("anomalyRecordings"), "Assets/Sounds/anomalycards");
            recordings.ApplyModifiedPropertiesWithoutUndo();
            var outline = Ensure<PickupPulseOutline>(prefab);
            var outlineSettings = new SerializedObject(outline);
            outlineSettings.FindProperty("outlineMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Pickup Outline.mat");
            outlineSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static void SetRecordings(SerializedProperty array, string folder)
    {
        var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        if (paths.Length == 0) throw new InvalidOperationException("No cassette recordings in " + folder);
        array.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
    }

    private static T Ensure<T>(GameObject target) where T : Component => target.GetComponent<T>() ? target.GetComponent<T>() : target.AddComponent<T>();
    private static Transform WorldChild(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var child = new GameObject(name).transform;
        child.SetPositionAndRotation(position, rotation);
        child.SetParent(parent, true);
        return child;
    }
}
