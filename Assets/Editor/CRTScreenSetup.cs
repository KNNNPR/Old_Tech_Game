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
    public static class CRTScreenSetup
    {
        const string ShaderName = "Old Tech/CRT Screen";
        const string MaterialPath = "Assets/Materials/CRTScreen_Cube002.mat";
        const string ProfilePath = "Assets/Settings/CRTScreenGlow.asset";

        static MeshRenderer FindScreen()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var screens = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r => string.Equals(r.name, "Cube.002", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (screens.Length != 1) throw new InvalidOperationException($"Expected one Cube.002, found {screens.Length}.");
            return screens[0];
        }

        [MenuItem("Tools/Old Tech/Apply Animated CRT to Cube.002")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply the CRT in Edit mode so the scene can be saved.");
            var renderer = FindScreen();
            var shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("CRT shader has not compiled successfully.");
            var materials = renderer.sharedMaterials;
            int slot = Array.FindIndex(materials, m => m && m.HasProperty("_BaseMap") &&
                m.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/comp.png"));
            if (slot < 0) throw new InvalidOperationException("Cube.002 has no material using the comp.png display atlas.");
            var source = materials[slot];
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CRTScreen_Cube002", enableInstancing = true };
                material.CopyPropertiesFromMaterial(source);
                material.SetFloat("_PreviewTime", -1);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Undo.RecordObject(renderer, "Apply animated CRT material");
            materials[slot] = material;
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CRTScreenGlow";
                AssetDatabase.CreateAsset(profile, ProfilePath);
                var bloom = profile.Add<Bloom>(false);
                bloom.name = "CRT Bloom";
                bloom.threshold.Override(0.85f);
                bloom.intensity.Override(0.32f);
                bloom.scatter.Override(0.58f);
                bloom.clamp.Override(8);
                bloom.maxIterations.Override(6);
                bloom.highQualityFiltering.Override(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                EditorUtility.SetDirty(profile);
            }
            var scene = renderer.gameObject.scene;
            var volume = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Volume>(true))
                .FirstOrDefault(v => v.sharedProfile == profile);
            if (volume == null)
            {
                var glowObject = new GameObject("CRT Screen Glow");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(glowObject, scene);
                Undo.RegisterCreatedObjectUndo(glowObject, "Add CRT bloom");
                volume = Undo.AddComponent<Volume>(glowObject);
                volume.isGlobal = true;
                volume.priority = 20;
                volume.weight = 1;
                volume.sharedProfile = profile;
            }
            foreach (var camera in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true)))
            {
                Undo.RecordObject(camera, "Enable CRT glow rendering");
                camera.allowHDR = true;
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (!data) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                Undo.RecordObject(data, "Enable CRT bloom");
                data.renderPostProcessing = true;
                data.volumeLayerMask |= 1 << volume.gameObject.layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = material;
            SceneView.RepaintAll();
            Debug.Log($"[CRTScreen] Applied to {renderer.name}, slot {slot}. Animated display and Bloom are enabled.");
        }

        [MenuItem("Tools/Old Tech/Validate Animated CRT")]
        public static async void Validate()
        {
            var renderer = FindScreen();
            var material = renderer.sharedMaterials.FirstOrDefault(m => m && m.shader.name == ShaderName);
            if (!material) throw new InvalidOperationException("CRT material is not assigned to Cube.002.");
            string output = "Temp/CRTScreen";
            Directory.CreateDirectory(output);
            File.WriteAllText(output + "/validation.txt", "Validation in progress...");
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            float previewTime = material.GetFloat("_PreviewTime");
            var captureObject = new GameObject("CRT Validation Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = captureObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 42;
            camera.nearClipPlane = 0.03f;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            var authoredCamera = renderer.gameObject.scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>())
                .Where(c => c.enabled).OrderBy(c => c.depth).LastOrDefault();
            Vector3 direction = authoredCamera ? (authoredCamera.transform.position - renderer.bounds.center).normalized : Vector3.forward;
            camera.transform.position = renderer.bounds.center + direction * renderer.bounds.size.magnitude * 1.4f;
            camera.transform.LookAt(renderer.bounds.center);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!profile || !profile.TryGet<Bloom>(out var bloom)) throw new InvalidOperationException("CRT bloom profile is missing.");
            float bloomIntensity = bloom.intensity.value;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                await Task.Delay(500);
                material.SetFloat("_PreviewTime", 0);
                var first = Capture(camera, output + "/screen-t0.png");
                material.SetFloat("_PreviewTime", 2);
                var second = Capture(camera, output + "/screen-t2.png");
                bloom.intensity.value = 0;
                await Task.Delay(150);
                var noBloom = Capture(camera, output + "/screen-no-bloom.png");
                int animated = ChangedPixels(first, second);
                int glowing = ChangedPixels(second, noBloom);
                if (animated < 100) throw new InvalidOperationException("The screen image did not animate.");
                if (glowing < 100) throw new InvalidOperationException("Bloom is not visible in the rendered frame.");
                if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("CRT shader has compilation errors.");
                var report = new StringBuilder();
                report.AppendLine($"PASS - Unity {Application.unityVersion}");
                report.AppendLine($"Target: {renderer.name}; material: {AssetDatabase.GetAssetPath(material)}");
                report.AppendLine($"Animated pixels between 0s and 2s: {animated}");
                report.AppendLine($"Pixels affected by Bloom: {glowing}");
                report.AppendLine($"Mesh: {renderer.GetComponent<MeshFilter>().sharedMesh.name}; bounds: {renderer.bounds}");
                foreach (var message in ShaderUtil.GetShaderMessages(material.shader)) report.AppendLine(message.message);
                File.WriteAllText(output + "/validation.txt", report.ToString());
                Debug.Log("[CRTScreen] " + report);
            }
            catch (Exception error)
            {
                File.WriteAllText(output + "/validation.txt", "FAIL\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                material.SetFloat("_PreviewTime", previewTime);
                bloom.intensity.value = bloomIntensity;
                ShaderUtil.allowAsyncCompilation = asynchronous;
                Object.DestroyImmediate(captureObject);
                SceneView.RepaintAll();
            }
        }

        static Color32[] Capture(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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
            }
        }

        static int ChangedPixels(Color32[] first, Color32[] second)
        {
            int changed = 0;
            for (int i = 0; i < first.Length; ++i)
                if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 6) ++changed;
            return changed;
        }
    }
}
