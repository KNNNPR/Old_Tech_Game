using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class EndAtmosphereSetup
{
    public const string ScenePath = "Assets/Scenes/THE_END.unity";
    public const string SeaMaterialPath = "Assets/Materials/TheEnd/Blood Sea.mat";
    public const string SkyMaterialPath = "Assets/Materials/TheEnd/Moving Blood Sky.mat";
    private const string MeshPath = "Assets/Meshes/TheEnd/Blood Sea Surface.asset";
    private const string Request = "Temp/EndAtmosphere/ambience-v3.request";
    private static bool applying;
    private static double nextCheck;

    [InitializeOnLoadMethod]
    private static void OnImport()
    {
        EditorApplication.delayCall += ApplyRequested;
        EditorApplication.update += () =>
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            ApplyRequested();
        };
        EditorSceneManager.sceneOpened += (_, __) => ApplyRequested();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyRequested();
        };
    }

    private static void ApplyRequested()
    {
        if (applying || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        applying = true;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Apply();
            File.Delete(Request);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            applying = false;
        }
    }

    [MenuItem("Tools/Punch Card/THE_END/Apply Blood Sea and Moving Sky")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Directory.CreateDirectory("Temp/EndAtmosphere");
        EditorSceneManager.SaveScene(scene, "Temp/EndAtmosphere/THE_END-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Configure(scene);
        Validate(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/EndAtmosphere/ambience-v3-applied.txt", "PASS\nTHE_END saved: low drifting mist, a green CRT beacon and its wave-distorted reflection, individually swaying giant cards, and sparse falling black ash.\nOriginal authored poses, FBX, gameplay colliders, directional lighting and other scenes preserved.\n");
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    public static MeshRenderer Configure(Scene scene)
    {
        var previousActive = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            var sea = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Single(r => r.name == "bloodbath");
            var filter = sea.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) throw new InvalidOperationException("bloodbath needs a mesh.");
            var seaShader = Shader.Find("Old Tech/Blood Sea");
            var skyShader = Shader.Find("Old Tech/Ominous Blood Sky");
            if (!seaShader || !skyShader || ShaderUtil.ShaderHasError(seaShader) || ShaderUtil.ShaderHasError(skyShader)) throw new InvalidOperationException("THE_END atmosphere shaders failed to import.");
            Folder("Assets/Materials/TheEnd");
            Folder("Assets/Meshes/TheEnd");
            var originalSea = sea.sharedMaterial;
            var bloodTexture = TextureFrom(originalSea, "_BaseMap", "_MainTex") ?? AssetDatabase.LoadAssetAtPath<Texture>("Assets/Textures/bloodbath.png");
            var originalSky = RenderSettings.skybox;
            var skyTexture = TextureFrom(originalSky, "_MainTex") ?? AssetDatabase.LoadAssetAtPath<Texture>("Assets/Textures/bloodsky.png");
            if (!bloodTexture || !skyTexture) throw new InvalidOperationException("Existing bloodbath / bloodsky textures are missing.");
            var water = AssetDatabase.LoadAssetAtPath<Material>(SeaMaterialPath);
            if (!water)
            {
                water = new Material(seaShader) { name = "Blood Sea" };
                water.SetTexture("_BaseMap", bloodTexture);
                string property = originalSea && originalSea.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (originalSea && originalSea.HasProperty(property))
                {
                    water.SetTextureScale("_BaseMap", originalSea.GetTextureScale(property));
                    water.SetTextureOffset("_BaseMap", originalSea.GetTextureOffset(property));
                }
                water.SetTexture("_SkyMap", skyTexture);
                AssetDatabase.CreateAsset(water, SeaMaterialPath);
            }
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (!sky)
            {
                sky = new Material(skyShader) { name = "Moving Blood Sky" };
                sky.SetTexture("_MainTex", skyTexture);
                if (originalSky)
                {
                    if (originalSky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", originalSky.GetFloat("_Exposure"));
                    if (originalSky.HasProperty("_Rotation")) sky.SetFloat("_Rotation", originalSky.GetFloat("_Rotation"));
                    // Unity's built-in panoramic sky multiplies its grey default tint by 2.
                    if (originalSky.HasProperty("_Tint")) sky.SetColor("_Tint", originalSky.GetColor("_Tint") * 2f);
                }
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            ConfigureAtmosphere(water, sky);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (!mesh)
            {
                mesh = Subdivide(filter.sharedMesh, 3);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            Undo.RecordObjects(new UnityEngine.Object[] { sea, filter }, "Animate THE_END blood sea");
            sea.sharedMaterial = water;
            filter.sharedMesh = mesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sea);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            RenderSettings.skybox = sky;
            foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                var skybox = camera.GetComponent<Skybox>();
                if (skybox && skybox.material == originalSky)
                {
                    Undo.RecordObject(skybox, "Animate THE_END sky");
                    skybox.material = sky;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(skybox);
                }
            }
            EndSceneAmbienceSetup.Configure(scene, sea);
            return sea;
        }
        finally { if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive); }
    }

    public static void Validate(Scene scene)
    {
        var previousActive = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            var sea = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Single(r => r.name == "bloodbath");
            if (AssetDatabase.GetAssetPath(sea.sharedMaterial) != SeaMaterialPath || !sea.sharedMaterial.GetTexture("_BaseMap")) throw new InvalidOperationException("bloodbath material / texture not assigned.");
            if (AssetDatabase.GetAssetPath(sea.GetComponent<MeshFilter>().sharedMesh) != MeshPath) throw new InvalidOperationException("Blood sea wave mesh not assigned.");
            if (AssetDatabase.GetAssetPath(RenderSettings.skybox) != SkyMaterialPath || !RenderSettings.skybox.GetTexture("_MainTex")) throw new InvalidOperationException("Moving sky not assigned.");
            if (sea.sharedMaterial.GetFloat("_PreviewTime") != -1 || RenderSettings.skybox.GetFloat("_PreviewTime") != -1) throw new InvalidOperationException("Atmosphere animation is frozen at a preview time.");
            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.ExponentialSquared || !Mathf.Approximately(RenderSettings.fogDensity, 0.0065f)) throw new InvalidOperationException("THE_END light distance fog not configured.");
            if (!Mathf.Approximately(RenderSettings.skybox.GetFloat("_Drift"), 0.6f) || RenderSettings.skybox.GetFloat("_Distortion") <= 0) throw new InvalidOperationException("Moving sky needs drift and cloud distortion.");
            if (ShaderUtil.ShaderHasError(sea.sharedMaterial.shader) || ShaderUtil.ShaderHasError(RenderSettings.skybox.shader)) throw new InvalidOperationException("Atmosphere shader compilation error.");
            EndSceneAmbienceSetup.Validate(scene);
        }
        finally { if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive); }
    }

    private static void ConfigureAtmosphere(Material water, Material sky)
    {
        Undo.RecordObjects(new UnityEngine.Object[] { water, sky }, "THE_END fog and sky motion");
        sky.SetFloat("_Drift", 0.6f);
        sky.SetFloat("_Distortion", 0.045f);
        sky.SetFloat("_DistortionSpeed", 0.14f);
        sky.SetFloat("_CloudLayer", 0.24f);
        sky.SetFloat("_PreviewTime", -1);
        water.SetFloat("_SkyRotation", sky.GetFloat("_Rotation"));
        water.SetFloat("_SkyDrift", sky.GetFloat("_Drift"));
        water.SetFloat("_SkyDistortion", sky.GetFloat("_Distortion"));
        water.SetFloat("_SkySpeed", sky.GetFloat("_DistortionSpeed"));
        EditorUtility.SetDirty(water);
        EditorUtility.SetDirty(sky);
        // A small amount close by, gradually veiling the distant cards and horizon.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.18f, 0.018f, 0.028f, 1);
        RenderSettings.fogDensity = 0.0065f;
    }

    private static Texture TextureFrom(Material material, params string[] properties)
    {
        if (material) foreach (string property in properties) if (material.HasProperty(property) && material.GetTexture(property)) return material.GetTexture(property);
        return null;
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Mesh Subdivide(Mesh source, int iterations)
    {
        // An independent mesh preserves the FBX, its UV seams and the flat gameplay collider.
        if (source.subMeshCount != 1) throw new InvalidOperationException("Expected the single-material bloodbath mesh.");
        var vertices = new List<Vector3>(source.vertices);
        var uv = new List<Vector2>(source.uv);
        var normals = new List<Vector3>(source.normals);
        if (uv.Count != vertices.Count || normals.Count != vertices.Count) throw new InvalidOperationException("bloodbath UVs or normals are missing.");
        var triangles = source.triangles;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var edges = new Dictionary<ulong, int>();
            int Midpoint(int a, int b)
            {
                ulong key = ((ulong)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                if (edges.TryGetValue(key, out int index)) return index;
                index = vertices.Count;
                edges.Add(key, index);
                vertices.Add((vertices[a] + vertices[b]) * 0.5f);
                uv.Add((uv[a] + uv[b]) * 0.5f);
                normals.Add((normals[a] + normals[b]).normalized);
                return index;
            }
            var next = new int[triangles.Length * 4];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a), j = i * 4;
                int[] split = { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca };
                Array.Copy(split, 0, next, j, 12);
            }
            triangles = next;
        }
        var mesh = new Mesh { name = "Blood Sea Surface", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetNormals(normals);
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        var bounds = mesh.bounds;
        bounds.Expand(0.05f); // 2.5 metres around the 100x FBX scale covers the full swell.
        mesh.bounds = bounds;
        return mesh;
    }
}
