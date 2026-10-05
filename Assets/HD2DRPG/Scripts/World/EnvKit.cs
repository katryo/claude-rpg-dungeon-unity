using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    /// <summary>Reusable dark-castle set pieces shared by the dungeon and the battle stages.</summary>
    public static class EnvKit
    {
        public static Material FloorMat => Mats.Lit("floor", ProcTex.Floor, Color.white, 0.25f);
        public static Material WallMat => Mats.Lit("wall", ProcTex.Wall, Color.white, 0.05f);
        public static Material TrimMat => Mats.Lit("trim", ProcTex.Trim, Color.white, 0.1f);
        public static Material PillarMat => Mats.Lit("pillar", ProcTex.Pillar, Color.white, 0.2f);
        public static Material CarpetMat => Mats.Lit("carpet", ProcTex.Carpet, Color.white, 0.0f);
        public static Material DarkMetalMat => Mats.Lit("darkmetal", ProcTex.Solid(new Color(0.16f, 0.13f, 0.2f)), Color.white, 0.55f);
        public static Material GoldMat => Mats.Lit("gold", ProcTex.Solid(new Color(0.85f, 0.62f, 0.25f)), Color.white, 0.6f);

        static GameObject Quad(string name, Transform parent, Material mat, bool shadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color c, float intensity, float range, bool flicker)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            if (flicker)
            {
                var f = go.AddComponent<TorchFlicker>();
                f.BaseIntensity = intensity;
            }
            return l;
        }

        /// <summary>Looping ambient particles (embers, sparkles, dust).</summary>
        public static ParticleSystem Ambient(Transform parent, Vector3 pos, Color color, float rate, float size,
                                             float life, Vector3 velocity, float radius, bool pixel = true)
        {
            var ps = FX.MakeSystem("Ambient", parent, null, pixel);
            ps.transform.position = pos;
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.4f));
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = rate;
            main.playOnAwake = true; // keeps running when loaded from a saved scene
            var shape = ps.shape;
            shape.radius = radius;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x - 0.1f, velocity.x + 0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y * 0.6f, velocity.y);
            vel.z = new ParticleSystem.MinMaxCurve(velocity.z - 0.1f, velocity.z + 0.1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.6f;
            ps.Play();
            return ps;
        }

        /// <summary>Wall sconce with an animated pixel flame, flickering light and embers.</summary>
        public static Light Torch(Transform parent, Vector3 wallFacePos, Vector3 facing, float height = 2.3f, float lightScale = 1f)
        {
            var root = new GameObject("Torch").transform;
            root.SetParent(parent, false);
            root.position = wallFacePos + Vector3.up * height;

            var bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bracket.GetComponent<Collider>());
            bracket.transform.SetParent(root, false);
            bracket.transform.position = root.position + facing * 0.12f - Vector3.up * 0.18f;
            bracket.transform.localScale = new Vector3(0.14f, 0.36f, 0.14f);
            bracket.GetComponent<MeshRenderer>().sharedMaterial = DarkMetalMat;
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cup.GetComponent<Collider>());
            cup.transform.SetParent(root, false);
            cup.transform.position = root.position + facing * 0.22f;
            cup.transform.localScale = new Vector3(0.26f, 0.12f, 0.26f);
            cup.GetComponent<MeshRenderer>().sharedMaterial = GoldMat;

            var flameMat = Mats.Glow(PixelArt.Tex("flame1"), 2.6f);
            var flame = Quad("Flame", root, flameMat);
            flame.transform.position = root.position + facing * 0.24f + Vector3.up * 0.26f;
            var fs = PixelArt.WorldSize("flame1") * 1.3f;
            flame.transform.localScale = new Vector3(fs.x, fs.y, 1);
            flame.AddComponent<Billboard>();
            var fc = flame.AddComponent<FrameCycler>();
            fc.Frames = new[] { "flame1", "flame2" };
            fc.Fps = 7f + Random.value * 3f;

            var l = PointLight(root, root.position + facing * 0.6f + Vector3.up * 0.3f, new Color(1f, 0.58f, 0.28f), 2.6f * lightScale, 7.5f, true);
            Ambient(root, root.position + facing * 0.24f + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), 3f, 0.06f, 1.6f, new Vector3(0, 0.8f, 0), 0.08f);
            return l;
        }

        /// <summary>Glowing stained-glass window with a fake volumetric moonlight shaft.</summary>
        public static Light Window(Transform parent, Vector3 wallFacePos, Vector3 facing, float width = 1.5f, float height = 3f,
                                   float bottom = 1.3f, Color? tint = null, bool shaft = true)
        {
            var root = new GameObject("Window").transform;
            root.SetParent(parent, false);
            Color tc = tint ?? Color.white;
            var mat = Mats.Sprite(ProcTex.Window, 1.35f, tc);
            Mats.SetEmission(mat, ProcTex.Window, tc * 1.35f);
            var q = Quad("Glass", root, mat);
            Vector3 center = wallFacePos + facing * 0.03f + Vector3.up * (bottom + height / 2);
            q.transform.position = center;
            q.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            q.transform.localScale = new Vector3(width, height, 1);

            Light l = null;
            if (shaft)
            {
                Vector3 floorPt = wallFacePos + facing * 3.4f;
                floorPt.y = 0;
                Vector3 top = center + Vector3.up * 0.4f;
                Vector3 d = (floorPt - top);
                Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
                var shaftMat = Mats.AdditiveInstance(ProcTex.Shaft, new Color(0.35f, 0.45f, 0.95f) * 0.32f * (tint.HasValue ? 1.4f : 1f));
                if (tint.HasValue) Mats.SetMainColor(shaftMat, new Color(tc.r, tc.g, tc.b) * 0.35f);
                var s = Quad("LightShaft", root, shaftMat);
                s.transform.position = top + d * 0.5f;
                s.transform.rotation = Quaternion.LookRotation(Vector3.Cross(d.normalized, side), -d.normalized);
                s.transform.localScale = new Vector3(width * 1.1f, d.magnitude, 1);
                var p = s.AddComponent<Pulse>();
                p.BaseColor = tint.HasValue ? new Color(tc.r, tc.g, tc.b) * 0.35f : new Color(0.35f, 0.45f, 0.95f) * 0.32f;
                p.Min = 0.65f; p.Max = 1f; p.Speed = 0.6f;
                l = PointLight(root, floorPt + Vector3.up * 0.8f, tint.HasValue ? tc : new Color(0.45f, 0.55f, 1f), 1.3f, 4.5f, false);
                Ambient(root, top + d * 0.5f, new Color(0.6f, 0.7f, 1f) * 0.8f, 1.5f, 0.05f, 5f, new Vector3(0, -0.05f, 0), 0.8f);
            }
            return l;
        }

        public static void Banner(Transform parent, Vector3 wallFacePos, Vector3 facing, float width = 1.0f, float height = 2.6f, float bottom = 1.4f)
        {
            var mat = Mats.Sprite(ProcTex.Banner, 0.15f);
            var q = Quad("Banner", parent, mat, true);
            q.transform.position = wallFacePos + facing * 0.04f + Vector3.up * (bottom + height / 2);
            q.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            q.transform.localScale = new Vector3(width, height, 1);
        }

        public static GameObject Pillar(Transform parent, Vector3 basePos, float height = 4.2f)
        {
            var root = new GameObject("Pillar");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(basePos.x, 0, basePos.z);
            var trim = new MeshBuilder();
            trim.Box(new Vector3(-0.45f, 0, -0.45f), new Vector3(0.45f, 0.35f, 0.45f));
            trim.Box(new Vector3(-0.38f, 0.35f, -0.38f), new Vector3(0.38f, 0.5f, 0.38f));
            trim.Box(new Vector3(-0.42f, height - 0.35f, -0.42f), new Vector3(0.42f, height - 0.2f, 0.42f));
            trim.Box(new Vector3(-0.48f, height - 0.2f, -0.48f), new Vector3(0.48f, height, 0.48f));
            trim.Create("PillarTrim", root.transform, TrimMat);
            var shaft = new MeshBuilder();
            shaft.Prism(new Vector3(0, 0.5f, 0), 0.32f, height - 0.85f, 10);
            shaft.Create("PillarShaft", root.transform, PillarMat);
            return root;
        }

        public static Light Brazier(Transform parent, Vector3 pos, Color fireColor, float scale = 1f)
        {
            var root = new GameObject("Brazier").transform;
            root.SetParent(parent, false);
            root.position = pos;
            var mb = new MeshBuilder();
            mb.Box(new Vector3(-0.3f, 0, -0.3f), new Vector3(0.3f, 0.15f, 0.3f));
            mb.Box(new Vector3(-0.1f, 0.15f, -0.1f), new Vector3(0.1f, 0.9f, 0.1f));
            mb.Create("Stand", root, DarkMetalMat);
            var bowl = new MeshBuilder();
            bowl.Box(new Vector3(-0.42f, 0.9f, -0.42f), new Vector3(0.42f, 1.15f, 0.42f));
            bowl.Create("Bowl", root, GoldMat);

            var flameMat = Mats.Glow(PixelArt.Tex("flame1"), 2.8f);
            Mats.SetMainColor(flameMat, fireColor * 2.8f);
            var flame = Quad("Flame", root, flameMat);
            flame.transform.position = pos + Vector3.up * (1.15f + 0.45f * scale);
            var fs = PixelArt.WorldSize("flame1") * 2.6f * scale;
            flame.transform.localScale = new Vector3(fs.x, fs.y, 1);
            flame.AddComponent<Billboard>();
            var fc = flame.AddComponent<FrameCycler>();
            fc.Frames = new[] { "flame1", "flame2" };
            fc.Fps = 9f;
            Ambient(root, pos + Vector3.up * 1.5f, fireColor, 10f, 0.08f, 1.8f, new Vector3(0, 1.1f, 0), 0.25f);
            var bc = root.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.6f, 0);
            bc.size = new Vector3(0.85f, 1.2f, 0.85f);
            return PointLight(root, pos + Vector3.up * 1.9f, fireColor, 3.4f, 9f, true);
        }

        /// <summary>Floating save crystal on a glowing magic circle. Restores the party when touched.</summary>
        public static GameObject SaveCrystal(Transform parent, Vector3 pos)
        {
            var root = new GameObject("SaveCrystal");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var mat = Mats.Sprite(PixelArt.Tex("crystal"), 1.3f);
            var q = Quad("Crystal", root.transform, mat, true);
            var s = PixelArt.WorldSize("crystal") * 1.25f;
            q.transform.localScale = new Vector3(s.x, s.y, 1);
            q.transform.localPosition = new Vector3(0, s.y / 2 + 0.05f, 0);
            q.AddComponent<Billboard>();
            var circle = FX.Decal(pos + Vector3.up * 0.03f, FX.Flat, ProcTex.MagicCircle, new Color(0.3f, 0.75f, 1f) * 0.8f, 1.8f, 1.8f, 1e9f, 0f);
            circle.transform.SetParent(root.transform, true);
            var sp = circle.AddComponent<Spinner>();
            sp.Axis = Vector3.forward; sp.Speed = 20f;
            PointLight(root.transform, pos + Vector3.up * 1.2f, new Color(0.4f, 0.75f, 1f), 2.4f, 6f, false);
            var cc = root.AddComponent<CapsuleCollider>();
            cc.center = Vector3.up;
            cc.radius = 0.5f;
            cc.height = 2f;
            Ambient(root.transform, pos + Vector3.up * 0.5f, new Color(0.5f, 0.85f, 1f), 6f, 0.07f, 2f, new Vector3(0, 0.6f, 0), 0.6f);
            return root;
        }

        /// <summary>The Dark Lord's throne on a raised dais.</summary>
        public static Transform Throne(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Throne").transform;
            root.SetParent(parent, false);
            root.position = pos;
            var dark = new MeshBuilder();
            dark.Box(new Vector3(-1.6f, 0, -1.2f), new Vector3(1.6f, 0.25f, 0.6f));     // dais
            dark.Box(new Vector3(-0.6f, 0.25f, -0.3f), new Vector3(0.6f, 0.85f, 0.4f));  // seat
            dark.Box(new Vector3(-0.7f, 0.25f, 0.3f), new Vector3(0.7f, 3.4f, 0.55f));   // back
            dark.Box(new Vector3(-0.85f, 0.25f, -0.3f), new Vector3(-0.6f, 1.25f, 0.4f)); // arm
            dark.Box(new Vector3(0.6f, 0.25f, -0.3f), new Vector3(0.85f, 1.25f, 0.4f));
            dark.Create("ThroneBody", root, DarkMetalMat);
            var gold = new MeshBuilder();
            gold.Box(new Vector3(-1.62f, 0.25f, -1.22f), new Vector3(1.62f, 0.31f, 0.62f), MeshBuilder.Faces.Sides | MeshBuilder.Faces.Top);
            gold.Box(new Vector3(-0.78f, 3.4f, 0.28f), new Vector3(0.78f, 3.55f, 0.57f));
            gold.Box(new Vector3(-0.12f, 3.55f, 0.36f), new Vector3(0.12f, 4.3f, 0.5f));   // central spike
            gold.Box(new Vector3(-0.68f, 3.55f, 0.36f), new Vector3(-0.5f, 4.0f, 0.5f));
            gold.Box(new Vector3(0.5f, 3.55f, 0.36f), new Vector3(0.68f, 4.0f, 0.5f));
            gold.Box(new Vector3(-0.9f, 1.25f, -0.35f), new Vector3(-0.55f, 1.35f, 0.45f));
            gold.Box(new Vector3(0.55f, 1.25f, -0.35f), new Vector3(0.9f, 1.35f, 0.45f));
            gold.Create("ThroneGold", root, GoldMat);
            // glowing gem
            var gem = Quad("Gem", root, Mats.Glow(PixelArt.Tex("icon_dark"), 3f));
            gem.transform.position = pos + new Vector3(0, 2.7f, 0.28f);
            gem.transform.localScale = Vector3.one * 0.5f;
            PointLight(root, pos + new Vector3(0, 2.6f, -0.3f), new Color(0.7f, 0.3f, 1f), 1.6f, 4f, false);
            var bc = root.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 1f, -0.3f);
            bc.size = new Vector3(3.2f, 2f, 1.8f);
            return root;
        }

        /// <summary>Huge red rose window behind the throne with a slowly turning arcane sigil.</summary>
        public static void RoseWindow(Transform parent, Vector3 wallFacePos, Vector3 facing)
        {
            Window(parent, wallFacePos, facing, 2.6f, 4.4f, 1.2f, new Color(1f, 0.32f, 0.42f), false);
            var sigil = FX.Decal(wallFacePos + facing * 0.08f + Vector3.up * 3.6f, Quaternion.LookRotation(-facing),
                                 ProcTex.MagicCircle, new Color(0.8f, 0.25f, 1f) * 0.9f, 4.2f, 4.2f, 1e9f, 0f);
            sigil.transform.SetParent(parent, true);
            var sp = sigil.AddComponent<Spinner>();
            sp.Axis = Vector3.forward; sp.Speed = -8f;
            var spot = new GameObject("ThroneSpot");
            spot.transform.SetParent(parent, false);
            spot.transform.position = wallFacePos + facing * 2.5f + Vector3.up * 6f;
            spot.transform.rotation = Quaternion.LookRotation((wallFacePos + facing * 2.2f) - spot.transform.position);
            var l = spot.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 55f;
            l.range = 12f;
            l.intensity = 5f;
            l.color = new Color(1f, 0.25f, 0.35f);
            l.shadows = LightShadows.None;
        }
    }
}
