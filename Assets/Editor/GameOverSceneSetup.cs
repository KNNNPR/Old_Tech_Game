using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameOverSceneSetup
{
    public const string ScenePath = "Assets/Scenes/GAME_OVER.unity";
    private const string Request = "Temp/GameOver/apply-v1.request";
    private static bool applying;
    private static double nextCheck;
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update += () => {
            if (EditorApplication.timeSinceStartup < nextCheck) return; nextCheck = EditorApplication.timeSinceStartup + 1;
            if (File.Exists(Request)) Apply();
        };
    }
    [MenuItem("Tools/Punch Card/GAME OVER/Set Up Defeat Screen")]
    public static void Apply()
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        applying = true; var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        Directory.CreateDirectory("Temp/GameOver");
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, "Temp/GameOver/GameOver-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            Configure(scene); EnableBuildScene(ScenePath); EnableBuildScene("Assets/Scenes/" + GameFlow.SceneAfterMenu() + ".unity");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/GameOver/applied.txt", "PASS\nGAME_OVER: asymmetric viscous blood waves and falling droplets, in-game computer font / CRT glow. DO YOU WANT TO RETRY? reveals YES / NO with no hover underline. YES resets the run and loads the first day; NO fades and quits (stops Play Mode in the Editor). Music loops immediately after splash finishes. Master volume and authored transforms preserved.\n");
            if (File.Exists(Request)) File.Delete(Request);
        }
        catch (Exception e) { File.WriteAllText("Temp/GameOver/error.txt", e.ToString()); Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); applying = false;
        }
    }
    public static GameOverScreen Configure(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Defeat screen setup only supports GAME_OVER.");
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "GAME OVER screen");
        var authored = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => !root || !t.IsChildOf(root.transform)).ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale, t.parent));
        var importer = AssetImporter.GetAtPath("Assets/Textures/blood.png") as TextureImporter;
        if (!importer) throw new InvalidOperationException("Assets/Textures/blood.png is missing.");
        if (!importer.alphaIsTransparency || importer.alphaSource != TextureImporterAlphaSource.FromInput || importer.mipmapEnabled || importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/blood.png");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/GameOverBlood.shader");
        var font = TerminalFont();
        if (!texture || !shader || !font) throw new InvalidOperationException("Defeat screen texture, shader or font is missing.");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/GameOver")) AssetDatabase.CreateFolder("Assets/Materials", "GameOver");
        const string materialPath = "Assets/Materials/GameOver/Blood Curtain.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        material.shader = shader; material.mainTexture = texture; material.SetFloat("_Progress", 1); material.SetFloat("_FlowTime", 0); EditorUtility.SetDirty(material);
        material.SetFloat("_WaveStrength", .026f); material.SetFloat("_FlowSpeed", .012f);
        var textMaterial = TerminalMaterial(font);
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 0;
        if (!camera.GetComponent<AudioListener>()) camera.gameObject.AddComponent<AudioListener>();
        if (!root) { root = new GameObject("GAME OVER screen", typeof(RectTransform)); SceneManager.MoveGameObjectToScene(root, scene); }
        var canvas = Ensure<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000; canvas.pixelPerfect = true;
        var scaler = Ensure<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        Ensure<GraphicRaycaster>(root);
        var screen = Ensure<GameOverScreen>(root);
        var background = Full(root.transform, "Black background"); var black = Ensure<Image>(background.gameObject); black.color = Color.black; black.raycastTarget = false;
        var bloodRect = Full(root.transform, "Flowing blood"); var blood = Ensure<RawImage>(bloodRect.gameObject); blood.texture = texture; blood.material = material; blood.color = Color.white; blood.raycastTarget = false;
        var contentRect = Full(root.transform, "Menu"); var content = Ensure<CanvasGroup>(contentRect.gameObject); content.alpha = 1; content.interactable = content.blocksRaycasts = true;
        var title = Text(contentRect, "GAME OVER", font, 160, new Vector2(.5f,.53f), new Vector2(1720,240));
        title.text = "GAME OVER"; title.fontStyle = FontStyles.Bold; title.characterSpacing = 6; title.color = new Color(.85f,1,1);
        title.fontSharedMaterial = textMaterial; title.extraPadding = true; title.UpdateMeshPadding();
        var oldRule = contentRect.Find("Title rule"); if (oldRule) oldRule.gameObject.SetActive(false);
        var oldButton = contentRect.Find("Back to menu"); if (oldButton) oldButton.name = "Retry prompt";
        var button = TerminalButton(contentRect, "Retry prompt", "DO YOU WANT TO RETRY?", font, textMaterial, screen,
            new Vector2(.5f,.34f), new Vector2(1120,110), 34);
        var choicesRect = Box(contentRect, "Retry choices", new Vector2(.5f,.22f), new Vector2(600,100));
        var choices = Ensure<CanvasGroup>(choicesRect.gameObject); choices.alpha = 0; choices.interactable = choices.blocksRaycasts = false;
        var yes = TerminalButton(choicesRect, "Yes", "YES", font, textMaterial, screen, new Vector2(.25f,.5f), new Vector2(220,90), 36);
        var no = TerminalButton(choicesRect, "No", "NO", font, textMaterial, screen, new Vector2(.75f,.5f), new Vector2(220,90), 36);
        var fadeRect = Full(root.transform, "Return fade"); fadeRect.SetAsLastSibling();
        var cover = Ensure<Image>(fadeRect.gameObject); cover.color = Color.black;
        var fade = Ensure<CanvasGroup>(fadeRect.gameObject); fade.alpha = 0; fade.blocksRaycasts = fade.interactable = false;
        Ref(screen, "blood", blood); Ref(screen, "menu", content); Ref(screen, "fade", fade); Ref(screen, "retryButton", button);
        Ref(screen, "choices", choices); Ref(screen, "yesButton", yes); Ref(screen, "noButton", no);
        Ref(screen, "splashAudio", Sound(camera.transform, "Fatal splash audio", "Assets/Sounds/sfx/splash.mp3", .7f));
        var music = Sound(camera.transform, "GAME OVER music", "Assets/Sounds/music/gameover.mp3", .35f); music.loop = true; Ref(screen, "musicAudio", music);
        Ref(screen, "hoverAudio", Sound(camera.transform, "GAME OVER hover", "Assets/Sounds/sfx/hover.wav", .4f));
        var oldPress = camera.transform.Find("GAME OVER return"); if (oldPress) oldPress.name = "GAME OVER press";
        Ref(screen, "pressAudio", Sound(camera.transform, "GAME OVER press", "Assets/Sounds/sfx/press_ingame.mp3", .65f));
        var events = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).SingleOrDefault();
        if (!events)
        {
            var eventRoot = new GameObject("GAME OVER EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventRoot, scene); eventRoot.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        foreach (var p in authored)
            if (p.Key.localPosition != p.Value.Item1 || p.Key.localRotation != p.Value.Item2 || p.Key.localScale != p.Value.Item3 || p.Key.parent != p.Value.Item4)
                throw new InvalidOperationException("Defeat screen setup moved an authored transform: " + p.Key.name);
        return screen;
    }
    public static void EnableBuildScene(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList(); var existing = scenes.FirstOrDefault(s => s.path == path);
        if (existing != null) { if (existing.enabled) return; existing.enabled = true; }
        else scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
    private static TMP_FontAsset TerminalFont()
    {
        // The same shared font is assigned to START / SETTINGS on the MENU computer.
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (!asset) throw new InvalidOperationException("The in-game computer font is missing.");
        return asset;
    }
    private static Button TerminalButton(Transform parent, string name, string caption, TMP_FontAsset font, Material material,
        GameOverScreen screen, Vector2 anchor, Vector2 size, float fontSize)
    {
        var rect = Box(parent, name, anchor, size);
        var hit = Ensure<Image>(rect.gameObject); hit.color = new Color(0,0,0,.005f); hit.raycastTarget = true;
        var button = Ensure<Button>(rect.gameObject); button.targetGraphic = hit; button.interactable = true;
        var label = Text(rect, "Label", font, fontSize, new Vector2(.5f,.5f), size - new Vector2(70,16));
        label.text = caption; label.fontStyle = FontStyles.Normal; label.characterSpacing = 3; label.color = new Color(.55f,.86f,.96f);
        label.fontSharedMaterial = material; label.extraPadding = true; label.UpdateMeshPadding();
        var cursor = Text(rect, "Terminal cursor", font, fontSize - 2, new Vector2(size.x > 500 ? .21f : .12f,.5f), new Vector2(42,60));
        cursor.text = ">"; cursor.color = new Color(.45f,.9f,1); cursor.fontSharedMaterial = material; cursor.extraPadding = true; cursor.UpdateMeshPadding();
        var oldUnderline = rect.Find("Hover underline"); if (oldUnderline) oldUnderline.gameObject.SetActive(false);
        var feedback = Ensure<GameOverBackButton>(rect.gameObject);
        Ref(feedback, "screen", screen); Ref(feedback, "label", label); Ref(feedback, "cursor", cursor);
        return button;
    }
    private static Material TerminalMaterial(TMP_FontAsset font)
    {
        const string path = "Assets/Materials/GameOver/CRT Terminal Text.mat";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/GameOverCRTText.shader");
        if (!shader) throw new InvalidOperationException("CRT text shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(font.material); AssetDatabase.CreateAsset(material,path); }
        material.shader = shader; material.mainTexture = font.atlasTexture;
        material.SetColor("_FaceColor", Color.white); material.SetFloat("_FaceDilate",0);
        material.EnableKeyword("GLOW_ON"); material.SetColor("_GlowColor",new Color(.12f,.66f,1,.65f));
        material.SetFloat("_GlowInner",.07f); material.SetFloat("_GlowOuter",.6f); material.SetFloat("_GlowPower",.6f);
        material.SetFloat("_OutlineWidth",0); material.SetFloat("_Sharpness",.12f);
        EditorUtility.SetDirty(material); return material;
    }
    private static T Ensure<T>(GameObject o) where T : Component { var c = o.GetComponent<T>(); return c ? c : o.AddComponent<T>(); }
    private static RectTransform Full(Transform parent, string name)
    {
        var r = Child(parent,name); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
    }
    private static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var r = Child(parent,name); r.anchorMin = r.anchorMax = r.pivot = anchor; r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = size; return r;
    }
    private static RectTransform Child(Transform parent, string name)
    {
        var t = parent.Find(name); if (t) return (RectTransform)t;
        var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); return r;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Vector2 anchor, Vector2 dimensions)
    {
        var t = Ensure<TextMeshProUGUI>(Box(parent,name,anchor,dimensions).gameObject); t.font = font; t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false; return t;
    }
    private static AudioSource Sound(Transform parent, string name, string path, float volume)
    {
        var child = parent.Find(name); if (!child) { child = new GameObject(name).transform; child.SetParent(parent,false); }
        var s = Ensure<AudioSource>(child.gameObject); s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (!s.clip) throw new InvalidOperationException("Missing defeat-screen sound: " + path);
        s.playOnAwake = false; s.loop = false; s.volume = volume; s.spatialBlend = 1; s.dopplerLevel = 0; s.ignoreListenerVolume = false;
        s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2; s.maxDistance = 30; return s;
    }
    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
}
