using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public static partial class MenuSceneSetup
{
    [InitializeOnLoadMethod]
    private static void InspectOnImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists("Temp/Menu/models.txt")) Inspect();
            if (File.Exists("Temp/Menu/build.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/Menu/build.request");
                try { Build(); }
                catch (Exception e) { File.WriteAllText("Temp/Menu/build-error.txt", e.ToString()); Debug.LogException(e); }
            }
            if (File.Exists("Temp/Menu/design.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/Menu/design.request");
                try { EnhanceDesign(); }
                catch (Exception e) { File.WriteAllText("Temp/Menu/design-error.txt", e.ToString()); Debug.LogException(e); }
            }
        };
    }

    [MenuItem("Tools/Punch Card/Menu/Inspect Models")]
    public static void Inspect()
    {
        Directory.CreateDirectory("Temp/Menu");
        var report = new StringBuilder();
        foreach (string path in new[] { "Assets/Models/PUNCH.fbx", "Assets/Models/SCENE.fbx" })
        {
            report.AppendLine(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                report.AppendLine($"{filter.name}: vertices={mesh.vertexCount}; bounds={mesh.bounds}; rotation={filter.transform.rotation.eulerAngles}; scale={filter.transform.lossyScale}; position={filter.transform.position}");
            }
        }
        File.WriteAllText("Temp/Menu/models.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    private const string ScenePath = "Assets/Scenes/MENU.unity";
    private const string ArtPath = "Assets/Materials/Menu";
    private static TMP_FontAsset font;

    [MenuItem("Tools/Punch Card/Menu/Build Menu Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building the menu.");
        Directory.CreateDirectory("Temp/Menu");
        Directory.CreateDirectory(ArtPath);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if (File.Exists(ScenePath)) File.Copy(ScenePath, $"Temp/Menu/MENU-disk-{stamp}.unity", true);
        Scene openMenu = SceneManager.GetSceneByPath(ScenePath);
        if (openMenu.IsValid() && openMenu.isLoaded)
            EditorSceneManager.SaveScene(openMenu, $"Temp/Menu/MENU-editor-{stamp}.unity", true);

        // Build additively so unsaved work in other open scenes stays intact.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.25f);
        RenderSettings.fog = false;

        Camera camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, 12), Quaternion.Euler(0, 180, 0));
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 40;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        Light key = new GameObject("Soft ivory key", typeof(Light)).GetComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.93f, 0.78f);
        key.intensity = 1.8f;
        key.shadows = LightShadows.None;
        key.transform.rotation = Quaternion.Euler(32, -25, 0);

        Material titleMaterial = Asset<Material>(ArtPath + "/Logo Ivory.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
        titleMaterial.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.8f));
        titleMaterial.SetFloat("_Metallic", 0.12f);
        titleMaterial.SetFloat("_Smoothness", 0.32f);
        EditorUtility.SetDirty(titleMaterial);
        BuildLogo(titleMaterial);
        BuildCards(camera);
        BuildUI(camera);
        EnhanceDesign(false);

        // Close the old menu only after its in-memory contents have been backed up.
        if (openMenu.IsValid() && openMenu.isLoaded) EditorSceneManager.CloseScene(openMenu, true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != "Assets/Scenes/scene_1.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/scene_1.unity", true) }.Concat(others).ToArray();
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Selection.activeGameObject = camera.gameObject;
        File.WriteAllText("Temp/Menu/build.txt", $"Menu created: {ScenePath}\nBackup: Temp/Menu/MENU-editor-{stamp}.unity\nLetters: {Object.FindObjectsByType<FloatingLogoLetter>(FindObjectsSortMode.None).Length}\n");
        Capture(camera, "Temp/Menu/menu-preview.png");
        Debug.Log("[Menu] Menu scene created and registered first in Build Settings.");
    }

    private static T Asset<T>(string path, Func<T> create) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) { asset = create(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }

    private static void BuildLogo(Material material)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PUNCH.fbx");
        if (!model) throw new InvalidOperationException("PUNCH.fbx is missing.");
        GameObject logo = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(logo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        logo.name = "PUNCH CARD 0 - floating letters";
        var filters = logo.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && f.sharedMesh.vertexCount > 0).ToArray();
        var renderers = filters.Select(f => f.GetComponent<MeshRenderer>()).ToArray();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float scale = 8.2f / bounds.size.x;
        logo.transform.localScale *= scale;
        logo.transform.position = new Vector3(-bounds.center.x * scale, 2.5f - bounds.center.y * scale, -bounds.center.z * scale + 2f);
        foreach (var filter in filters)
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var pivot = new GameObject("Floating " + filter.name);
            pivot.transform.SetParent(logo.transform, false);
            pivot.transform.position = renderer.bounds.center;
            filter.transform.SetParent(pivot.transform, true);
            var collider = filter.gameObject.AddComponent<BoxCollider>();
            collider.center = filter.sharedMesh.bounds.center;
            Vector3 size = filter.sharedMesh.bounds.size;
            size.z = Mathf.Max(size.z, 0.0004f);
            collider.size = size;
            pivot.AddComponent<FloatingLogoLetter>();
        }
        foreach (var filter in logo.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh && filter.sharedMesh.vertexCount == 0) Object.DestroyImmediate(filter.gameObject);
    }

    private static void BuildCards(Camera camera)
    {
        var root = new GameObject("Falling punch cards");
        var rain = root.AddComponent<MenuCardRain>();
        var shader = Shader.Find("PunchCard/Menu Paper");
        if (!shader) throw new InvalidOperationException("Menu paper shader not imported.");
        Material material = Asset<Material>(ArtPath + "/Punched Paper.mat", () => new Material(shader));
        Mesh mesh = Asset<Mesh>(ArtPath + "/Punch Card Mesh.asset", () =>
        {
            var m = new Mesh { name = "Punch card" };
            // Thin double-sided sheet; holes are cut by the paper shader on both sides.
            m.vertices = new[] { new Vector3(-1.12f,-0.48f,0), new Vector3(-1.12f,0.48f,0), new Vector3(1.12f,0.48f,0), new Vector3(1.12f,-0.48f,0) };
            m.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        });
        var cards = new Transform[12];
        float[] xs = { .13f,.87f,.06f,.94f,.20f,.80f,.08f,.92f,.18f,.82f,.04f,.96f };
        for (int i = 0; i < cards.Length; i++)
        {
            var card = new GameObject("Punch card " + (i + 1), typeof(MeshFilter), typeof(MeshRenderer));
            card.transform.SetParent(root.transform, false);
            card.GetComponent<MeshFilter>().sharedMesh = mesh;
            card.GetComponent<MeshRenderer>().sharedMaterial = material;
            card.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            card.transform.position = camera.ViewportToWorldPoint(new Vector3(xs[i], -0.1f + i / 11f * 1.25f, 12 + i % 4));
            card.transform.rotation = Quaternion.Euler(15 + i * 11, 10 + i * 21, -35 + i * 27);
            card.transform.localScale = Vector3.one * (0.55f + i % 4 * 0.15f);
            cards[i] = card.transform;
        }
        Set(rain, "menuCamera", camera);
        SetArray(rain, "cards", cards);
    }

    internal static void BuildUI(Camera camera)
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var canvas = new GameObject("Menu UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var eventSystem = new GameObject("Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var controller = new GameObject("Main Menu").AddComponent<MainMenuController>();

        RectTransform main = Rect("Main buttons", canvas.transform, new Vector2(430, 160), new Vector2(0, -20));
        Button start = Button("START", main, new Vector2(350, 65), new Vector2(0, 32), 39);
        Button settings = Button("SETTINGS", main, new Vector2(350, 60), new Vector2(0, -29), 27);
        TMP_Text credit = Text("Made for Ludum Dare 60", canvas.transform, 14, new Vector2(330, 30), Vector2.zero, TextAlignmentOptions.BottomLeft);
        credit.rectTransform.anchorMin = credit.rectTransform.anchorMax = Vector2.zero;
        credit.rectTransform.pivot = Vector2.zero;
        credit.rectTransform.anchoredPosition = new Vector2(18, 16);
        credit.color = new Color(0.55f, 0.55f, 0.52f);

        RectTransform panel = Rect("Settings panel", canvas.transform, new Vector2(580, 520), new Vector2(0, -42));
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.012f, 0.013f, 0.012f, 0.97f);
        Text("SETTINGS", panel, 30, new Vector2(480, 50), new Vector2(0, 217));
        Line(panel, 179);
        Text("MASTER VOLUME", panel, 16, new Vector2(285, 35), new Vector2(-105, 145), TextAlignmentOptions.Left);
        TMP_Text volumeValue = Text("100%", panel, 18, new Vector2(80, 35), new Vector2(199, 145), TextAlignmentOptions.Right);
        Slider slider = Slider(panel, new Vector2(470, 24), new Vector2(0, 109));
        Text("KEY BINDINGS", panel, 16, new Vector2(470, 35), new Vector2(0, 63), TextAlignmentOptions.Left);
        string[] actions = { "MOVE FORWARD", "MOVE BACKWARD", "MOVE LEFT", "MOVE RIGHT", "INTERACT / PICK UP", "DROP ITEM" };
        var bindButtons = new Button[6]; var bindLabels = new TMP_Text[6];
        for (int i = 0; i < actions.Length; i++)
        {
            float y = 22 - i * 31;
            Text(actions[i], panel, 17, new Vector2(350, 30), new Vector2(-60, y), TextAlignmentOptions.Left);
            bindButtons[i] = Button("Key " + actions[i], panel, new Vector2(100, 28), new Vector2(189, y), 17);
            bindLabels[i] = bindButtons[i].GetComponentInChildren<TMP_Text>();
            var image = bindButtons[i].GetComponent<Image>(); image.color = new Color(1,1,1,0.06f);
        }
        TMP_Text hint = Text("SELECT A KEY TO REBIND   /   DUPLICATES SWAP", panel, 11, new Vector2(500, 26), new Vector2(0, -158));
        hint.color = new Color(0.55f,0.55f,0.51f);
        Line(panel, -181);
        Button reset = Button("RESET KEYS", panel, new Vector2(215, 42), new Vector2(-119, -213), 16);
        Button back = Button("BACK", panel, new Vector2(215, 42), new Vector2(119, -213), 20);
        panel.gameObject.SetActive(false);

        var fadeRect = Rect("Scene fade", canvas.transform, new Vector2(1280,720), Vector2.zero);
        fadeRect.anchorMin = Vector2.zero; fadeRect.anchorMax = Vector2.one; fadeRect.sizeDelta = Vector2.zero;
        fadeRect.gameObject.AddComponent<Image>().color = Color.black;
        CanvasGroup fade = fadeRect.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0; fade.blocksRaycasts = false;
        Set(controller,"menuCamera",camera);
        Set(controller,"mainPanel",main.gameObject); Set(controller,"settingsPanel",panel.gameObject);
        Set(controller,"startButton",start); Set(controller,"settingsButton",settings);
        Set(controller,"backButton",back); Set(controller,"resetButton",reset);
        Set(controller,"volumeSlider",slider); Set(controller,"volumeValue",volumeValue);
        Set(controller,"bindingHint",hint); Set(controller,"startLabel",start.GetComponentInChildren<TMP_Text>());
        Set(controller,"fade",fade); SetArray(controller,"bindingButtons",bindButtons); SetArray(controller,"bindingLabels",bindLabels);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private static TMP_Text Text(string value, Transform parent, float size, Vector2 dimensions, Vector2 position, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var text = Rect(value, parent, dimensions, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.alignment = alignment;
        text.fontStyle = FontStyles.Bold; text.color = new Color(0.88f,0.88f,0.84f);
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; return text;
    }
    private static Button Button(string value, Transform parent, Vector2 size, Vector2 position, float fontSize)
    {
        RectTransform rect = Rect(value, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>(); image.color = Color.clear;
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        TMP_Text label = Text(value, rect, fontSize, size, Vector2.zero);
        Set(rect.gameObject.AddComponent<MenuButtonFeedback>(), "label", label);
        return button;
    }
    private static void Line(Transform parent, float y)
    {
        Rect("Rule", parent, new Vector2(470,1), new Vector2(0,y)).gameObject.AddComponent<Image>().color = new Color(0.5f,0.5f,0.44f,0.35f);
    }
    private static Slider Slider(Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform rect = Rect("Volume slider", parent, size, position);
        var slider = rect.gameObject.AddComponent<Slider>();
        RectTransform track = Rect("Track", rect, new Vector2(size.x,3), Vector2.zero);
        track.gameObject.AddComponent<Image>().color = new Color(0.22f,0.22f,0.2f);
        RectTransform fillArea = Rect("Fill area", rect, new Vector2(size.x,3), Vector2.zero);
        RectTransform fill = Rect("Fill", fillArea, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        fill.gameObject.AddComponent<Image>().color = new Color(0.83f,0.78f,0.63f);
        RectTransform handleArea = Rect("Handle area", rect, size, Vector2.zero);
        RectTransform handle = Rect("Handle", handleArea, new Vector2(10,20), Vector2.zero);
        Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.92f,0.88f,0.74f);
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1; return slider;
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(Object target, string field, Object[] values)
    {
        var serialized = new SerializedObject(target); var property = serialized.FindProperty(field); property.arraySize = values.Length;
        for (int i=0;i<values.Length;i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Capture(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(1280,720,24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
    }
}
