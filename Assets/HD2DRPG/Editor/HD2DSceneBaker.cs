using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HD2DRPG.EditorTools
{
    /// <summary>
    /// Turns the code-generated content into ordinary, editable Unity content:
    ///  - "Bake Data Assets": characters, skills, equipment, items, enemies and encounters become
    ///    ScriptableObject assets (Assets/HD2DRPG/Data + Resources/HD2D/GameData.asset)
    ///  - "Bake Castle Into Scene": the castle, both battle arenas and the moonlight are built in
    ///    edit mode and saved into Assets/Scenes/Main.unity; their meshes, materials and textures
    ///    are written to Assets/HD2DRPG/Baked so they can be inspected and changed
    /// At runtime the game uses whatever is baked and only generates what is missing.
    /// </summary>
    public static class HD2DSceneBaker
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string BakedDir = "Assets/HD2DRPG/Baked";
        const string DataDir = "Assets/HD2DRPG/Data";
        const string RegistryPath = "Assets/HD2DRPG/Resources/HD2D/GameData.asset";
        const string ProfilePath = "Assets/HD2DRPG/Resources/HD2D/HD2D_PostFX.asset";

        // ================================================================== menus
        [MenuItem("HD-2D RPG/Bake Everything (Data + Scene)", priority = 20)]
        static void BakeEverythingMenu()
        {
            if (!EditorUtility.DisplayDialog("Bake Everything",
                    "This will (re)create the data assets from the built-in defaults and rebuild the castle in " + ScenePath +
                    ".\n\nExisting data assets, the baked castle and everything in " + BakedDir + " will be replaced.",
                    "Bake", "Cancel")) return;
            BakeData(false);
            BakeScene(false);
        }

        [MenuItem("HD-2D RPG/Bake Data Assets", priority = 21)]
        static void BakeDataMenu()
        {
            if (File.Exists(RegistryPath) &&
                !EditorUtility.DisplayDialog("Bake Data Assets",
                    "Data assets already exist. Overwrite them with the built-in defaults from Database.cs?\n\nAny edits you made to them will be lost.",
                    "Overwrite", "Cancel")) return;
            BakeData(true);
        }

        [MenuItem("HD-2D RPG/Bake Castle Into Scene", priority = 22)]
        static void BakeSceneMenu()
        {
            bool exists = Object.FindAnyObjectByType<CastleRoot>() != null || AssetDatabase.IsValidFolder(BakedDir);
            if (exists && !EditorUtility.DisplayDialog("Bake Castle Into Scene",
                    "The castle, battle arenas and moonlight in " + ScenePath + " will be rebuilt from CastleMap.cs, and " +
                    BakedDir + " will be regenerated.\n\nManual edits to the baked castle will be lost.",
                    "Rebake", "Cancel")) return;
            BakeScene(true);
        }

        public static void BakeAll(bool interactive)
        {
            if (!File.Exists(RegistryPath)) BakeData(false);
            BakeScene(interactive);
        }

        // ================================================================== data
        public static void BakeData(bool interactive)
        {
            Database.BuildDefaults();
            EnsureFolder(DataDir);
            var reg = ScriptableObject.CreateInstance<GameDataAsset>();
            reg.StartLevel = Database.StartLevel;
            reg.StartGold = Database.StartGold;
            reg.StartItems.AddRange(Database.StartItems);
            reg.StartEquipment.AddRange(Database.StartEquipment);

            foreach (var id in Database.PartyOrder)
                reg.Characters.Add(Save<CharacterAsset>("Characters", id, a => a.Def = Database.Characters[id]));
            foreach (var kv in Database.Skills) reg.Skills.Add(Save<SkillAsset>("Skills", kv.Key, a => a.Def = kv.Value));
            foreach (var kv in Database.Equips) reg.Equipment.Add(Save<EquipAsset>("Equipment", kv.Key, a => a.Def = kv.Value));
            foreach (var kv in Database.Items) reg.Items.Add(Save<ItemAsset>("Items", kv.Key, a => a.Def = kv.Value));
            foreach (var kv in Database.Enemies) reg.Enemies.Add(Save<EnemyAsset>("Enemies", kv.Key, a => a.Def = kv.Value));
            foreach (var kv in Database.Encounters) reg.Encounters.Add(new EncounterDef { Id = kv.Key, Enemies = kv.Value });

            EnsureFolder(Path.GetDirectoryName(RegistryPath).Replace('\\', '/'));
            AssetDatabase.DeleteAsset(RegistryPath);
            AssetDatabase.CreateAsset(reg, RegistryPath);
            AssetDatabase.SaveAssets();
            Database.Reset(); // next use loads from the assets
            Debug.Log("[HD-2D RPG] Data assets written to " + DataDir + " (registry: " + RegistryPath + ").");
            if (interactive)
            {
                Selection.activeObject = reg;
                EditorGUIUtility.PingObject(reg);
            }
        }

        static T Save<T>(string folder, string id, System.Action<T> fill) where T : ScriptableObject
        {
            string dir = DataDir + "/" + folder;
            EnsureFolder(dir);
            string path = dir + "/" + id + ".asset";
            AssetDatabase.DeleteAsset(path);
            var a = ScriptableObject.CreateInstance<T>();
            fill(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ================================================================== scene
        public static void BakeScene(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[HD-2D RPG] Exit Play Mode before baking.");
                return;
            }
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) ||
                !File.Exists("Assets/HD2DRPG/Resources/HD2D/Materials/HD2D_SpriteLit.mat"))
                HD2DProjectSetup.Setup(false);

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!File.Exists(ScenePath)) HD2DProjectSetup.Setup(false);
                if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (!interactive && scene.isDirty)
                {
                    Debug.LogWarning("[HD-2D RPG] The open scene has unsaved changes; run 'HD-2D RPG ▸ Bake Castle Into Scene' manually.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            try
            {
                EditorUtility.DisplayProgressBar("HD-2D RPG", "Removing previous bake...", 0.05f);
                foreach (var old in Object.FindObjectsByType<CastleRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (old.Moon != null) Object.DestroyImmediate(old.Moon.gameObject);
                    Object.DestroyImmediate(old.gameObject);
                }
                foreach (var old in Object.FindObjectsByType<BattleStage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(old.gameObject);
                AssetDatabase.DeleteAsset(BakedDir);

                EditorUtility.DisplayProgressBar("HD-2D RPG", "Building castle...", 0.2f);
                Database.Reset();
                Database.Build();
                PixelArt.Load();
                var state = PartyState.NewGame();
                var castle = CastleBuilder.Build(null, state);
                var castleRoot = castle.Root.GetComponent<CastleRoot>();
                var moon = Game.CreateDefaultEnvironment(null);
                castleRoot.Moon = moon;

                EditorUtility.DisplayProgressBar("HD-2D RPG", "Building battle arenas...", 0.4f);
                var normal = BattleStage.Build(null, new Vector3(200, 0, 0), false);
                var boss = BattleStage.Build(null, new Vector3(260, 0, 0), true);
                var roots = new[] { castle.Root.gameObject, normal.gameObject, boss.gameObject, moon.gameObject };

                foreach (var r in roots) StripStaticDecals(r);
                var fxRoot = GameObject.Find("FX");
                if (fxRoot != null && fxRoot.transform.childCount == 0) Object.DestroyImmediate(fxRoot);

                EditorUtility.DisplayProgressBar("HD-2D RPG", "Saving meshes, materials and textures...", 0.6f);
                new AssetWriter().SaveAll(roots);
                EnsurePostFxProfile();

                EditorUtility.DisplayProgressBar("HD-2D RPG", "Saving scene...", 0.95f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[HD-2D RPG] Castle baked into " + ScenePath + ". Generated assets are in " + BakedDir + ".");
                if (interactive)
                {
                    Selection.activeGameObject = castle.Root.gameObject;
                    EditorUtility.DisplayDialog("HD-2D RPG",
                        "The castle is now in the scene.\n\n" +
                        "• Move, duplicate or delete pillars, props, chests and enemy symbols freely; walls are grouped per row (WallRow_n). Collision follows the colliders.\n" +
                        "• Edit chest contents and encounter ids in the Inspector; use 'Refresh Lists' on the Castle object after adding/removing chests or enemies.\n" +
                        "• Move 'PartyStart' to change where the game begins.\n" +
                        "• Lighting & fog: Window ▸ Rendering ▸ Lighting. Post-processing: Resources/HD2D/HD2D_PostFX.", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Permanent decals (magic circles, sigils) don't need their runtime animator.</summary>
        static void StripStaticDecals(GameObject root)
        {
            foreach (var d in root.GetComponentsInChildren<DecalAnim>(true))
            {
                if (d.Duration < 1e6f) continue;
                d.transform.localScale = Vector3.one * d.StartSize;
                var r = d.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null) Mats.SetMainColor(r.sharedMaterial, d.BaseColor);
                Object.DestroyImmediate(d);
            }
        }

        static void EnsurePostFxProfile()
        {
            if (File.Exists(ProfilePath)) return;
            EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            PostFX.AddDefaults(profile);
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ================================================================== asset writer
        /// <summary>Saves every in-memory mesh, material and texture used under the given roots as assets.</summary>
        class AssetWriter
        {
            readonly Dictionary<Texture2D, Texture2D> textures = new Dictionary<Texture2D, Texture2D>();
            readonly HashSet<Material> materials = new HashSet<Material>();
            readonly HashSet<Mesh> meshes = new HashSet<Mesh>();

            public void SaveAll(GameObject[] roots)
            {
                EnsureFolder(BakedDir + "/Textures");
                EnsureFolder(BakedDir + "/Materials");
                EnsureFolder(BakedDir + "/Meshes");
                foreach (var root in roots)
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) SaveMaterial(mats[i]);
                        r.sharedMaterials = mats;
                    }
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) SaveMesh(mf.sharedMesh);
                }
            }

            static string UniquePath(string folder, string name, string ext)
            {
                string clean = string.IsNullOrEmpty(name) ? "Generated" : name;
                foreach (char c in Path.GetInvalidFileNameChars()) clean = clean.Replace(c, '_');
                return AssetDatabase.GenerateUniqueAssetPath(BakedDir + "/" + folder + "/" + clean + ext);
            }

            void SaveMesh(Mesh m)
            {
                if (m == null || EditorUtility.IsPersistent(m) || !meshes.Add(m)) return;
                AssetDatabase.CreateAsset(m, UniquePath("Meshes", m.name, ".asset"));
            }

            void SaveMaterial(Material m)
            {
                if (m == null || EditorUtility.IsPersistent(m) || !materials.Add(m)) return;
                foreach (var prop in m.GetTexturePropertyNames())
                {
                    if (m.GetTexture(prop) is Texture2D t && !EditorUtility.IsPersistent(t))
                        m.SetTexture(prop, SaveTexture(t));
                }
                AssetDatabase.CreateAsset(m, UniquePath("Materials", m.name, ".mat"));
            }

            Texture2D SaveTexture(Texture2D t)
            {
                if (textures.TryGetValue(t, out var saved)) return saved;
                string path = UniquePath("Textures", t.name, ".png");
                File.WriteAllBytes(path, t.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = t.mipmapCount > 1;
                imp.filterMode = t.filterMode;
                imp.wrapMode = t.wrapMode;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
                saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                textures[t] = saved;
                return saved;
            }
        }
    }
}
