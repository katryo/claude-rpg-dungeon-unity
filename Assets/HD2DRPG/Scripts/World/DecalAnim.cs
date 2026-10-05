using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    public class DecalAnim : MonoBehaviour
    {
        public float StartSize = 1, EndSize = 2, Duration = 0.4f, Spin;
        public Color BaseColor = Color.white;
        float t;
        Material m;
        void Start()
        {
            m = GetComponent<MeshRenderer>().sharedMaterial;
            transform.localScale = Vector3.one * StartSize;
        }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Duration);
            transform.localScale = Vector3.one * Mathf.Lerp(StartSize, EndSize, 1f - (1f - k) * (1f - k));
            if (Spin != 0) transform.Rotate(0, 0, Spin * Time.deltaTime, Space.Self);
            float a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            if (m) Mats.SetMainColor(m, BaseColor * a);
            if (t >= Duration) { if (m) Destroy(m); Destroy(gameObject); }
        }
    }
}
