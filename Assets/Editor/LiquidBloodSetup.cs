using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace OldTech.Editor
{
    public static class LiquidBloodSetup
    {
        const string ShaderName = "Old Tech/Liquid Blood";
        const string MaterialFolder = "Assets/Materials/Liquids";

        static Renderer[] FindTargets()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .Where(r => string.Equals(r.name, "bucket", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.name, "blood", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        [MenuItem("Tools/Old Tech/Apply Liquid Blood to Bucket and Floor")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply liquid blood in Edit mode to save the scene.");
            var targets = FindTargets();
            var bucket = targets.Single(r => string.Equals(r.name, "bucket", StringComparison.OrdinalIgnoreCase));
            var puddle = targets.Single(r => string.Equals(r.name, "blood", StringComparison.OrdinalIgnoreCase));
            var shader = Shader.Find(ShaderName);
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Liquid blood shader has not compiled successfully.");
            Directory.CreateDirectory("Temp/LiquidBlood");
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                if (SceneView.lastActiveSceneView) CaptureSceneView("Temp/LiquidBlood/before.png");
                if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Liquids");
                var source = puddle.sharedMaterials[0];
                var metal = bucket.sharedMaterials[0];
                foreach (var renderer in targets)
                {
                    bool isBucket = renderer == bucket;
                    string path = MaterialFolder + (isBucket ? "/Blood_Bucket.mat" : "/Blood_Floor.mat");
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material)
                    {
                        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path), enableInstancing = true };
                        string property = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        if (source.HasProperty(property))
                        {
                            material.SetTexture("_BaseMap", source.GetTexture(property));
                            material.SetTextureScale("_BaseMap", source.GetTextureScale(property));
                            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(property));
                        }
                        material.SetFloat("_RippleStrength", isBucket ? 0.055f : 0.025f);
                        material.SetFloat("_RippleSpeed", isBucket ? 0.65f : 0.4f);
                        material.SetFloat("_Smoothness", isBucket ? 0.94f : 0.91f);
                        var flowDirection = puddle.bounds.center - bucket.bounds.center;
                        flowDirection.y = 0;
                        flowDirection.Normalize();
                        material.SetVector("_FlowDirection", flowDirection);
                        material.SetFloat("_FlowSpeed", isBucket ? 0.015f : 0.03f);
                        material.SetFloat("_SpreadStrength", isBucket ? 0 : 0.025f);
                        material.SetFloat("_PreviewTime", -1);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    var materials = renderer.sharedMaterials;
                    int slot = isBucket ? Array.FindIndex(materials, m => m == source || (m && m.shader.name == ShaderName)) : 0;
                    if (slot < 0 || (isBucket && slot == 0)) throw new InvalidOperationException("Cannot identify the blood-only material slot on the bucket.");
                    Undo.RecordObject(renderer, "Apply liquid blood material");
                    materials[slot] = material;
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    Debug.Log($"[LiquidBlood] Applied {material.name} to {renderer.name}, slot {slot}.");
                }
                if (bucket.sharedMaterials[0] != metal) throw new InvalidOperationException("Bucket metal material changed unexpectedly.");
                AssetDatabase.SaveAssets();
                var scene = bucket.gameObject.scene;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Blood_Floor.mat");
                SceneView.RepaintAll();
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
        }

        [MenuItem("Tools/Old Tech/Validate Liquid Blood")]
        public static async void Validate()
        {
            var targets = FindTargets();
            if (targets.Length != 2) throw new InvalidOperationException("Expected bucket and Blood in the active scene.");
            var materials = targets.SelectMany(r => r.sharedMaterials).Where(m => m && m.shader.name == ShaderName).Distinct().ToArray();
            if (materials.Length != 2) throw new InvalidOperationException("Expected separate bucket and floor blood materials.");
            Directory.CreateDirectory("Temp/LiquidBlood");
            File.WriteAllText("Temp/LiquidBlood/validation.txt", "Validation in progress...");
            var times = materials.Select(m => m.GetFloat("_PreviewTime")).ToArray();
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                await Task.Delay(500);
                foreach (var material in materials) material.SetFloat("_PreviewTime", 0);
                var first = CaptureSceneView("Temp/LiquidBlood/liquid-t0.png");
                foreach (var material in materials) material.SetFloat("_PreviewTime", 2);
                var second = CaptureSceneView("Temp/LiquidBlood/liquid-t2.png");
                int changed = 0;
                for (int i = 0; i < first.Length; ++i)
                    if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 6) ++changed;
                if (changed < 20) throw new InvalidOperationException("No visible moving liquid reflections in the capture.");
                var shader = Shader.Find(ShaderName);
                if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Liquid blood shader has compilation errors.");
                var report = new StringBuilder();
                report.AppendLine("PASS - Unity " + Application.unityVersion);
                report.AppendLine("Scene: " + targets[0].gameObject.scene.path);
                foreach (var target in targets)
                    report.AppendLine(target.name + ": " + string.Join(", ", target.sharedMaterials.Select(m => AssetDatabase.GetAssetPath(m))));
                report.AppendLine($"Pixels changed by liquid ripples over 2 seconds: {changed}");
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) report.AppendLine(message.message);
                File.WriteAllText("Temp/LiquidBlood/validation.txt", report.ToString());
                Debug.Log("[LiquidBlood] " + report);
            }
            catch (Exception error)
            {
                File.WriteAllText("Temp/LiquidBlood/validation.txt", "FAIL\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                for (int i = 0; i < materials.Length; ++i) materials[i].SetFloat("_PreviewTime", times[i]);
                ShaderUtil.allowAsyncCompilation = asynchronous;
                SceneView.RepaintAll();
            }
        }

        static Color32[] CaptureSceneView(string path)
        {
            var view = SceneView.lastActiveSceneView;
            if (!view) throw new InvalidOperationException("A Scene view is required to capture the blood surfaces.");
            var captureObject = new GameObject("Liquid Blood Validation Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = captureObject.AddComponent<Camera>();
            camera.CopyFrom(view.camera);
            camera.enabled = false;
            camera.cameraType = CameraType.Game;
            camera.allowHDR = true;
            camera.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            var target = RenderTexture.GetTemporary(1280, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(captureObject);
            }
        }

        [MenuItem("Tools/Old Tech/Inspect Blood Surfaces")]
        public static void Inspect()
        {
            var report = new StringBuilder();
            report.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            foreach (var renderer in FindTargets())
            {
                report.AppendLine($"Object: {renderer.name}; bounds: {renderer.bounds}; position: {renderer.transform.position}; rotation: {renderer.transform.eulerAngles}");
                for (int i = 0; i < renderer.sharedMaterials.Length; ++i)
                {
                    var material = renderer.sharedMaterials[i];
                    report.AppendLine($"  Slot {i}: {material.name}; shader: {material.shader.name}; path: {AssetDatabase.GetAssetPath(material)}; color: {material.color}");
                    foreach (string property in material.GetTexturePropertyNames())
                        if (material.GetTexture(property)) report.AppendLine($"    {property}: {AssetDatabase.GetAssetPath(material.GetTexture(property))}");
                }
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;
                report.AppendLine($"  Mesh: {mesh.name}; vertices: {mesh.vertexCount}; submeshes: {mesh.subMeshCount}");
                try
                {
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    var uv = mesh.uv;
                    var meshData = new StringBuilder("vertex,x,y,z,nx,ny,nz,u,v\n");
                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        var p = renderer.transform.TransformPoint(vertices[i]);
                        var n = renderer.transform.TransformDirection(normals[i]);
                        meshData.AppendLine(FormattableString.Invariant($"{i},{p.x},{p.y},{p.z},{n.x},{n.y},{n.z},{uv[i].x},{uv[i].y}"));
                    }
                    Directory.CreateDirectory("Temp/LiquidBlood");
                    File.WriteAllText($"Temp/LiquidBlood/{renderer.name}-vertices.csv", meshData.ToString());
                    for (int i = 0; i < mesh.subMeshCount; ++i)
                    {
                        var indices = mesh.GetTriangles(i);
                        report.AppendLine($"  Submesh {i}: {indices.Length / 3} triangles; vertices: {string.Join(",", indices.Distinct())}");
                    }
                }
                catch (Exception error) { report.AppendLine(error.Message); }
            }
            Directory.CreateDirectory("Temp/LiquidBlood");
            File.WriteAllText("Temp/LiquidBlood/inspection.txt", report.ToString());
            Debug.Log("[LiquidBlood] " + report);
        }
    }
}
