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
}
