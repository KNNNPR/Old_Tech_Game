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
    /// <summary>Installs persistent assets; no runtime material replacement is needed.</summary>
    public static class RetroLookSetup
    {
        const string ModelPath = "Assets/Models/SCENE.fbx";
        const string MaterialFolder = "Assets/Materials/Retro";
        const string PostPath = MaterialFolder + "/RetroDither.mat";
        const string FeatureName = "Retro Dither + Palette";
        static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("Tools/Old Tech/Apply Retro Dither + Cel")]
        public static void Apply()
        {
            var cel = RequireShader("Old Tech/Retro Cel Lit");
            var dither = RequireShader("Old Tech/Retro Dither");
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var post = AssetDatabase.LoadAssetAtPath<Material>(PostPath);
            if (post == null)
            {
                post = new Material(dither) { name = "RetroDither" };
                AssetDatabase.CreateAsset(post, PostPath);
            }

            foreach (string path in RendererPaths)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null) throw new InvalidOperationException("Missing URP renderer: " + path);
                var feature = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>()
                    .FirstOrDefault(f => f.name == FeatureName);
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    renderer.rendererFeatures.Add(feature);
                }
                feature.passMaterial = post;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feature.fetchColorBuffer = true;
                feature.requirements = ScriptableRenderPassInput.None;
                feature.passIndex = 0;
                feature.bindDepthStencilAttachment = false;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);

                // Keep URP's recovery map consistent with its serialized feature list.
                var serialized = new SerializedObject(renderer);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize = renderer.rendererFeatures.Count;
                for (int i = 0; i < map.arraySize; ++i)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long id);
                    map.GetArrayElementAtIndex(i).longValue = id;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
            }

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing scene model: " + ModelPath);
            var sources = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().ToArray();
            int count = 0;
            foreach (var source in sources)
            {
                string safeName = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string path = MaterialFolder + "/" + safeName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = ConvertMaterial(source, cel);
                    AssetDatabase.CreateAsset(material, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
                ++count;
            }
            // Subsequent runs can have no embedded materials after external remapping.
            if (count > 0) importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log($"[RetroLook] Applied cel materials ({count}) and dithering to PC + Mobile renderers.");
        }

        static Material ConvertMaterial(Material source, Shader shader)
        {
            string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            var texture = source.HasProperty(textureProperty) ? source.GetTexture(textureProperty) : null;
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var material = new Material(shader) { name = source.name, enableInstancing = true };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            if (source.HasProperty(textureProperty))
            {
                material.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
            }
            if (source.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
            if (source.HasProperty("_EmissionMap") && source.GetTexture("_EmissionMap") != null)
                material.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
            if (source.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_AlphaClip", 1);
                if (source.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            }
            if (source.HasProperty("_Cull")) material.SetFloat("_Cull", source.GetFloat("_Cull"));
            Debug.Log($"[RetroLook] {source.name}: {source.shader.name}, texture={texture?.name ?? "none"}, tint={color}");
            return material;
        }

        static Shader RequireShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Shader not imported: " + name);
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader has compiler errors: " + name);
            return shader;
        }

        [MenuItem("Tools/Old Tech/Select Dither Settings")]
        public static void SelectSettings()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(PostPath);
        }

        [MenuItem("Tools/Old Tech/Inspect Current Scene")]
        public static void Inspect()
        {
            var report = new StringBuilder();
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>())
            {
                report.AppendLine($"Material: {material.name}; shader={material.shader.name}; color={material.color}");
                foreach (string property in material.GetTexturePropertyNames())
                    report.AppendLine($"  {property}: {AssetDatabase.GetAssetPath(material.GetTexture(property))}");
            }
            var scene = Application.isBatchMode
                ? EditorSceneManager.OpenScene("Assets/scene_1.unity", OpenSceneMode.Single)
                : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                report.AppendLine($"Root: {root.name}; position={root.transform.position}");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    report.AppendLine($"  {renderer.name}; bounds={renderer.bounds}; materials={string.Join(",", renderer.sharedMaterials.Select(m => m ? m.name : "null"))}");
                foreach (var camera in root.GetComponentsInChildren<Camera>())
                    report.AppendLine($"  Camera: {camera.name}; position={camera.transform.position}; rotation={camera.transform.eulerAngles}");
                foreach (var light in root.GetComponentsInChildren<Light>())
                    report.AppendLine($"  Light: {light.name}; type={light.type}; intensity={light.intensity}");
            }
            Directory.CreateDirectory("Temp/RetroLook");
            File.WriteAllText("Temp/RetroLook/inspection.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        public static void ApplyAndValidate()
        {
            Apply();
            Validate();
        }

        [MenuItem("Tools/Old Tech/Validate Current Scene")]
        public static async void Validate()
        {
            Directory.CreateDirectory("Temp/RetroLook");
            File.WriteAllText("Temp/RetroLook/validation.txt", "Validation in progress...\n");
            var scene = Application.isBatchMode
                ? EditorSceneManager.OpenScene("Assets/scene_1.unity", OpenSceneMode.Single)
                : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var renderers = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Renderer>()).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Scene has no renderers.");
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader.name != "Old Tech/Retro Cel Lit")
                        throw new InvalidOperationException("Cel material not applied to " + renderer.name);

            var camera = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>())
                .Where(c => c.enabled && c.gameObject.activeInHierarchy).OrderBy(c => c.depth).LastOrDefault();
            if (camera == null) throw new InvalidOperationException("Scene has no main camera.");
            // Move a temporary capture camera only; the scene's camera is left as authored.
            var captureObject = new GameObject("Retro Look Validation Camera") { hideFlags = HideFlags.HideAndDontSave };
            var capture = captureObject.AddComponent<Camera>();
            capture.CopyFrom(camera);
            capture.enabled = false;
            var additional = capture.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true;
            additional.antialiasing = AntialiasingMode.None;
            capture.clearFlags = CameraClearFlags.SolidColor;
            capture.backgroundColor = Color.black;

            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float radius = Mathf.Max(bounds.extents.magnitude, 1);
            capture.nearClipPlane = 0.03f;
            capture.farClipPlane = radius * 20;
            if (Application.isBatchMode)
            {
                capture.transform.position = bounds.center + new Vector3(0.7f, 0.45f, -1f).normalized * radius * 1.9f;
                capture.transform.LookAt(bounds.center);
            }
            else capture.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            int quality = QualitySettings.GetQualityLevel();
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var post = AssetDatabase.LoadAssetAtPath<Material>(PostPath);
                float intensity = post.GetFloat("_Intensity");
                try
                {
                    QualitySettings.SetQualityLevel(1, true);
                    SceneView.RepaintAll();
                    EditorApplication.QueuePlayerLoopUpdate();
                    await Task.Delay(700);
                    post.SetFloat("_Intensity", 0);
                    Capture(capture, "Temp/RetroLook/cel-only.png");
                    post.SetFloat("_Intensity", intensity);
                    Capture(capture, "Temp/RetroLook/retro-applied.png");
                    QualitySettings.SetQualityLevel(0, true);
                    SceneView.RepaintAll();
                    EditorApplication.QueuePlayerLoopUpdate();
                    // Pipeline changes are processed by Unity's next render loop.
                    await Task.Delay(700);
                    Capture(capture, "Temp/RetroLook/retro-mobile.png");
                }
                finally { post.SetFloat("_Intensity", intensity); }
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/RetroLook/validation.txt", "FAIL\n" + exception);
                Debug.LogException(exception);
                return;
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                Object.DestroyImmediate(captureObject);
            }

            foreach (string name in new[] { "Old Tech/Retro Cel Lit", "Old Tech/Retro Dither" })
            {
                var shader = RequireShader(name);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    Debug.Log($"[RetroLook] Shader diagnostic {name}: {message.severity} {message.message} ({message.file}:{message.line})");
            }
            foreach (string path in RendererPaths)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().Count(f => f.name == FeatureName && f.isActive && f.passMaterial) != 1)
                    throw new InvalidOperationException("Expected one active dither feature on " + path);
            }
            File.WriteAllText("Temp/RetroLook/validation.txt", $"PASS\nUnity {Application.unityVersion}\nCel applied to {renderers.Length} renderers.\nPC deferred and Mobile forward captures rendered.\nBoth shaders compiled without errors.\n");
            Debug.Log("[RetroLook] VALIDATION PASSED");
        }

        static void Capture(Camera camera, string path)
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
                int visible = image.GetPixels32().Count(c => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) > 8);
                if (visible < target.width * target.height / 100)
                    throw new InvalidOperationException("Capture is effectively blank: " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
