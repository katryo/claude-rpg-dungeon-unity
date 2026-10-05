using UnityEngine;

namespace HD2DRPG
{
    public class Spinner : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;
        public float Speed = 30f;
        void Update() => transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self);
    }
}
