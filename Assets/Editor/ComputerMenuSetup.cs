using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class ComputerMenuSetup
{
    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists("Temp/Menu/computer-inspect.txt")) Inspect();
            if (File.Exists("Temp/Menu/computer.request"))
            {
                File.Delete("Temp/Menu/computer.request");
                try { Apply(); }
                catch(Exception e) { File.WriteAllText("Temp/Menu/computer-error.txt",e.ToString()); Debug.LogException(e); }
            }
        };
    }

    [MenuItem("Tools/Punch Card/Menu/Inspect Computer Scene")]
    public static void Inspect()
    {
        Scene scene=SceneManager.GetSceneByPath("Assets/Scenes/MENU.unity");
        if(!scene.IsValid()||!scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Menu");
        var report=new StringBuilder();
        foreach(var root in scene.GetRootGameObjects())
        {
            report.AppendLine("ROOT "+root.name);
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))
                report.AppendLine($"CAMERA {camera.name} pos={camera.transform.position:F5} rot={camera.transform.rotation.eulerAngles:F5} fov={camera.fieldOfView}");
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                report.AppendLine($"MESH {PathOf(renderer.transform)} center={renderer.bounds.center:F5} size={renderer.bounds.size:F5} rotation={renderer.transform.rotation.eulerAngles:F4} scale={renderer.transform.lossyScale:F5} mats={string.Join(",",renderer.sharedMaterials.Select(m=>m?m.name:"null"))}");
            foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))
                report.AppendLine($"CANVAS {PathOf(canvas.transform)} pos={canvas.transform.position:F5} rot={canvas.transform.rotation.eulerAngles:F5} size={((RectTransform)canvas.transform).sizeDelta} scale={canvas.transform.lossyScale:F7}");
        }
        File.WriteAllText("Temp/Menu/computer-inspect.txt",report.ToString());
    }
    private static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;

    [MenuItem("Tools/Punch Card/Menu/Apply Computer Room Menu")]
    public static void Apply() => ApplyComputerRoom();
}
