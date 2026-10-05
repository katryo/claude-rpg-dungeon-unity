using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HD2DRPG.EditorTools
{
    /// <summary>
    /// One-click (and automatic on first import) project configuration:
    ///  - creates a URP pipeline asset + Forward+ renderer with post-processing and assigns it
    ///  - creates template materials under Resources so player builds keep the shader variants
    ///  - switches to Linear color space
    ///  - creates Assets/Scenes/Main.unity and adds it to the build settings
    /// </summary>
    [InitializeOnLoad]
    public static class HD2DProjectSetup
    {
        const string SettingsDir = "Assets/HD2DRPG/Settings";
        const string MaterialsDir = "Assets/HD2DRPG/Resources/HD2D/Materials";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PipelinePath = SettingsDir + "/HD2D_URP.asset";
        const string RendererPath = SettingsDir + "/HD2D_URP_Renderer.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        static HD2DProjectSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool pipelineOk = GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset;
            bool materialsOk = File.Exists(MaterialsDir + "/HD2D_SpriteLit.mat");
            bool sceneOk = File.Exists(ScenePath);
            if (pipelineOk && materialsOk && sceneOk) return;
            Debug.Log("[HD-2D RPG] First-time project setup...");
            Setup(false);
            // First time only: put the castle and the data assets into the project so they can be edited.
            if (!sceneOk && File.Exists(ScenePath) && Shader.Find("Universal Render Pipeline/Lit") != null)
                HD2DSceneBaker.BakeAll(false);
        }

        [MenuItem("HD-2D RPG/Run Project Setup", priority = 0)]
        public static void SetupFromMenu() => Setup(true);

        [MenuItem("HD-2D RPG/Open Main Scene", priority = 1)]
        public static void OpenMainScene()
        {
            if (!File.Exists(ScenePath)) Setup(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Setup(bool interactive)
        {
            EnsureFolder(SettingsDir);
            EnsureFolder(MaterialsDir);
            var pipeline = SetupPipeline();
            SetupMaterials();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SetupScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HD-2D RPG] Setup complete. Pipeline: " + (pipeline != null ? AssetDatabase.GetAssetPath(pipeline) : "none") +
                      ". Open Assets/Scenes/Main.unity and press Play.");
            if (interactive)
                EditorUtility.DisplayDialog("HD-2D RPG", "Project configured for URP.\n\nOpen Assets/Scenes/Main.unity and press Play.", "OK");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ URP
        static UniversalRenderPipelineAsset SetupPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                if (renderer.postProcessData == null)
                    renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                renderer.renderingMode = RenderingMode.ForwardPlus; // dozens of torches and windows
                EditorUtility.SetDirty(renderer);

                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.shadowDistance = 70f;
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            return pipeline;
        }

        // ------------------------------------------------------------------ materials
        static void SetupMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit == null || unlit == null || particles == null)
            {
                Debug.LogWarning("[HD-2D RPG] URP shaders not found yet (package still importing?). Run 'HD-2D RPG/Run Project Setup' again later.");
                return;
            }

            SaveMaterial("HD2D_Lit", new Material(lit), m =>
            {
                m.SetFloat("_Smoothness", 0.15f);
                m.SetFloat("_Metallic", 0f);
            });
            SaveMaterial("HD2D_SpriteLit", new Material(lit), m =>
            {
                Mats.ConfigureCutout(m);
                Mats.SetEmission(m, null, Color.white * 0.28f);
            });
            SaveMaterial("HD2D_UnlitCutout", new Material(unlit), m =>
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });
            SaveMaterial("HD2D_Additive", new Material(particles), Mats.ConfigureAdditive);
        }

        static void SaveMaterial(string name, Material m, System.Action<Material> configure)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                configure(existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(m);
                return;
            }
            m.name = name;
            configure(m);
            AssetDatabase.CreateAsset(m, path);
        }

        // ------------------------------------------------------------------ scene
        static void SetupScene()
        {
            EnsureFolder("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var active = SceneManager.GetActiveScene();
                bool canReplace = !active.isDirty && (string.IsNullOrEmpty(active.path) || active.path.Contains("SampleScene"));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, canReplace ? NewSceneMode.Single : NewSceneMode.Additive);
                var go = new GameObject("HD2D RPG");
                go.AddComponent<Game>();
                SceneManager.MoveGameObjectToScene(go, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!canReplace) EditorSceneManager.CloseScene(scene, true);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
