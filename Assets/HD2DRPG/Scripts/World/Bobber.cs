using UnityEngine;

namespace HD2DRPG
{
    public class Bobber : MonoBehaviour
    {
        public float Amplitude = 0.12f, Frequency = 1.4f;
        Vector3 basePos;
        float seed;
        void Start() { basePos = transform.localPosition; seed = Random.value * 6f; }
        void Update() => transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * Frequency + seed) * Amplitude;
    }
}
