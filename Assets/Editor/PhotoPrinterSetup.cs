using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PhotoPrinterSetup
{
    private const string ScenePath = "Assets/Scenes/scene_1.unity";
    private const string Request = "Temp/PhotoPrinter/apply.request";
    private const string MaterialPath = "Assets/Materials/Pickup Outline.mat";

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

    [MenuItem("Tools/Punch Card/Set Up Photo Printer and Pickup Outlines")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/PhotoPrinter");
        EditorSceneManager.SaveScene(scene, "Temp/PhotoPrinter/scene_1-before-printer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PhotoPrinter/applied.txt", "Photo printer delivery and subtle pickup outlines configured.\n");
    }

    public static PhotoPrinterSpawner Configure(Scene scene)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            var shader = Shader.Find("PunchCard/Menu Hover Outline");
            if (!shader) throw new InvalidOperationException("Pickup outline shader is missing.");
            material = new Material(shader) { name = "Pickup Outline" };
            material.SetColor("_OutlineColor", new Color(.8f, 1.1f, .9f));
            material.SetFloat("_OutlineWidth", 1.35f);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        ConfigureOutline("Assets/Prefabs/Photo.prefab", material);
        ConfigureOutline("Assets/Prefabs/Normal_punchcard.prefab", material);

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var printer = all.Single(t => t.name.Equals("printer", StringComparison.OrdinalIgnoreCase));
        var bounds = printer.GetComponent<Renderer>().bounds;
        // The manually placed Photo at the printer is the artist's reference for the outlet pose.
        var marker = all.Where(t => t && t.name == "Photo" && t.GetComponent<DevelopingPhoto>())
            .Where(t => Vector3.Distance(t.position, bounds.center) < 3f).SingleOrDefault();
        var output = printer.Find("Photo Output Pose");
        Vector3 direction = Vector3.ProjectOnPlane(-printer.up, Vector3.up).normalized;
        if (!output)
        {
            Vector3 position = marker ? marker.position : bounds.center + direction * 1.4f;
            Quaternion rotation = marker ? marker.rotation : Quaternion.LookRotation(direction, Vector3.up);
            output = WorldChild(printer, "Photo Output Pose", position, rotation);
        }
        var start = printer.Find("Photo Feed Start");
        if (!start) start = WorldChild(printer, "Photo Feed Start", output.position - direction * .95f, output.rotation);
        // The large MysticComputer box extends past the actual print. Put a pickup-only trigger just in front of it.
        var pickup = printer.Find("Photo Pickup Target");
        if (!pickup) pickup = WorldChild(printer, "Photo Pickup Target", output.position + direction * .72f, Quaternion.identity);
        var pickupCollider = pickup.GetComponent<BoxCollider>();
        if (!pickupCollider) pickupCollider = pickup.gameObject.AddComponent<BoxCollider>();
        pickupCollider.size = new Vector3(1.35f, .7f, .18f);
        pickupCollider.isTrigger = true;
        pickupCollider.enabled = false;
        var spawner = printer.GetComponent<PhotoPrinterSpawner>();
        if (!spawner) spawner = printer.gameObject.AddComponent<PhotoPrinterSpawner>();
        var serialized = new SerializedObject(spawner);
        serialized.FindProperty("photoPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photo.prefab");
        serialized.FindProperty("feedStart").objectReferenceValue = start;
        serialized.FindProperty("outputPose").objectReferenceValue = output;
        serialized.FindProperty("pickupTarget").objectReferenceValue = pickupCollider;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
        foreach (var interactor in all.Select(t => t.GetComponent<PlayerInteractor>()).Where(i => i))
        {
            var settings = new SerializedObject(interactor);
            var distance = settings.FindProperty("interactDistance");
            distance.floatValue = Mathf.Max(distance.floatValue, 6f);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        if (marker) UnityEngine.Object.DestroyImmediate(marker.gameObject);

        var stack = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Blank Photo Stack");
        if (stack && stack.transform.Cast<Transform>().All(t => t.GetComponent<DevelopingPhoto>()))
            UnityEngine.Object.DestroyImmediate(stack);
        EditorSceneManager.MarkSceneDirty(scene);
        return spawner;
    }

    private static void ConfigureOutline(string path, Material material)
    {
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var outline = prefab.GetComponent<PickupPulseOutline>();
            if (!outline) outline = prefab.AddComponent<PickupPulseOutline>();
            var serialized = new SerializedObject(outline);
            serialized.FindProperty("outlineMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static Transform WorldChild(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var child = new GameObject(name).transform;
        child.SetPositionAndRotation(position, rotation);
        child.SetParent(parent, true);
        return child;
    }
}
