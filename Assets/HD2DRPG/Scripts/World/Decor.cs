using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Flickering torch / brazier light.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        public float BaseIntensity = 2.2f;
        public float Amount = 0.35f;
        public float Fade = 1f;
        Light l;
        float seed;

        void Awake()
        {
            l = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (l == null) return;
            float t = Time.time * 7f + seed;
            float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, seed + 3f) * 0.3f;
            l.intensity = BaseIntensity * (1f - Amount + n * Amount * 2f) * Fade;
        }
    }

    /// <summary>Cycles sprite frames on an unlit/glow quad (flames).</summary>
    public class FrameCycler : MonoBehaviour
    {
        public string[] Frames;
        public float Fps = 8f;
        Material m;
        float t;
        int shown = -1;

        void Start()
        {
            m = GetComponent<MeshRenderer>().material;
            t = Random.value * 10f;
        }

        void Update()
        {
            if (Frames == null || Frames.Length == 0 || m == null) return;
            t += Time.deltaTime * Fps;
            int i = (int)t % Frames.Length;
            if (i == shown) return;
            shown = i;
            var tex = PixelArt.Tex(Frames[i]);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        void OnDestroy()
        {
            if (m) Destroy(m);
        }
    }

    /// <summary>Keeps a quad facing the camera (yaw only).</summary>
    public class Billboard : MonoBehaviour
    {
        public bool YawOnly = true;
        void LateUpdate()
        {
            var cam = CameraRig.MainCamera;
            if (cam == null) return;
            if (YawOnly) transform.rotation = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);
            else transform.rotation = cam.transform.rotation;
        }
    }

    public class Spinner : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;
        public float Speed = 30f;
        void Update() => transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self);
    }

    public class Bobber : MonoBehaviour
    {
        public float Amplitude = 0.12f, Frequency = 1.4f;
        Vector3 basePos;
        float seed;
        void Start() { basePos = transform.localPosition; seed = Random.value * 6f; }
        void Update() => transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * Frequency + seed) * Amplitude;
    }

    /// <summary>Pulses an additive material's alpha (light shafts, glows).</summary>
    public class Pulse : MonoBehaviour
    {
        public Color BaseColor = Color.white;
        public float Min = 0.6f, Max = 1f, Speed = 0.8f;
        Material m;
        float seed;
        void Start() { m = GetComponent<Renderer>().material; seed = Random.value * 10f; }
        void Update()
        {
            if (m == null) return;
            float k = Mathf.Lerp(Min, Max, (Mathf.Sin(Time.time * Speed + seed) + 1f) * 0.5f);
            Mats.SetMainColor(m, BaseColor * k);
        }
        void OnDestroy() { if (m) Destroy(m); }
    }

    /// <summary>
    /// Diorama cutaway: walls and pillars that stand between the camera and the party sink down
    /// so the player is never hidden (rows south of the leader are lowered).
    /// </summary>
    public class Cutaway : MonoBehaviour
    {
        public static Transform Focus;
        public float Z;
        public float X;
        public float XRange = -1f;   // < 0 = whole row
        public float Margin = 0.6f;
        public float LowScale = 0.12f;
        public GameObject Tall;
        public GameObject Low;
        public Light[] Lights;
        float current = 1f;

        void Update()
        {
            if (Focus == null) return;
            Vector3 p = Focus.position;
            bool lower = Z < p.z - Margin && (XRange < 0 || Mathf.Abs(X - p.x) < XRange);
            float target = lower ? LowScale : 1f;
            current = Mathf.MoveTowards(current, target, Time.deltaTime * 3.5f);
            bool fullyLow = Low != null && current <= LowScale + 0.001f;
            if (Tall != null)
            {
                Tall.SetActive(!fullyLow);
                var s = Tall.transform.localScale;
                Tall.transform.localScale = new Vector3(s.x, current, s.z);
            }
            if (Low != null) Low.SetActive(fullyLow);
            if (Lights != null)
                foreach (var l in Lights)
                    if (l != null)
                    {
                        var f = l.GetComponent<TorchFlicker>();
                        if (f) f.Fade = Mathf.InverseLerp(LowScale, 1f, current);
                        else l.enabled = current > 0.5f;
                    }
        }
    }
}
