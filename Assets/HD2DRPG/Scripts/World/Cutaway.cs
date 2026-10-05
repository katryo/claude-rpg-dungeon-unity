using UnityEngine;

namespace HD2DRPG
{
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
