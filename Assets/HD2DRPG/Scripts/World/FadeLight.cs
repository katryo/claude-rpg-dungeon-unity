using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    public class FadeLight : MonoBehaviour
    {
        public float Duration = 0.3f, StartIntensity = 5f;
        float t;
        Light l;
        void Awake() { l = GetComponent<Light>(); }
        void Update()
        {
            t += Time.deltaTime;
            if (l) l.intensity = StartIntensity * (1f - t / Duration);
            if (t >= Duration) Destroy(gameObject);
        }
    }
}
