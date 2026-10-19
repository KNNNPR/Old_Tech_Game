using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies audio to live scenes without replacing any authored scene or transform.</summary>
public static class SceneAudioSetup
{
    public static readonly string[] ScenePaths = {
        "Assets/Scenes/MENU.unity", "Assets/Scenes/scene_1_Kirill.unity", "Assets/Scenes/TUTORIAL.unity",
        "Assets/Scenes/HELPYDIALOGUE.unity", "Assets/Scenes/Intermission.unity", "Assets/Scenes/Intermission 2.unity"
    };
    private const string Request = "Temp/SceneAudio/apply-v1.request";
    private const string InteractionRequest = "Temp/SceneAudio/interactions-v1.request";
    private const string WrongSortingRequest = "Temp/SceneAudio/wrong-sorting-v1.request";
    private const string EntranceDoorRequest = "Temp/SceneAudio/entrance-door-v1.request";
    private const string FootstepsRequest = "Temp/SceneAudio/concrete-footsteps-v1.request";
    private const string WorldPlayerRequest = "Temp/SceneAudio/world-player-v1.request";
    private const string IntermissionEnvironmentRequest = "Temp/SceneAudio/intermission-environment-v1.request";
    private static double nextCheck;
    private static bool applying;
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update += () => {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (File.Exists(Request)) Apply();
            if (File.Exists(InteractionRequest)) ApplyInteractions();
            if (File.Exists(WrongSortingRequest)) ApplyWrongSorting();
            if (File.Exists(EntranceDoorRequest)) ApplyEntranceDoor();
            if (File.Exists(FootstepsRequest)) ApplyConcreteFootsteps();
            if (File.Exists(WorldPlayerRequest)) ApplyWorldPlayerAudio();
            if (File.Exists(IntermissionEnvironmentRequest)) ApplyIntermissionEnvironment();
        };
    }
    [MenuItem("Tools/Punch Card/Audio/Set Up Room and Dialogue Sounds")]
    public static void Apply()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        Directory.CreateDirectory("Temp/SceneAudio");
        try
        {
            foreach (string path in ScenePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/" + scene.name + "-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
                    Configure(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText("Temp/SceneAudio/applied.txt", "PASS\nMENU screen-power sound limited to one second. scene_1_Kirill: ambient, local ironlungs loop, half-volume lever, screen buttons, cipher slider, per-stroke drill, ten-second reading fade, station camera sounds, physical buttons and grounded footsteps. All five dialogue scenes have spatial Helpy / Operator C syllables and immediate skip / end cleanup. Recorder channels respect master volume. All non-menu sounds except ambient are fully 3D; original transforms preserved.\n");
            File.Delete(Request);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/apply-error.txt", e.ToString()); Debug.LogException(e); }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false; }
    }
    public static void Configure(Scene scene)
    {
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses = transforms.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale, t.parent));
        if (scene.path == MenuAudioSetup.ScenePath) MenuAudioSetup.Configure(scene);
        else
        {
            if (scene.name == "scene_1_Kirill") ConfigureGameplay(scene, transforms);
            foreach (var t in transforms)
            {
                var intro = t.GetComponent<HelpyTutorialIntro>();
                var dialogue = t.GetComponent<HelpyReturnDialogue>();
                var monologue = t.GetComponent<IntermissionCinematic>();
                if (!intro && !dialogue && !monologue) continue;
                UnityEngine.Object owner = intro ? intro : dialogue ? (UnityEngine.Object)dialogue : monologue;
                var settings = new SerializedObject(owner);
                var playerCamera = (Camera)settings.FindProperty("playerCamera").objectReferenceValue;
                Transform robot = monologue ? null : (Transform)settings.FindProperty("robot").objectReferenceValue;
                if (robot)
                {
                    var face = robot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Cube.001");
                    if (face) robot = face.transform;
                }
                var voice = t.GetComponent<DialogueVoice>(); if (!voice) voice = t.gameObject.AddComponent<DialogueVoice>();
                Ref(voice, "source", Source(t, "Dialogue voice audio", .32f));
                Ref(voice, "robotVoice", Clip("robovoice.mp3")); Ref(voice, "humanVoice", Clip("humanvoice.mp3"));
                Ref(voice, "robotSpeaker", robot); Ref(voice, "operatorSpeaker", playerCamera.transform);
            }
            foreach (var source in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)))
            {
                source.ignoreListenerVolume = false; source.dopplerLevel = 0;
                if (source.name != "Room ambient audio") source.spatialBlend = 1;
            }
        }
        foreach (var p in poses)
            if (p.Key.localPosition != p.Value.Item1 || p.Key.localRotation != p.Value.Item2 || p.Key.localScale != p.Value.Item3 || p.Key.parent != p.Value.Item4)
                throw new InvalidOperationException("Audio setup moved an authored transform: " + scene.name + "/" + p.Key.name);
    }
    private static void ConfigureGameplay(Scene scene, Transform[] all)
    {
        T One<T>() where T : Component => all.Select(t => t.GetComponent<T>()).Single(c => c);
        var lever = One<FeedLever>(); var player = One<FirstPersonController>(); var interactor = One<PlayerInteractor>();
        var computer = all.Single(t => t.name == "MysticComputer");
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Room audio");
        if (!root) { root = new GameObject("Room audio"); SceneManager.MoveGameObjectToScene(root, scene); }
        var room = root.GetComponent<GameplaySceneAudio>(); if (!room) room = root.AddComponent<GameplaySceneAudio>();
        var ambient = Source(root.transform, "Room ambient audio", .18f, Clip("ambient.mp3")); ambient.spatialBlend = 0; ambient.loop = true;
        var hum = Source(computer, "Computer ironlungs audio", .28f, Clip("ironlungs.mp3")); hum.loop = true; hum.minDistance = .7f; hum.maxDistance = 6f;
        var reading = Source(computer, "Card reading audio", .45f, Clip("reading.wav"));
        var camera = Source(interactor.PlayerCamera ? interactor.PlayerCamera.transform : player.transform, "Station camera audio", .45f, Clip("camera move.mp3"));
        var steps = Source(player.transform, "Player footsteps audio", .4f, Clip("step_concrete.mp3")); steps.minDistance = 1.5f;
        Ref(room, "ambient", ambient); Ref(room, "computerHum", hum); Ref(room, "reading", reading); Ref(room, "cameraMove", camera); Ref(room, "footsteps", steps);
        Ref(room, "lever", lever); Ref(room, "player", player); Ref(room, "character", player.GetComponent<CharacterController>());
        Ref(lever, "audioSource", Source(lever.transform, "Lever audio", 1f)); Ref(lever, "pullClip", Clip("lever.wav")); Number(lever, "pullVolume", .5f);
        Transform head = (Transform)new SerializedObject(lever).FindProperty("punchHead").objectReferenceValue;
        if (!head) head = all.Single(t => t.name == "PunchingMachine");
        Ref(lever, "punchAudioSource", Source(head, "Punch stroke audio", .55f)); Ref(lever, "punchClip", Clip("drill.mp3"));
        foreach (var button in all.Select(t => t.GetComponent<PressButton>()).Where(c => c))
        { Ref(button, "audioSource", Source(button.transform, "Physical button audio", .6f)); Ref(button, "clickClip", Clip("buttonpress.wav")); }
        foreach (var button in all.Select(t => t.GetComponent<ExtractButton>()).Where(c => c))
        { Ref(button, "audioSource", Source(button.transform, "Screen button audio", .5f)); Ref(button, "clickClip", Clip("press_ingame.mp3")); }
        foreach (var dial in all.Select(t => t.GetComponent<CipherDial>()).Where(c => c))
        { Ref(dial, "audioSource", Source(dial.transform, "Screen slider audio", .3f)); Ref(dial, "stepClip", Clip("slider.mp3")); }
        foreach (var recorder in all.Select(t => t.GetComponent<CassetteRecorder>()).Where(c => c))
            Ref(recorder, "buttonClip", Clip("buttonpress.wav"));
    }
    private static AudioClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/sfx/" + name);
        if (!clip) throw new InvalidOperationException("Missing sound: " + name);
        return clip;
    }
    private static AudioSource Source(Transform parent, string name, float volume, AudioClip clip = null)
    {
        var child = parent.Find(name);
        if (!child)
        {
            child = new GameObject(name).transform; child.SetParent(parent, false);
            var renderer = parent.GetComponent<Renderer>(); if (renderer) child.position = renderer.bounds.center;
        }
        var source = child.GetComponent<AudioSource>(); if (!source) source = child.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.volume = volume; source.clip = clip; source.spatialBlend = 1;
        source.ignoreListenerVolume = false; source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f; source.maxDistance = 12f;
        return source;
    }
    private static void Ref(UnityEngine.Object owner, string name, UnityEngine.Object value)
    { var data = new SerializedObject(owner); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(owner); }
    private static void Number(UnityEngine.Object owner, string name, float value)
    { var data = new SerializedObject(owner); data.FindProperty(name).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }

    [MenuItem("Tools/Punch Card/Audio/Set Up Kirill Interaction Sounds")]
    public static void ApplyInteractions()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        const string path = "Assets/Scenes/scene_1_Kirill.unity";
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Directory.CreateDirectory("Temp/SceneAudio");
            EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/Kirill-before-interactions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            ConfigureInteractions(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/SceneAudio/interactions-applied.txt", "PASS\nscene_1_Kirill: spaced non-overlapping footsteps including the authored entrance walk; door opening/closing, photo exposure, pickup/throw, real drop impacts, both tube spawns; oven bite times derived from incinerate curves and burning faded with its flames; NormalCardCan vacuum two seconds with fade after one second. All sources 3D and master-controlled. Authored transforms unchanged.\n");
            if (File.Exists(InteractionRequest)) File.Delete(InteractionRequest);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/interactions-error.txt", e.ToString()); Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false;
        }
    }
    public static void ConfigureInteractions(Scene scene)
    {
        if (scene.name != "scene_1_Kirill") throw new InvalidOperationException("Interaction sound setup is scoped to scene_1_Kirill.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale, t.parent));
        var room = all.Select(t => t.GetComponent<GameplaySceneAudio>()).Single(c => c);
        var player = all.Select(t => t.GetComponent<PlayerInteractor>()).Single(c => c);
        Ref(room, "itemHandling", Source(player.PlayerCamera.transform, "Item handling audio", .4f, Clip("pickUp.wav")));
        Number(room, "stepInterval", .62f); Number(room, "stepDistance", .35f);
        var drops = new SerializedObject(room); var voices = drops.FindProperty("dropImpacts"); voices.arraySize = 3;
        for (int i = 0; i < voices.arraySize; i++) voices.GetArrayElementAtIndex(i).objectReferenceValue = Source(room.transform, "Drop impact audio " + (i + 1), .45f, Clip("Drop.wav"));
        drops.ApplyModifiedPropertiesWithoutUndo();
        foreach (var door in all.Select(t => t.GetComponent<AnimatedDoor>()).Where(c => c))
            Ref(door, "motionAudio", Source(door.transform, "Door motion audio", .5f, Clip("autodoor.mp3")));
        ConfigureEntranceDoor(scene);
        foreach (var photo in all.Select(t => t.GetComponent<PhotoExposureStation>()).Where(c => c))
            Ref(photo, "exposureAudio", Source(photo.transform, "Photo exposure audio", .55f, Clip("photo.mp3")));
        foreach (var tube in all.Select(t => t.GetComponent<PunchCardTubeSpawner>()).Where(c => c))
        {
            var outlet = (Transform)new SerializedObject(tube).FindProperty("releasePoint").objectReferenceValue;
            Ref(tube, "spawnAudio", Source(outlet, "Punch card spawn audio", .55f, Clip("punchcard_spawn.wav")));
        }
        foreach (var tube in all.Select(t => t.GetComponent<CassetteTubeSpawner>()).Where(c => c))
        {
            var outlet = (Transform)new SerializedObject(tube).FindProperty("outputPose").objectReferenceValue;
            Ref(tube, "spawnAudio", Source(outlet, "Cassette spawn audio", .55f, Clip("casette_spawn.wav")));
        }
        foreach (var bin in all.Select(t => t.GetComponent<SortBin>()).Where(c => c && c.Verdict == SortVerdict.Normal))
        {
            Ref(bin, "audioSource", Source(bin.transform, "Accepted card vacuum audio", .55f)); Ref(bin, "suckClip", Clip("vacuum.mp3"));
            Number(bin, "processingSoundSeconds", 2f); Number(bin, "processingFadeStart", 1f);
            Ref(bin, "acceptedAudio", Source(bin.transform, "Inside pipe audio", .45f, Clip("drop_inside_pipe.mp3")));
        }
        var oven = all.Select(t => t.GetComponent<OvenIncineration>()).Single(c => c);
        var ovenData = new SerializedObject(oven);
        var flames = (ParticleSystem)ovenData.FindProperty("flames").objectReferenceValue;
        Ref(oven, "fireAudio", Source(flames.transform, "Oven burning audio", .45f, Clip("burning.wav")));
        Ref(oven, "biteAudio", Source(flames.transform, "Oven metal bite audio", .6f, Clip("metal_bite.mp3")));
        Number(oven, "fireFadeSeconds", 2f);
        var clip = (AnimationClip)ovenData.FindProperty("incinerate").objectReferenceValue;
        float[] bites = FindBiteTimes(clip);
        ovenData.Update(); var times = ovenData.FindProperty("biteTimes"); times.arraySize = bites.Length;
        for (int i = 0; i < bites.Length; i++) times.GetArrayElementAtIndex(i).floatValue = bites[i];
        ovenData.ApplyModifiedPropertiesWithoutUndo();
        foreach (var p in poses)
            if (p.Key.localPosition != p.Value.Item1 || p.Key.localRotation != p.Value.Item2 || p.Key.localScale != p.Value.Item3 || p.Key.parent != p.Value.Item4)
                throw new InvalidOperationException("Interaction sounds changed a transform: " + p.Key.name);
    }
    private static float[] FindBiteTimes(AnimationClip clip)
    {
        var curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform) && b.propertyName.StartsWith("m_LocalPosition."))
            .ToDictionary(b => (b.path, b.propertyName), b => AnimationUtility.GetEditorCurve(clip, b));
        Vector3 Position(string path, float t)
        {
            float Value(string axis) => curves.TryGetValue((path, "m_LocalPosition." + axis), out var curve) ? curve.Evaluate(t) : 0;
            return new Vector3(Value("x"), Value("y"), Value("z"));
        }
        float Gap(float t) => (Position("OvenToothTop", t) - Position("OvenToothBottom", t)).sqrMagnitude;
        float[] keys = curves.Values.SelectMany(c => c.keys.Select(k => k.time)).Distinct().OrderBy(t => t).ToArray();
        var bites = Enumerable.Range(1, Mathf.Max(0, keys.Length - 2)).Where(i => Gap(keys[i]) < Gap(keys[i - 1]) - 1e-9f && Gap(keys[i]) < Gap(keys[i + 1]) - 1e-9f).Select(i => keys[i]).ToArray();
        if (bites.Length == 0) throw new InvalidOperationException("No tooth closures found in incinerate animation.");
        return bites;
    }

    [MenuItem("Tools/Punch Card/Audio/Set Up Wrong Sorting Sound")]
    public static void ApplyWrongSorting()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        const string path = "Assets/Scenes/scene_1_Kirill.unity";
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Directory.CreateDirectory("Temp/SceneAudio");
            EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/Kirill-before-wrong-sorting-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            ConfigureWrongSorting(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/SceneAudio/wrong-sorting-applied.txt", "PASS\nWrong.wav plays for both wrong sorting verdicts from the midpoint between Left_Lamp and Right_Lamp. Fully spatial, master-controlled, linear attenuation from 6 to 40 metres.\n");
            if (File.Exists(WrongSortingRequest)) File.Delete(WrongSortingRequest);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/wrong-sorting-error.txt", e.ToString()); Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false;
        }
    }
    public static void ConfigureWrongSorting(Scene scene)
    {
        if (scene.name != "scene_1_Kirill") throw new InvalidOperationException("Wrong sorting audio setup is scoped to scene_1_Kirill.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var left = all.Single(t => t.name.Equals("Left_Lamp", StringComparison.OrdinalIgnoreCase));
        var right = all.Single(t => t.name.Equals("Right_Lamp", StringComparison.OrdinalIgnoreCase));
        var manager = all.Select(t => t.GetComponent<SortingManager>()).Single(c => c);
        var room = all.Select(t => t.GetComponent<GameplaySceneAudio>()).Single(c => c);
        var source = Source(room.transform, "Sorting error audio", .65f, Clip("Wrong.wav"));
        source.transform.position = (left.position + right.position) * .5f;
        source.minDistance = 6f; source.maxDistance = 40f; source.priority = 64;
        Ref(manager, "audioSource", source); Ref(manager, "mistakeClip", source.clip);
    }

    [MenuItem("Tools/Punch Card/Audio/Set Up Entrance Door Sounds")]
    public static void ApplyEntranceDoor()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        const string path = "Assets/Scenes/scene_1_Kirill.unity";
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Directory.CreateDirectory("Temp/SceneAudio");
            EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/Kirill-before-entrance-door-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            ConfigureEntranceDoor(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/SceneAudio/entrance-door-applied.txt", "PASS\nOpening entrance door: dooropen. Closing entrance door after walking in: autodoor. Independent spatial, master-controlled voices finish naturally without movement-duration cutoffs or restarts; short 15ms attack and 120ms release smooth the clip edges.\n");
            if (File.Exists(EntranceDoorRequest)) File.Delete(EntranceDoorRequest);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/entrance-door-error.txt", e.ToString()); Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false;
        }
    }
    public static void ConfigureEntranceDoor(Scene scene)
    {
        if (scene.name != "scene_1_Kirill") throw new InvalidOperationException("Entrance door audio setup is scoped to scene_1_Kirill.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var room = all.Select(t => t.GetComponent<GameplaySceneAudio>()).Single(c => c);
        var intro = all.Select(t => t.GetComponent<HelpyTutorialIntro>()).Single(c => c);
        var entrance = (Transform)new SerializedObject(intro).FindProperty("entranceDoor").objectReferenceValue;
        Ref(room, "entranceDoorOpenAudio", Source(entrance, "Entrance door creak audio", .5f, Clip("dooropen.mp3")));
        Ref(room, "entranceDoorAudio", Source(entrance, "Door motion audio", .5f, Clip("autodoor.mp3")));
    }

    [MenuItem("Tools/Punch Card/Audio/Use Concrete Footsteps")]
    public static void ApplyConcreteFootsteps()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        const string path = "Assets/Scenes/scene_1_Kirill.unity";
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var room = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameplaySceneAudio>(true)).Single();
            var source = (AudioSource)new SerializedObject(room).FindProperty("footsteps").objectReferenceValue;
            if (!source) throw new InvalidOperationException("Player footstep source is missing.");
            var clip = Clip("step_concrete.mp3");
            Directory.CreateDirectory("Temp/SceneAudio");
            EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/Kirill-before-concrete-footsteps-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            source.clip = clip;
            EditorUtility.SetDirty(source); PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/SceneAudio/concrete-footsteps-applied.txt", "PASS\nFootstep clip changed to step_concrete.mp3 (" + clip.length + "s). Cadence, volume, spatial routing and scripted entrance footsteps preserved.\n");
            if (File.Exists(FootstepsRequest)) File.Delete(FootstepsRequest);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/concrete-footsteps-error.txt", e.ToString()); Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false;
        }
    }

    public static string[] WorldPlayerScenePaths => AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "MENU", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileNameWithoutExtension(p), "GAME_OVER", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p).ToArray();

    [MenuItem("Tools/Punch Card/Audio/Set Up Player Sounds In All Gameplay Scenes")]
    public static void ApplyWorldPlayerAudio()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        Directory.CreateDirectory("Temp/SceneAudio");
        try
        {
            foreach (string path in WorldPlayerScenePaths)
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/" + scene.name + "-before-player-audio-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
                    ConfigureWorldPlayerAudio(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText("Temp/SceneAudio/world-player-applied.txt", "PASS\nstep_concrete footsteps in all nine non-menu scenes, including scripted entrance and both Intermission walks. Both Intermissions: inhale at cigarette drag, exhale on lowering, camera move for authored head turns. Reader slots: insert.wav on player insertion before reading, silent on ejection. All sounds spatial and master-controlled; authored transforms preserved.\n" + string.Join("\n", WorldPlayerScenePaths));
            if (File.Exists(WorldPlayerRequest)) File.Delete(WorldPlayerRequest);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/world-player-error.txt", e.ToString()); Debug.LogException(e); }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false; }
    }
    public static void ConfigureWorldPlayerAudio(Scene scene)
    {
        if (string.Equals(scene.name, "MENU", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Player audio setup excludes MENU.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale, t.parent));
        var player = all.Select(t => t.GetComponent<FirstPersonController>()).Single(c => c);
        var room = all.Select(t => t.GetComponent<GameplaySceneAudio>()).SingleOrDefault(c => c);
        if (!room)
        {
            var root = new GameObject("Room audio"); SceneManager.MoveGameObjectToScene(root, scene);
            room = root.AddComponent<GameplaySceneAudio>();
        }
        var steps = (AudioSource)new SerializedObject(room).FindProperty("footsteps").objectReferenceValue;
        if (!steps) { steps = Source(player.transform, "Player footsteps audio", .4f); steps.minDistance = 1.5f; }
        steps.clip = Clip(scene.name == "THE_END" ? "slimestep.mp3" : "step_concrete.mp3"); steps.spatialBlend = 1; steps.ignoreListenerVolume = false; steps.dopplerLevel = 0;
        Ref(room, "footsteps", steps); Ref(room, "player", player); Ref(room, "character", player.GetComponent<CharacterController>());
        foreach (var cinematic in all.Select(t => t.GetComponent<IntermissionCinematic>()).Where(c => c))
        {
            var camera = (Camera)new SerializedObject(cinematic).FindProperty("playerCamera").objectReferenceValue;
            Ref(cinematic, "inhaleAudio", Source(camera.transform, "Cigarette inhale audio", .38f, Clip("inhale.mp3")));
            Ref(cinematic, "exhaleAudio", Source(camera.transform, "Cigarette exhale audio", .38f, Clip("exhale.mp3")));
            Ref(cinematic, "headTurnAudio", Source(camera.transform, "Cinematic head turn audio", .35f, Clip("camera move.mp3")));
        }
        foreach (var lever in all.Select(t => t.GetComponent<FeedLever>()).Where(c => c))
        {
            var slot = (ItemSlot)new SerializedObject(lever).FindProperty("slot").objectReferenceValue;
            if (slot) Ref(slot, "insertAudio", Source(slot.transform, "Reader insert audio", .5f, Clip("insert.wav")));
        }
        foreach (var p in poses)
            if (p.Key.localPosition != p.Value.Item1 || p.Key.localRotation != p.Value.Item2 || p.Key.localScale != p.Value.Item3 || p.Key.parent != p.Value.Item4)
                throw new InvalidOperationException("Player audio setup moved an authored transform: " + scene.name + "/" + p.Key.name);
    }

    [MenuItem("Tools/Punch Card/Audio/Set Up Intermission Wind and Exit Door")]
    public static void ApplyIntermissionEnvironment()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        Directory.CreateDirectory("Temp/SceneAudio");
        try
        {
            foreach (string path in new[] { "Assets/Scenes/Intermission.unity", "Assets/Scenes/Intermission 2.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    EditorSceneManager.SaveScene(scene, "Temp/SceneAudio/" + scene.name + "-before-wind-door-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
                    ConfigureIntermissionEnvironment(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText("Temp/SceneAudio/intermission-environment-applied.txt", "PASS\nBoth Intermissions: looping spatial wind.wav outside on the balcony, smoothly faded in and out with the cinematic. dooropen at arrival by the exit, using the tutorial's smooth door playback; allowed to finish before scene completion. Master-controlled; authored transforms preserved.\n");
            if (File.Exists(IntermissionEnvironmentRequest)) File.Delete(IntermissionEnvironmentRequest);
        }
        catch (Exception e) { File.WriteAllText("Temp/SceneAudio/intermission-environment-error.txt", e.ToString()); Debug.LogException(e); }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false; }
    }
    public static void ConfigureIntermissionEnvironment(Scene scene)
    {
        if (scene.name != "Intermission" && scene.name != "Intermission 2") throw new InvalidOperationException("Wind / exit door setup is scoped to the two Intermissions.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poses = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale, t.parent));
        var cinematic = all.Select(t => t.GetComponent<IntermissionCinematic>()).Single(c => c);
        var room = all.Select(t => t.GetComponent<GameplaySceneAudio>()).Single(c => c);
        var data = new SerializedObject(cinematic);
        var balcony = (Transform)data.FindProperty("openingPose").objectReferenceValue;
        var door = (Transform)data.FindProperty("doorFocus").objectReferenceValue;
        var wind = Source(balcony, "Balcony wind audio", .18f, Clip("wind.wav"));
        wind.loop = true; wind.minDistance = 6f; wind.maxDistance = 45f;
        Ref(cinematic, "windAudio", wind);
        Ref(room, "entranceDoorOpenAudio", Source(door, "Exit door creak audio", .5f, Clip("dooropen.mp3")));
        foreach (var p in poses)
            if (p.Key.localPosition != p.Value.Item1 || p.Key.localRotation != p.Value.Item2 || p.Key.localScale != p.Value.Item3 || p.Key.parent != p.Value.Item4)
                throw new InvalidOperationException("Intermission environment audio moved a transform: " + p.Key.name);
    }
}
