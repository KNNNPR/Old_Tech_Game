using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class ComputerMenuSetup
{
    private const string MenuPath="Assets/Scenes/MENU.unity";
    private static TMP_FontAsset menuFont;

    private static void ApplyComputerRoom()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before applying the computer menu.");
        Directory.CreateDirectory("Temp/Menu");
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Scene scene=SceneManager.GetSceneByPath(MenuPath);
        if(!scene.IsValid()||!scene.isLoaded) scene=EditorSceneManager.OpenScene(MenuPath,OpenSceneMode.Additive);
        EditorSceneManager.SaveScene(scene,"Temp/Menu/MENU-before-computer-"+stamp+".unity",true);
        // Recover the user's room composition if the old generated menu was opened/saved over MENU.
        if(!scene.GetRootGameObjects().Any(r=>r.name=="SCENE"))
        {
            string room="Temp/Menu/MENU-editor-20261018-012110.unity";
            if(!File.Exists(room)) throw new InvalidOperationException("The room backup is missing: "+room);
            const string staging="Assets/Scenes/MenuRoomRestore.unity";
            File.Copy(room,staging,true);
            AssetDatabase.ImportAsset(staging,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            Scene restored=EditorSceneManager.OpenScene(staging,OpenSceneMode.Additive);
            if(!restored.GetRootGameObjects().Any(r=>r.name=="SCENE")) throw new InvalidOperationException("Room restoration did not load the SCENE model.");
            EditorSceneManager.CloseScene(scene,true);
            scene=restored;
        }
        SceneManager.SetActiveScene(scene);
        var roots=scene.GetRootGameObjects();
        Camera camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.CompareTag("MainCamera"));
        Vector3 originalPosition=camera.transform.position;
        Quaternion originalRotation=camera.transform.rotation;
        float originalFov=camera.fieldOfView;
        camera.transform.SetParent(null,true);
        foreach(var root in roots)
        {
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if(behaviour is FirstPersonController || behaviour is PlayerInteractor || behaviour is InteractionHUD || behaviour is MonitorDisplay || behaviour is FeedLever || behaviour is ExtractButton)
                { behaviour.enabled=false; PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour); }
            foreach(var body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true; body.useGravity=false; }
            if(root.name=="Player" || root.name=="Canvas") root.SetActive(false);
            if(new[]{"Menu UI","Menu EventSystem","Main Menu","Menu atmosphere","Falling punch cards","PUNCH CARD 0 - floating letters","Computer Menu Rig","Menu Overlay","Menu Logo"}.Contains(root.name))
                Object.DestroyImmediate(root);
        }
        Transform oldLogo=camera.transform.Find("Menu Logo"); if(oldLogo) Object.DestroyImmediate(oldLogo.gameObject);
        camera.name="Menu Camera"; camera.nearClipPlane=.02f; camera.allowHDR=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var roomRoot=scene.GetRootGameObjects().First(r=>r.name=="SCENE");
        var renderers=roomRoot.GetComponentsInChildren<MeshRenderer>(true);
        MeshRenderer screen=renderers.First(r=>r.name=="Monitor");
        MeshRenderer computer=renderers.First(r=>r.name=="MysticComputer");
        MeshRenderer sourceLever=renderers.First(r=>r.name=="PunchCardLever");
        foreach(var canvas in roomRoot.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        foreach(var text in roomRoot.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);

        var rig=new GameObject("Computer Menu Rig");
        Transform overview=Child("Overview pose",rig.transform);
        overview.SetPositionAndRotation(originalPosition,originalRotation);
        var filter=screen.GetComponent<MeshFilter>();
        var vertices=filter.sharedMesh.vertices.Select(v=>screen.transform.TransformPoint(v)).ToArray();
        Vector3 centre=vertices.Aggregate(Vector3.zero,(sum,v)=>sum+v)/vertices.Length;
        var triangles=filter.sharedMesh.triangles;
        Vector3 normal=Vector3.Cross(vertices[triangles[1]]-vertices[triangles[0]],vertices[triangles[2]]-vertices[triangles[0]]).normalized;
        if(Vector3.Dot(normal,originalPosition-centre)<0) normal=-normal;
        Vector3 up=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;
        Quaternion displayRotation=Quaternion.LookRotation(-normal,up);
        Vector3 right=displayRotation*Vector3.right;
        float width=vertices.Max(v=>Vector3.Dot(v,right))-vertices.Min(v=>Vector3.Dot(v,right));
        float height=vertices.Max(v=>Vector3.Dot(v,up))-vertices.Min(v=>Vector3.Dot(v,up));
        Transform focus=Child("Computer pose",rig.transform);
        focus.position=centre+normal*3.2f+up*.05f;
        focus.rotation=Quaternion.LookRotation(centre-up*.12f-focus.position,up);
        var controller=rig.AddComponent<ComputerMenuController>();
        var screenMaterial=MaterialAt("Assets/Materials/Menu/Computer CRT.mat","PunchCard/Menu Computer Screen");
        screen.sharedMaterial=screenMaterial;
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen);

        AddMeshTarget(computer,rig.transform,ComputerMenuTarget.TargetKind.Computer);
        Transform monitorHit=Child("Monitor click area",rig.transform);
        monitorHit.SetPositionAndRotation(centre,displayRotation);
        monitorHit.gameObject.layer=30;
        var monitorCollider=monitorHit.gameObject.AddComponent<BoxCollider>(); monitorCollider.size=new Vector3(width,height,.08f);
        monitorHit.gameObject.AddComponent<ComputerMenuTarget>().kind=ComputerMenuTarget.TargetKind.Computer;
        Transform lever=CreateLever(sourceLever,rig.transform);
        Light screenLight=Child("Monitor glow",rig.transform).gameObject.AddComponent<Light>();
        screenLight.type=LightType.Point; screenLight.color=new Color(.4f,1f,.66f); screenLight.intensity=.8f;
        screenLight.range=3; screenLight.shadows=LightShadows.None;screenLight.transform.position=centre+normal*.3f;
        Transform logo=BuildRoomLogo(camera);
        BuildMonitorUI(camera,centre+normal*.018f,displayRotation,width,height,controller,out var menu,out var display,out var pages,out var idle,out var hint);
        Set(controller,"menuCamera",camera);Set(controller,"overviewPose",overview);Set(controller,"computerPose",focus);Set(controller,"logo",logo);
        Set(controller,"monitorDisplay",display);Set(controller,"menuPages",pages);Set(controller,"idleDisplay",idle);
        Set(controller,"screen",screen);Set(controller,"monitorLight",screenLight);Set(controller,"lever",lever);Set(controller,"hint",hint);Set(controller,"menu",menu);
        Set(controller,"hoverOutlineMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Menu/Hover Outline.mat"));
        SetFloat(controller,"overviewFov",originalFov);
        camera.transform.SetPositionAndRotation(overview.position,overview.rotation);
        MenuAmbientMotionSetup.Configure(scene);
        var others=EditorBuildSettings.scenes.Where(s=>s.path!=MenuPath&&s.path!="Assets/Scenes/scene_1.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MenuPath,true),new EditorBuildSettingsScene("Assets/Scenes/scene_1.unity",true)}.Concat(others).ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,MenuPath);AssetDatabase.SaveAssets();
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MenuRoomRestore.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/MenuRoomRestore.unity");
        Inspect();
        File.WriteAllText("Temp/Menu/computer-applied.txt",$"Applied computer room menu.\nBackup: MENU-before-computer-{stamp}.unity\nCamera: {overview.position}; focus: {focus.position}\nMonitor: {centre}; dimensions: {width} x {height}\nLogo letters: {logo.GetComponentsInChildren<FloatingLogoLetter>().Length}\n");
        Selection.activeGameObject=rig;SceneView.RepaintAll();
        Debug.Log("[Menu] Computer room menu applied to MENU.");
    }

    private static Transform CreateLever(MeshRenderer source,Transform parent)
    {
        Transform pivot=Child("Red power lever",parent);
        pivot.position=source.bounds.center-Vector3.up*.13f;
        var visual=Child("Lever handle",pivot);
        visual.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        visual.localScale=source.transform.lossyScale;
        Mesh mesh=source.GetComponent<MeshFilter>().sharedMesh;
        visual.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=visual.gameObject.AddComponent<MeshRenderer>();
        var material=MaterialAt("Assets/Materials/Menu/Red Power Lever.mat","Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor",new Color(.55f,.018f,.012f));material.SetColor("_EmissionColor",new Color(.24f,.008f,.003f));
        material.EnableKeyword("_EMISSION");EditorUtility.SetDirty(material);renderer.sharedMaterial=material;
        var collider=visual.gameObject.AddComponent<BoxCollider>();collider.center=mesh.bounds.center;
        collider.size=Vector3.Max(mesh.bounds.size,new Vector3(.002f,.002f,.002f));
        visual.gameObject.layer=30;visual.gameObject.AddComponent<ComputerMenuTarget>().kind=ComputerMenuTarget.TargetKind.PowerLever;
        source.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        return pivot;
    }

    private static void AddMeshTarget(MeshRenderer source,Transform parent,ComputerMenuTarget.TargetKind kind)
    {
        Transform target=Child("Computer click area",parent);
        target.SetPositionAndRotation(source.transform.position,source.transform.rotation);target.localScale=source.transform.lossyScale;
        target.gameObject.AddComponent<MeshCollider>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
        target.gameObject.layer=30;target.gameObject.AddComponent<ComputerMenuTarget>().kind=kind;
    }

    private static Transform BuildRoomLogo(Camera camera)
    {
        Transform logo=Child("Menu Logo",camera.transform);logo.localPosition=new Vector3(0,0,2.4f);
        GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PUNCH.fbx");
        var glyphs=model.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh&&f.sharedMesh.vertexCount>0)
            .OrderByDescending(f=>f.GetComponent<Renderer>().bounds.center.x).ToArray();
        if(glyphs.Length!=10) throw new InvalidOperationException("Expected 10 model letters in PUNCH.fbx.");
        float viewHeight=2*2.4f*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        float viewWidth=viewHeight*(16f/9f);
        float targetWidth=viewWidth*.24f;
        float sourceWidth=glyphs.Take(5).Sum(f=>f.GetComponent<Renderer>().bounds.size.x)+.15f;
        float scale=targetWidth/sourceWidth;
        var material=MaterialAt("Assets/Materials/Menu/Room Logo Glow.mat","Universal Render Pipeline/Lit");
        material.shader=Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor",new Color(.8f,.8f,.72f));
        material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(material);
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
        float x=-viewWidth*.38f;
        for(int i=0;i<glyphs.Length;i++)
        {
            if(i==5) x=-viewWidth*.38f;
            var source=glyphs[i];Bounds bounds=source.GetComponent<Renderer>().bounds;
            float w=bounds.size.x*scale;
            Transform pivot=Child("Letter "+"PUNCHCARD0"[i],logo);
            pivot.localPosition=new Vector3(x+w*.5f,viewHeight*(i<5?.30f:.14f),0);
            Transform meshTransform=Child(source.name,pivot);
            meshTransform.localRotation=Quaternion.Euler(0,180,0)*source.transform.rotation;
            meshTransform.localScale=source.transform.lossyScale*scale;
            meshTransform.localPosition=-(meshTransform.localRotation*Vector3.Scale(source.sharedMesh.bounds.center,meshTransform.localScale));
            meshTransform.gameObject.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
            meshTransform.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            var collider=meshTransform.gameObject.AddComponent<BoxCollider>();collider.center=source.sharedMesh.bounds.center;collider.size=source.sharedMesh.bounds.size;
            meshTransform.gameObject.layer=30;
            var motion=pivot.gameObject.AddComponent<FloatingLogoLetter>();
            SetFloat(motion,"maxOffset",.085f);SetFloat(motion,"pushStrength",.025f);SetFloat(motion,"hoverAmplitude",.004f);
            var serialized=new SerializedObject(motion);serialized.FindProperty("glowColor").colorValue=new Color(.45f,.45f,.45f,1);serialized.ApplyModifiedPropertiesWithoutUndo();
            x+=w+.025f;
            if(i==8) x+=.055f;
        }
        // Keep the initial composition, then leave the letters anchored in the room.
        logo.SetParent(null,true);
        return logo;
    }

    private static void BuildMonitorUI(Camera camera,Vector3 centre,Quaternion rotation,float width,float height,ComputerMenuController controller,
        out MainMenuController menu,out CanvasGroup display,out CanvasGroup pages,out CanvasGroup idle,out TMP_Text hint)
    {
        MenuSceneSetup.BuildUI(camera);
        var scene=camera.gameObject.scene;
        menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainMenuController>(true)).First();
        var canvas=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First(c=>c.name=="Menu UI");
        menuFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        canvas.name="Computer screen interface";canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        var rect=(RectTransform)canvas.transform;rect.SetPositionAndRotation(centre,rotation);
        float scale=width/640f;rect.sizeDelta=new Vector2(640,height/scale);rect.localScale=Vector3.one*scale;
        canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=2;
        display=canvas.gameObject.AddComponent<CanvasGroup>();
        var serialized=new SerializedObject(menu);
        var main=(GameObject)serialized.FindProperty("mainPanel").objectReferenceValue;
        var settings=(GameObject)serialized.FindProperty("settingsPanel").objectReferenceValue;
        var pageRect=UIRect("Menu pages",rect,rect.sizeDelta,Vector2.zero);
        pages=pageRect.gameObject.AddComponent<CanvasGroup>();pages.alpha=0;pages.blocksRaycasts=pages.interactable=false;
        main.transform.SetParent(pageRect,false);settings.transform.SetParent(pageRect,false);
        ((RectTransform)main.transform).anchoredPosition=Vector2.zero;
        ((RectTransform)settings.transform).anchoredPosition=new Vector2(0,-12);
        settings.GetComponent<Image>().color=new Color(.002f,.012f,.008f,.98f);
        Button start=(Button)serialized.FindProperty("startButton").objectReferenceValue;
        Button options=(Button)serialized.FindProperty("settingsButton").objectReferenceValue;
        StyleMonitorButton(start,"START",70,39);StyleMonitorButton(options,"SETTINGS",0,32);
        var exitRect=UIRect("EXIT",main.transform,new Vector2(350,60),new Vector2(0,-70));
        var image=exitRect.gameObject.AddComponent<Image>();image.color=Color.clear;
        Button exit=exitRect.gameObject.AddComponent<Button>();exit.targetGraphic=image;
        var exitText=UIText("EXIT",exitRect,32,new Vector2(350,60),Vector2.zero);exitText.raycastTarget=false;
        Set(exitRect.gameObject.AddComponent<MenuButtonFeedback>(),"label",exitText);
        Set(menu,"exitButton",exit);Set(menu,"computerMenu",controller);
        UIText("PUNCH CARD / 0",pageRect,19,new Vector2(500,32),new Vector2(0,rect.sizeDelta.y*.5f-35));
        UIText("ESC / RETURN",pageRect,12,new Vector2(400,30),new Vector2(0,-rect.sizeDelta.y*.5f+22));
        var idleRect=UIRect("Idle display",rect,rect.sizeDelta,Vector2.zero);
        idle=idleRect.gameObject.AddComponent<CanvasGroup>();idle.blocksRaycasts=idle.interactable=false;
        UIText("PC / 00",idleRect,30,new Vector2(500,50),new Vector2(0,25));
        UIText("CLICK TO CONNECT",idleRect,17,new Vector2(500,40),new Vector2(0,-25));
        // Full-screen fades and hints remain outside the physical monitor canvas.
        var overlay=UIRect("Menu Overlay",null,new Vector2(1280,720),Vector2.zero);
        var overlayCanvas=overlay.gameObject.AddComponent<Canvas>();overlayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=overlay.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        var fade=(CanvasGroup)serialized.FindProperty("fade").objectReferenceValue;
        fade.transform.SetParent(overlay,false);
        var credit=canvas.GetComponentsInChildren<TMP_Text>(true).First(t=>t.text=="Made for Ludum Dare 60");
        credit.transform.SetParent(overlay,false);
        credit.gameObject.SetActive(true);
        credit.text="Made for Ludumdare 60";
        credit.fontSize=12;credit.fontStyle=FontStyles.Normal;
        credit.color=new Color(.8f,.8f,.76f);credit.raycastTarget=false;
        credit.rectTransform.anchorMin=credit.rectTransform.anchorMax=Vector2.zero;
        credit.rectTransform.pivot=Vector2.zero;
        credit.rectTransform.anchoredPosition=new Vector2(18,16);
        hint=UIText("CLICK COMPUTER",overlay,15,new Vector2(700,32),new Vector2(0,30));
        hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=new Vector2(.5f,0);
        hint.color=new Color(.7f,.8f,.74f);
        fade.transform.SetAsLastSibling();
        MenuTerminalStyleSetup.ConfigureCanvas(canvas);
    }

    private static void StyleMonitorButton(Button button,string text,float y,float size)
    {
        ((RectTransform)button.transform).anchoredPosition=new Vector2(0,y);
        var label=button.GetComponentInChildren<TMP_Text>();label.text=text;label.fontSize=size;label.characterSpacing=3;
    }
    private static TMP_Text UIText(string text,Transform parent,float size,Vector2 dimensions,Vector2 position)
    {
        var label=UIRect(text,parent,dimensions,position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=menuFont;label.fontSize=size;label.text=text;label.alignment=TextAlignmentOptions.Center;
        label.color=new Color(.47f,.95f,.67f);label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.NoWrap;return label;
    }
    private static RectTransform UIRect(string name,Transform parent,Vector2 size,Vector2 position)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
    }
    private static Transform Child(string name,Transform parent) { var t=new GameObject(name).transform;t.SetParent(parent,false);return t; }
    private static Material MaterialAt(string path,string shader)
    {
        Directory.CreateDirectory("Assets/Materials/Menu");var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material) { material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path); }
        return material;
    }
    private static void Set(Object target,string name,Object value)
    {
        var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetFloat(Object target,string name,float value)
    {
        var s=new SerializedObject(target);s.FindProperty(name).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();
    }
}
