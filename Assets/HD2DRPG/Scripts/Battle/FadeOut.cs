using System.Collections;
using UnityEngine;

namespace HD2DRPG
{
    public class FadeOut : MonoBehaviour
    {
        public float Duration = 0.5f;
        float t;
        Material m;
        Color c;
        void Start()
        {
            m = GetComponent<Renderer>().sharedMaterial;
            c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        }
        void Update()
        {
            t += Time.deltaTime;
            if (m) Mats.SetMainColor(m, c * Mathf.Clamp01(1f - t / Duration));
            if (t >= Duration) { if (m) Destroy(m); Destroy(gameObject); }
        }
    }
}
