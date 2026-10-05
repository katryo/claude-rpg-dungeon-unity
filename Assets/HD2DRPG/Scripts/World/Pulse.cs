using UnityEngine;

namespace HD2DRPG
{
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
}
