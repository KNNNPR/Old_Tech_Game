using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

public static class MenuHoverValidation
{
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall+=()=>
        {
            if(File.Exists("Temp/Menu/hover-input-validation.request"))
            {
                if(!EditorApplication.isPlaying)
                {
                    MenuHoverOutlineSetup.EnsureInstalled();
                    MenuHoverOutlineSetup.ApplyToLoadedMenu();
                    SessionState.SetBool("MenuHoverValidation.ReturnToEditMode",true);
                    EditorApplication.isPlaying=true;
                    return;
                }
                File.Delete("Temp/Menu/hover-input-validation.request");
                ValidateInput();
                return;
            }
            if(!File.Exists("Temp/Menu/hover-validation.request")) return;
            File.Delete("Temp/Menu/hover-validation.request");
            Validate();
        };
    }

    [MenuItem("Tools/Punch Card/Menu/Validate Logo and Hover")]
    public static async void Validate()
    {
        bool asynchronous=ShaderUtil.allowAsyncCompilation;
        GameObject helper=null;
        try
        {
            Directory.CreateDirectory("Temp/Menu");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/MENU.unity");
            if(!scene.IsValid()||!scene.isLoaded) throw new InvalidOperationException("Open MENU before validation.");
            var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.CompareTag("MainCamera"));
            var body=renderers.First(r=>r.name=="MysticComputer");
            var screen=renderers.First(r=>r.name=="Monitor");
            var lever=renderers.First(r=>r.name=="Lever handle");
            var outlineMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Menu/Hover Outline.mat");
            var whiteMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Menu/Room Logo Glow.mat");
            if(!outlineMaterial||!whiteMaterial||whiteMaterial.shader.name!="Universal Render Pipeline/Lit") throw new InvalidOperationException("Menu materials are not assigned.");
            ShaderUtil.allowAsyncCompilation=false;
            helper=new GameObject("Menu hover validation") { hideFlags=HideFlags.HideAndDontSave };
            var pcOutline=helper.AddComponent<MenuHoverOutline>(); pcOutline.Configure(outlineMaterial,body,screen);
            var leverOutline=helper.AddComponent<MenuHoverOutline>(); leverOutline.Configure(outlineMaterial,lever);
            EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(500);
            var original=Capture(camera,"Temp/Menu/hover-none.png");
            pcOutline.SetHovered(true);
            var pc=Capture(camera,"Temp/Menu/hover-computer.png");
            pcOutline.SetHovered(false);leverOutline.SetHovered(true);
            var handle=Capture(camera,"Temp/Menu/hover-lever.png");
            leverOutline.SetHovered(false);
            var restored=Capture(camera,"Temp/Menu/hover-restored.png");
            int pcPixels=Changed(original,pc); int leverPixels=Changed(original,handle);
            if(ShaderUtil.ShaderHasError(outlineMaterial.shader)||ShaderUtil.ShaderHasError(whiteMaterial.shader)) throw new InvalidOperationException("Menu shader compilation failed.");
            if(pcPixels<40||leverPixels<5) throw new InvalidOperationException($"Outline not visible: computer={pcPixels}, lever={leverPixels}.");
            if(pcPixels>30000||leverPixels>2500) throw new InvalidOperationException("Hover colors fill the object instead of outlining it.");
            if(Changed(original,restored)>5) throw new InvalidOperationException("Hover contour did not disappear.");
            File.WriteAllText("Temp/Menu/hover-validation.txt",$"PASS\nUnity {Application.unityVersion}\nComputer outline pixels: {pcPixels}\nLever outline pixels: {leverPixels}\nBoth shaders compiled.\n");
            Debug.Log($"[Menu] Hover outlines verified: computer {pcPixels}, lever {leverPixels} pixels.");
        }
        catch(Exception e) {File.WriteAllText("Temp/Menu/hover-validation.txt","FAIL\n"+e);Debug.LogException(e);}
        finally
        {
            ShaderUtil.allowAsyncCompilation=asynchronous;
            if(helper) Object.DestroyImmediate(helper);
            SceneView.RepaintAll();
        }
    }

    [MenuItem("Tools/Punch Card/Menu/Validate Hover Input in Play Mode")]
    public static async void ValidateInput()
    {
        var trace=new System.Text.StringBuilder();
        Mouse originalMouse=Mouse.current;
        Mouse testMouse=null;
        bool asynchronous=ShaderUtil.allowAsyncCompilation;
        try
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Start Play Mode before validating hover input.");
            await Task.Delay(2200); // Allow the menu fade and any current camera flight to finish.
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/MENU.unity");
            var controller=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ComputerMenuController>()).Single();
            if(controller.IsFocused||controller.IsMoving) throw new InvalidOperationException("Return to the room overview before validating hover input.");
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).First(c=>c.CompareTag("MainCamera"));
            var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>()).ToArray();
            var screen=renderers.First(r=>r.name=="Monitor");
            var lever=renderers.First(r=>r.name=="Lever handle");
            var hovered=typeof(ComputerMenuController).GetField("hoveredTarget",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(string name in new[]{"computerOutline","leverOutline","hoverOutlineMaterial"})
                trace.AppendLine(name+"="+typeof(ComputerMenuController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller));
            ShaderUtil.allowAsyncCompilation=false;
            Physics.SyncTransforms();
            // A virtual Unity device leaves the desktop cursor and physical mouse untouched.
            testMouse=InputSystem.AddDevice<Mouse>("Menu hover validation");
            var outside=new Vector2(-100,-100);
            void MovePointer(Vector2 position,ComputerMenuTarget.TargetKind? expected)
            {
                testMouse.MakeCurrent();
                InputState.Change(testMouse.position,position);
                UnityEngine.EventSystems.EventSystem.current?.currentInputModule?.Process();
                controller.SendMessage("Update");
                controller.SendMessage("LateUpdate");
                var actual=(ComputerMenuTarget.TargetKind?)hovered.GetValue(controller);
                trace.AppendLine($"Pointer {position}: hover={actual}");
                foreach(var copy in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.name.StartsWith("Hover outline -")))
                    trace.AppendLine($"{copy.name}: enabled={copy.enabled}, active={copy.gameObject.activeInHierarchy}, layer={copy.gameObject.layer}, forceOff={copy.forceRenderingOff}, shader={copy.sharedMaterial.shader.name}, bounds={copy.bounds}");
                if(actual!=expected) throw new InvalidOperationException($"Hover route: expected {expected}, got {actual}, pointer {position}.");
            }
            MovePointer(outside,null);
            var original=Capture(camera,"Temp/Menu/input-none.png");
            MovePointer(camera.WorldToScreenPoint(screen.bounds.center),ComputerMenuTarget.TargetKind.Computer);
            var pc=Capture(camera,"Temp/Menu/input-computer.png");
            MovePointer(camera.WorldToScreenPoint(lever.bounds.center),ComputerMenuTarget.TargetKind.PowerLever);
            var handle=Capture(camera,"Temp/Menu/input-lever.png");
            MovePointer(outside,null);
            var restored=Capture(camera,"Temp/Menu/input-restored.png");
            int pcPixels=Changed(original,pc), leverPixels=Changed(original,handle);
            if(pcPixels<40||pcPixels>30000||leverPixels<5||leverPixels>2500||Changed(original,restored)>5)
                throw new InvalidOperationException($"Hover input rendering: computer={pcPixels}, lever={leverPixels}, restored={Changed(original,restored)}.");
            File.WriteAllText("Temp/Menu/hover-input-validation.txt",$"PASS\nComputer and lever pointer hits verified.\nComputer contour pixels: {pcPixels}\nLever contour pixels: {leverPixels}\nContour disappears on pointer exit.\n");
        }
        catch(Exception e) {File.WriteAllText("Temp/Menu/hover-input-validation.txt","FAIL\n"+e+"\n"+trace);Debug.LogException(e);}
        finally
        {
            ShaderUtil.allowAsyncCompilation=asynchronous;
            if(testMouse!=null) InputSystem.RemoveDevice(testMouse);
            if(originalMouse!=null&&originalMouse.added) originalMouse.MakeCurrent();
            if(SessionState.GetBool("MenuHoverValidation.ReturnToEditMode",false))
            {
                SessionState.SetBool("MenuHoverValidation.ReturnToEditMode",false);
                EditorApplication.isPlaying=false;
            }
        }
    }

    private static int Changed(Color32[] a,Color32[] b)
    {
        int count=0;
        for(int i=0;i<a.Length;i++) if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>90) count++;
        return count;
    }

    private static Color32[] Capture(Camera camera,string path)
    {
        var target=RenderTexture.GetTemporary(1280,720,24);
        var previous=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());return image.GetPixels32();
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
    }
}
