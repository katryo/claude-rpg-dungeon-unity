using UnityEngine;

namespace HD2DRPG
{
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
}
