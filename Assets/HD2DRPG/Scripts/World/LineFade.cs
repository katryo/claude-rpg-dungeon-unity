using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    public class LineFade : MonoBehaviour
    {
        public float Duration = 0.3f;
        float t;
        LineRenderer lr;
        void Awake() { lr = GetComponent<LineRenderer>(); }
        void Update()
        {
            t += Time.deltaTime;
            if (lr) lr.widthMultiplier = 0.22f * (1f - t / Duration) * (Random.value > 0.3f ? 1f : 0.4f);
            if (t >= Duration) { if (lr) Destroy(lr.material); Destroy(gameObject); }
        }
    }
}
