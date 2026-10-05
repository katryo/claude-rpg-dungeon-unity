using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Keeps a quad facing the camera (yaw only).</summary>
    public class Billboard : MonoBehaviour
    {
        public bool YawOnly = true;
        void LateUpdate()
        {
            var cam = CameraRig.MainCamera;
            if (cam == null) return;
            if (YawOnly) transform.rotation = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);
            else transform.rotation = cam.transform.rotation;
        }
    }
}
