using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class MenuSceneSetup
{
    private static readonly Color Amber = new Color(0.87f, 0.66f, 0.32f);
    private static readonly Color Muted = new Color(0.37f, 0.4f, 0.35f);
    private const string GlowPath = "Assets/Settings/MenuGlow.asset";

    [MenuItem("Tools/Punch Card/Menu/Apply Menu Design and Glow")]
    public static void EnhanceDesign() => EnhanceDesign(true);

    private static void EnhanceDesign(bool save)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply menu design in Edit Mode.");
        Scene scene = save ? SceneManager.GetSceneByPath(ScenePath) : SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Directory.CreateDirectory("Temp/Menu");
        if (save) EditorSceneManager.SaveScene(scene, "Temp/Menu/MENU-before-design-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        var roots = scene.GetRootGameObjects();
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.name == "Menu Camera");
        var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).First(c => c.name == "Menu UI");
        var menu = roots.SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).First();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        SetupGlow(camera, roots);
        SetupAtmosphere(camera, roots);
        Transform oldFrame = canvas.transform.Find("Reader frame");
        if (oldFrame) Object.DestroyImmediate(oldFrame.gameObject);
        RectTransform frame = Rect("Reader frame", canvas.transform, new Vector2(1280,720), Vector2.zero);
        frame.SetAsFirstSibling();
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.sizeDelta = Vector2.zero;
        BuildFrame(frame);

        var serialized = new SerializedObject(menu);
        Transform main = serialized.FindProperty("mainPanel").objectReferenceValue is GameObject mainObject ? mainObject.transform : null;
        if (!main) throw new InvalidOperationException("The menu has no main button panel.");
        ((RectTransform)main).anchoredPosition = new Vector2(0,-12);
        ((RectTransform)main).sizeDelta = new Vector2(470,190);
        StyleRow((Button)serialized.FindProperty("startButton").objectReferenceValue, "01", "START", 34, 30, true);
        StyleRow((Button)serialized.FindProperty("settingsButton").objectReferenceValue, "02", "SETTINGS", 23, -54, false);

        var settings = (GameObject)serialized.FindProperty("settingsPanel").objectReferenceValue;
        var settingsRect = (RectTransform)settings.transform;
        settingsRect.anchoredPosition = new Vector2(0,-90);
        settingsRect.localScale = Vector3.one * .9f;
        settings.GetComponent<Image>().color = new Color(.014f,.018f,.015f,.98f);
        Transform previousTrim = settings.transform.Find("Reader trim");
        if (previousTrim) Object.DestroyImmediate(previousTrim.gameObject);
        var trim = Rect("Reader trim", settings.transform, new Vector2(580,520), Vector2.zero);
        trim.SetAsFirstSibling();
        Bar(trim, "Top rule", new Vector2(470,2), new Vector2(0,252), new Color(.7f,.52f,.24f,.65f));
        Corner(trim, new Vector2(-274,244), 1,-1);
        Corner(trim, new Vector2(274,244), -1,-1);
        Corner(trim, new Vector2(-274,-244), 1,1);
        Corner(trim, new Vector2(274,-244), -1,1);
        // Keep the existing settings controls and their serialized references intact.
        var credit = canvas.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.text == "Made for Ludum Dare 60");
        if (credit)
        {
            credit.text = "MADE FOR LUDUM DARE 60"; credit.fontSize = 11; credit.characterSpacing = 1.6f;
            credit.color = Muted; credit.rectTransform.anchoredPosition = new Vector2(42,28);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (save)
        {
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/Menu/design.txt", "Applied reader design, HDR title emission, local Bloom, button feedback and atmosphere.\nScene: " + scene.path);
            ValidateDesign();
        }
        if (previous.IsValid() && previous.isLoaded && previous != scene) SceneManager.SetActiveScene(previous);
        SceneView.RepaintAll();
    }

    private static void SetupGlow(Camera camera, GameObject[] roots)
    {
        camera.allowHDR = true;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.volumeLayerMask = 1;
        VolumeProfile profile = Asset<VolumeProfile>(GlowPath, () => ScriptableObject.CreateInstance<VolumeProfile>());
        if (!profile.TryGet<Bloom>(out var bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom,profile); }
        bloom.name = "Menu title bloom";
        bloom.threshold.Override(.9f); bloom.intensity.Override(.8f); bloom.scatter.Override(.72f);
        bloom.clamp.Override(12f); bloom.highQualityFiltering.Override(true); bloom.maxIterations.Override(6);
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(bloom);
        var volume = roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.name == "Menu title glow");
        if (!volume) volume = new GameObject("Menu title glow").AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Logo Ivory.mat");
        material.EnableKeyword("_EMISSION");
        material.SetColor("_BaseColor", new Color(.52f,.49f,.39f));
        material.SetColor("_EmissionColor", new Color(3.2f,2.1f,.95f));
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        foreach (var letter in roots.SelectMany(r => r.GetComponentsInChildren<FloatingLogoLetter>(true)))
        {
            var s = new SerializedObject(letter); s.FindProperty("glowColor").colorValue = new Color(3.2f,2.1f,.95f,1); s.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupAtmosphere(Camera camera, GameObject[] roots)
    {
        GameObject old = roots.FirstOrDefault(r => r.name == "Menu atmosphere");
        if (old) Object.DestroyImmediate(old);
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Menu atmosphere";
        Object.DestroyImmediate(background.GetComponent<Collider>());
        background.transform.position = new Vector3(0,0,-7);
        background.transform.localScale = new Vector3(42,14,1);
        background.GetComponent<MeshRenderer>().sharedMaterial = Asset<Material>(ArtPath + "/Atmosphere.mat", () => new Material(Shader.Find("PunchCard/Menu Atmosphere")));
        background.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void BuildFrame(RectTransform frame)
    {
        // Marks are decorative and must never intercept the mouse rays aimed at the logo.
        Label("PUNCH CARD READER", frame, 10, new Vector2(300,20), new Vector2(0,284), Muted, 5f);
        var badge = Label("00  /  STANDBY",frame,10,new Vector2(200,20),new Vector2(0,100), Amber, 3.5f);
        badge.color = new Color(.62f,.55f,.39f);
        for (int i=0;i<9;i++)
            Bar(frame,"Reader hole",new Vector2(3,7),new Vector2(-44+i*11,77), new Color(.42f,.38f,.26f,i%3==0?.55f:.22f));
        var leftTop = Rect("Upper left",frame,Vector2.zero,Vector2.zero);
        leftTop.anchorMin = leftTop.anchorMax = new Vector2(0,1); leftTop.anchoredPosition = new Vector2(42,-38);
        Corner(leftTop,Vector2.zero,1,-1);
        var topText = Label("PC / 00",leftTop,11,new Vector2(160,22),new Vector2(88,-4), Muted, 3f);
        topText.alignment = TextAlignmentOptions.Left;
        var rightTop = Rect("Upper right",frame,Vector2.zero,Vector2.zero);
        rightTop.anchorMin = rightTop.anchorMax = Vector2.one; rightTop.anchoredPosition = new Vector2(-42,-38);
        Corner(rightTop,Vector2.zero,-1,-1);
        Label("READY",rightTop,10,new Vector2(100,22),new Vector2(-57,-4),Amber,3f);
        Bar(rightTop,"Status light",new Vector2(4,4),new Vector2(-103,-4),Amber);
        var bottom = Rect("Footer rule",frame,new Vector2(-84,1),new Vector2(0,55));
        bottom.anchorMin = new Vector2(0,0); bottom.anchorMax = new Vector2(1,0);
        var image = bottom.gameObject.AddComponent<Image>(); image.color = new Color(.4f,.42f,.36f,.22f); image.raycastTarget = false;
        var footer = Label("AWAITING INPUT",frame,10,new Vector2(230,20),new Vector2(-42,30),Muted,3f);
        footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(1,0);
        footer.rectTransform.pivot = new Vector2(1,.5f); footer.alignment = TextAlignmentOptions.Right;
        for (int side=0;side<2;side++)
        {
            var rail = Rect("Feed rail",frame,Vector2.zero,Vector2.zero);
            rail.anchorMin = rail.anchorMax = new Vector2(side,.5f); rail.anchoredPosition = new Vector2(side==0?23:-23,0);
            for (int i=0;i<17;i++)
                Bar(rail,"Feed perforation",new Vector2(3,8),new Vector2(0,-224+i*28),new Color(.45f,.45f,.35f,i%4==0?.26f:.10f));
        }
    }

    private static void StyleRow(Button button, string number, string title, float fontSize, float y, bool primary)
    {
        var row = (RectTransform)button.transform;
        row.sizeDelta = new Vector2(440,70); row.anchoredPosition = new Vector2(0,y);
        var label = button.GetComponentInChildren<TMP_Text>();
        label.text = title; label.fontSize = fontSize; label.characterSpacing = primary ? 2f : 4f;
        label.alignment = TextAlignmentOptions.Left;
        label.rectTransform.sizeDelta = new Vector2(290,60); label.rectTransform.anchoredPosition = new Vector2(9,1);
        label.color = primary ? new Color(.93f,.91f,.83f) : new Color(.68f,.69f,.65f);
        Transform old = row.Find("Reader row details"); if (old) Object.DestroyImmediate(old.gameObject);
        var detail = Rect("Reader row details",row,row.sizeDelta,Vector2.zero);
        Image underline = Bar(detail,"Underline",new Vector2(440,1),new Vector2(0,-34),new Color(.56f,.55f,.46f,.26f));
        TMP_Text index = Label(number,detail,11,new Vector2(36,30),new Vector2(-190,2),Muted,2f);
        Label("/",detail,13,new Vector2(15,30),new Vector2(-161,2),new Color(.4f,.4f,.33f));
        var active = Rect("Hover details",detail,row.sizeDelta,Vector2.zero);
        CanvasGroup group = active.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
        Bar(active,"Read head",new Vector2(2,42),new Vector2(-219,0),Amber);
        Label(">",active,23,new Vector2(35,35),new Vector2(193,0),Amber);
        for(int i=0;i<5;i++) Bar(active,"Punch",new Vector2(3,7),new Vector2(125+i*8,3+(i%2)*8),new Color(.8f,.62f,.29f,.7f));
        Image backing = button.GetComponent<Image>(); backing.color = new Color(.82f,.62f,.28f,.025f);
        var feedback = button.GetComponent<MenuButtonFeedback>();
        Set(feedback,"rowBackground",backing); Set(feedback,"underline",underline); Set(feedback,"hoverDetails",group); Set(feedback,"indexLabel",index);
        var s = new SerializedObject(feedback); s.FindProperty("primaryRow").boolValue=primary; s.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text Label(string text, Transform parent, float size, Vector2 dimensions, Vector2 pos, Color color, float tracking=0)
    {
        TMP_Text label = Text(text,parent,size,dimensions,pos); label.color=color; label.characterSpacing=tracking; label.fontStyle=FontStyles.Normal; return label;
    }
    private static Image Bar(Transform parent,string name,Vector2 size,Vector2 pos,Color color)
    {
        Image image = Rect(name,parent,size,pos).gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
    }
    private static void Corner(Transform parent,Vector2 pos,int x,int y)
    {
        var color = new Color(.54f,.50f,.36f,.42f);
        Bar(parent,"Registration mark",new Vector2(19,1),pos+new Vector2(x*9,0),color);
        Bar(parent,"Registration mark",new Vector2(1,19),pos+new Vector2(0,y*9),color);
    }

    [MenuItem("Tools/Punch Card/Menu/Validate Design and Glow")]
    public static async void ValidateDesign()
    {
        bool asynchronous = ShaderUtil.allowAsyncCompilation;
        Bloom bloom = null; float intensity = 0;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Camera camera = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).First(c=>c.name=="Menu Camera");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlowPath);
            if (!profile.TryGet(out bloom)) throw new InvalidOperationException("Menu bloom is missing.");
            intensity = bloom.intensity.value;
            foreach(string path in new[]{ArtPath+"/Logo Ivory.mat",ArtPath+"/Atmosphere.mat",ArtPath+"/Punched Paper.mat"})
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Invalid menu material: "+path);
            }
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); await Task.Delay(500);
            Capture(camera,"Temp/Menu/menu-glow.png");
            bloom.intensity.value = 0;
            EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(250);
            Capture(camera,"Temp/Menu/menu-without-glow.png");
            var a=new Texture2D(2,2); var b=new Texture2D(2,2);
            int changed=0;
            try
            {
                a.LoadImage(File.ReadAllBytes("Temp/Menu/menu-glow.png")); b.LoadImage(File.ReadAllBytes("Temp/Menu/menu-without-glow.png"));
                var first=a.GetPixels32(); var second=b.GetPixels32();
                for(int i=0;i<first.Length;i++) if(Math.Abs(first[i].r-second[i].r)+Math.Abs(first[i].g-second[i].g)+Math.Abs(first[i].b-second[i].b)>8) changed++;
            }
            finally { Object.DestroyImmediate(a);Object.DestroyImmediate(b); }
            if(changed<500) throw new InvalidOperationException("Bloom is not visible. Changed pixels: "+changed);
            File.WriteAllText("Temp/Menu/design-validation.txt","PASS\nUnity "+Application.unityVersion+"\nPixels affected by Bloom: "+changed+"\nMenu materials compiled successfully.\n");
            Debug.Log("[Menu] Visible bloom verified: "+changed+" pixels.");
        }
        catch(Exception e) { File.WriteAllText("Temp/Menu/design-validation.txt","FAIL\n"+e); Debug.LogException(e); }
        finally { if(bloom!=null) bloom.intensity.value=intensity; ShaderUtil.allowAsyncCompilation=asynchronous; EditorApplication.QueuePlayerLoopUpdate(); }
    }
}
