using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    /// <summary>Runtime-built particle bursts, light flashes, lightning bolts and sprite effects.</summary>
    public static class FX
    {
        static Transform root;

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("FX").transform;
                return root;
            }
        }

        public static ParticleSystem MakeSystem(string name, Transform parent, Texture tex, bool pixel = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Additive(pixel ? "pixel" : "soft", tex != null ? tex : (pixel ? ProcTex.PixelDot : ProcTex.SoftDot));
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>One-shot burst of glowing particles.</summary>
        public static void Burst(Vector3 pos, Color color, int count = 30, float speed = 3f, float size = 0.25f,
                                 float life = 0.7f, float gravity = 0f, float radius = 0.2f, bool pixel = false,
                                 Vector3? velocity = null)
        {
            var ps = MakeSystem("Burst", null, null, pixel);
            ps.transform.position = pos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            main.gravityModifier = gravity;
            var shape = ps.shape;
            shape.radius = radius;
            if (velocity.HasValue)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = velocity.Value.x; vel.y = velocity.Value.y; vel.z = velocity.Value.z;
            }
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.1f));
            ps.Emit(count);
            Object.Destroy(ps.gameObject, life + 0.5f);
        }

        /// <summary>Temporary point light that fades out.</summary>
        public static void Flash(Vector3 pos, Color color, float intensity = 6f, float range = 8f, float duration = 0.35f)
        {
            var go = new GameObject("FlashLight");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            var f = go.AddComponent<FadeLight>();
            f.Duration = duration;
            f.StartIntensity = intensity;
        }

        /// <summary>A flat textured quad effect (slash arc, ring, magic circle) that scales and fades.</summary>
        public static GameObject Decal(Vector3 pos, Quaternion rot, Texture tex, Color color, float startSize, float endSize,
                                       float duration, float spin = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "FXDecal";
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mats.AdditiveInstance(tex, color);
            r.shadowCastingMode = ShadowCastingMode.Off;
            var a = go.AddComponent<DecalAnim>();
            a.StartSize = startSize; a.EndSize = endSize; a.Duration = duration; a.Spin = spin; a.BaseColor = color;
            return go;
        }

        public static Quaternion FaceCamera()
        {
            var cam = CameraRig.MainCamera;
            return cam != null ? cam.transform.rotation : Quaternion.identity;
        }

        public static Quaternion Flat => Quaternion.Euler(90, 0, 0);

        /// <summary>Jagged lightning bolt from above down to a point.</summary>
        public static void Lightning(Vector3 target, Color color, float height = 9f)
        {
            var go = new GameObject("Lightning");
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            int n = 14;
            lr.positionCount = n;
            Vector3 top = target + new Vector3(Random.Range(-1f, 1f), height, Random.Range(-0.5f, 0.5f));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(top, target, t);
                if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-0.45f, 0.45f), 0, Random.Range(-0.2f, 0.2f));
                lr.SetPosition(i, p);
            }
            lr.widthMultiplier = 0.22f;
            lr.material = Mats.AdditiveInstance(ProcTex.SoftDot, color * 3f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.numCapVertices = 2;
            var f = go.AddComponent<LineFade>();
            f.Duration = 0.35f;
            Flash(target + Vector3.up, color, 10f, 12f, 0.3f);
        }

        /// <summary>Falling glowing projectile used by meteor-type spells.</summary>
        public static IEnumerator Meteor(Vector3 target, Color color, float duration = 0.45f)
        {
            Vector3 start = target + new Vector3(-3f, 8f, 2f);
            var ps = MakeSystem("Meteor", null, null);
            var main = ps.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = color;
            var em = ps.emission;
            em.rateOverTime = 120;
            ps.Play();
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                ps.transform.position = Vector3.Lerp(start, target, t / duration);
                yield return null;
            }
            ps.transform.position = target;
            em.rateOverTime = 0;
            Burst(target, color, 40, 6f, 0.45f, 0.8f, 0.5f, 0.3f);
            Flash(target + Vector3.up, color, 9f, 10f, 0.4f);
            Object.Destroy(ps.gameObject, 1f);
        }
    }

}
