using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PhotoDarkroomSetup
{
    private const string ScenePath = "Assets/Scenes/scene_1.unity";
    private const string Request = "Temp/Photo/apply.request";
    private const string PrefabPath = "Assets/Prefabs/Photo.prefab";
    private static readonly string[] ClipNames = { "Plane.008", "Plane.009", "Plane.010", "Plane.012", "Plane.013" };

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

    [MenuItem("Tools/Punch Card/Set Up Photo Darkroom in scene_1")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/Photo");
        EditorSceneManager.SaveScene(scene, "Temp/Photo/scene_1-before-photos-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/Photo/applied.txt", "Photo enlarger, five clothespins and five blank prints configured.\n");
    }

    public static void Configure(Scene scene)
    {
        ConfigurePrefab();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var instrument = all.Single(t => t.name == "Cylinder.016");
        var table = all.Single(t => t.name == "Cube.026").GetComponent<Renderer>().bounds;
        var spot = all.Select(t => t.GetComponent<Light>()).Where(l => l && l.type == LightType.Spot)
            .OrderBy(l => Vector3.Distance(l.transform.position, instrument.position)).First();
        float paperSize = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.localScale.x * 10f;

        var station = Ensure<PhotoExposureStation>(instrument.gameObject);
        if (!instrument.GetComponent<Collider>())
        {
            var meshCollider = instrument.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = instrument.GetComponent<MeshFilter>().sharedMesh;
        }
        // Keep the entire sheet on the table, inside the existing downward red spotlight.
        var exposurePosition = new Vector3(spot.transform.position.x, table.max.y + .016f, table.min.z + paperSize * .5f + .06f);
        var exposureAnchor = WorldChild(instrument, "Photo Exposure Anchor", exposurePosition, Quaternion.identity);
        var paperHit = WorldChild(instrument, "Photo Table Interaction", exposurePosition + Vector3.up * .04f, Quaternion.identity);
        var paperHitbox = Ensure<BoxCollider>(paperHit.gameObject);
        paperHitbox.size = new Vector3(paperSize + .1f, .1f, paperSize + .1f);
        paperHitbox.isTrigger = true;
        Set(station, s => { s.FindProperty("paperAnchor").objectReferenceValue = exposureAnchor; s.FindProperty("exposureLight").objectReferenceValue = spot; });

        foreach (var name in ClipNames)
        {
            var clip = all.Single(t => t.name == name);
            var bounds = clip.GetComponent<Renderer>().bounds;
            var clothespin = Ensure<PhotoClothespin>(clip.gameObject);
            var paperPosition = new Vector3(bounds.center.x, bounds.min.y - paperSize * .5f + .065f, bounds.center.z - .055f);
            var anchor = WorldChild(clip, "Hanging Photo Anchor", paperPosition, Quaternion.Euler(-90f, 0f, 0f));
            Set(clothespin, s => s.FindProperty("paperAnchor").objectReferenceValue = anchor);
            var hitBounds = bounds;
            hitBounds.Encapsulate(new Bounds(paperPosition, new Vector3(paperSize, paperSize, .04f)));
            hitBounds.Expand(.06f);
            var hit = WorldChild(clip, "Photo Clip Interaction", hitBounds.center, Quaternion.identity);
            var hitbox = Ensure<BoxCollider>(hit.gameObject);
            hitbox.size = hitBounds.size;
            hitbox.isTrigger = true;
        }

        // Only seed the scene once. Existing prints and manually placed prefab instances are retained.
        if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DevelopingPhoto>(true)).Any())
        {
            var stack = all.FirstOrDefault(t => t.name == "Blank Photo Stack");
            if (!stack) { stack = new GameObject("Blank Photo Stack").transform; SceneManager.MoveGameObjectToScene(stack.gameObject, scene); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            for (int i = 0; i < 5; i++)
            {
                var photo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                photo.name = "Blank Photo " + (i + 1);
                photo.transform.SetParent(stack, true);
                photo.transform.position = new Vector3(exposurePosition.x - 1.05f + i * .018f, table.max.y + .012f + i * .01f, exposurePosition.z + i * .016f);
                photo.transform.rotation = Quaternion.Euler(0f, -7f + i * 3f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(photo.transform);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigurePrefab()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/photo.mat");
        var shader = Shader.Find("Old Tech/Developing Photo");
        if (!material || !shader) throw new InvalidOperationException("Photo material or developing shader is missing.");
        material.shader = shader;
        material.SetFloat("_Development", 0);
        material.SetColor("_PaperColor", new Color(.88f, .86f, .79f));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (var mesh in prefab.GetComponents<MeshCollider>()) UnityEngine.Object.DestroyImmediate(mesh);
            var collider = Ensure<BoxCollider>(prefab);
            collider.center = Vector3.zero;
            collider.size = new Vector3(10f, .08f, 10f);
            var body = Ensure<Rigidbody>(prefab);
            body.mass = .025f;
            body.linearDamping = .4f;
            body.angularDamping = .8f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var pickup = Ensure<Pickupable>(prefab);
            Set(pickup, s =>
            {
                s.FindProperty("displayName").stringValue = "photo";
                s.FindProperty("holdPositionOffset").vector3Value = new Vector3(.12f, -.13f, .32f);
                s.FindProperty("holdRotationOffset").vector3Value = new Vector3(-78f, -12f, -8f);
            });
            var renderer = prefab.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var photo = Ensure<DevelopingPhoto>(prefab);
            Set(photo, s =>
            {
                s.FindProperty("photoRenderer").objectReferenceValue = renderer;
                s.FindProperty("developmentSeconds").floatValue = 10f;
                SetImages(s.FindProperty("anomalyImages"), "Assets/Textures/PHOTOS/anomaly_photos");
                SetImages(s.FindProperty("normalImages"), "Assets/Textures/PHOTOS/not_anomaly");
            });
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static void SetImages(SerializedProperty property, string folder)
    {
        var images = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();
        if (images.Length == 0) throw new InvalidOperationException("No photo images in " + folder);
        property.arraySize = images.Length;
        for (int i = 0; i < images.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
    }

    private static T Ensure<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component ? component : target.AddComponent<T>();
    }
    private static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var serialized = new SerializedObject(target);
        configure(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static Transform WorldChild(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var child = parent.Find(name);
        if (child) return child;
        child = new GameObject(name).transform;
        child.SetPositionAndRotation(position, rotation);
        child.SetParent(parent, true);
        return child;
    }
}
