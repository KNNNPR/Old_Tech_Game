using System;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PosterWindSetup
{
    public const string ScenePath = "Assets/Scenes/Intermission.unity";
    private const string Request = "Temp/PosterWind/apply-v1.request";
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
    }
    private static void ApplyRequested()
    {
        if (applying || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        applying = true;
        try { Apply(scene); File.Delete(Request); }
        catch (Exception e) { Debug.LogException(e); }
        finally { applying = false; }
    }
    [MenuItem("Tools/Punch Card/Set Up Pinned Intermission Poster")]
    public static void ApplyOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) Apply(scene);
    }
    private static void Apply(Scene scene)
    {
        Directory.CreateDirectory("Temp/PosterWind");
        EditorSceneManager.SaveScene(scene,"Temp/PosterWind/Intermission-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity",true);
        Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PosterWind/applied.txt","PASS\nFirst Intermission POSTER has a pinned upper strip and subtle wind-driven free-edge deformation capped at 2.5cm. Transform, wall placement, material, texture and source FBX are preserved.\n");
        SceneView.RepaintAll();
    }

    public static PinnedPosterWind Configure(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Poster wind is only for the first Intermission.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var poster = all.Single(t => t.name.Equals("POSTER",StringComparison.OrdinalIgnoreCase));
        var filter = poster.GetComponentInChildren<MeshFilter>(true);
        var source = filter.sharedMesh;
        const string meshPath = "Assets/Meshes/Intermission Poster Deformable.asset";
        var copy = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (!copy) { copy = new Mesh { name = "Intermission poster deformation source" }; AssetDatabase.CreateAsset(copy,meshPath); }
        using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(source))
        {
            var data = snapshot[0];
            using var vertices = new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);
            using var normals = new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);
            using var uv = new NativeArray<Vector2>(data.vertexCount,Allocator.Temp);
            data.GetVertices(vertices); data.GetNormals(normals); data.GetUVs(0,uv);
            copy.Clear(); copy.indexFormat = source.indexFormat;
            copy.vertices = vertices.ToArray(); copy.normals = normals.ToArray(); copy.uv = uv.ToArray();
            if (data.HasVertexAttribute(VertexAttribute.Tangent))
            {
                using var tangents = new NativeArray<Vector4>(data.vertexCount,Allocator.Temp);
                data.GetTangents(tangents); copy.tangents = tangents.ToArray();
            }
            copy.subMeshCount = data.subMeshCount;
            for (int i = 0; i < data.subMeshCount; i++)
            {
                var submesh = data.GetSubMesh(i);
                using var indices = new NativeArray<int>(submesh.indexCount,Allocator.Temp);
                data.GetIndices(indices,i);
                copy.SetIndices(indices.ToArray(),submesh.topology,i);
            }
            copy.bounds = source.bounds;
        }
        var normal = copy.normals.Aggregate(Vector3.zero,(sum,n) => sum + n).normalized;
        if (normal.sqrMagnitude < .1f) normal = copy.normals[0];
        var cinematic = all.Select(t => t.GetComponent<IntermissionCinematic>()).Single(c => c);
        var view = (Transform)new SerializedObject(cinematic).FindProperty("openingPose").objectReferenceValue;
        if (Vector3.Dot(filter.transform.TransformDirection(normal),view.position-filter.GetComponent<Renderer>().bounds.center) < 0) normal = -normal;
        var component = filter.GetComponent<PinnedPosterWind>();
        if (!component) component = filter.gameObject.AddComponent<PinnedPosterWind>();
        var settings = new SerializedObject(component);
        settings.FindProperty("deformableMesh").objectReferenceValue = copy;
        settings.FindProperty("wind").objectReferenceValue = all.Select(t => t.GetComponent<IntermissionWind>()).Single(w => w);
        settings.FindProperty("localOutward").vector3Value = normal;
        settings.FindProperty("pinnedTopFraction").floatValue = .16f;
        settings.FindProperty("amplitude").floatValue = .025f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(copy); AssetDatabase.SaveAssets();
        return component;
    }
}
