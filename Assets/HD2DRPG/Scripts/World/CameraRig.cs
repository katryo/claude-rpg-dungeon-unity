using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Narrow-FOV "diorama" camera that looks down on the scene from the south, follows a target
    /// smoothly and keeps the depth-of-field focus locked on it.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static Camera MainCamera { get; private set; }
        public static CameraRig Instance { get; private set; }

        public Camera Cam { get; private set; }
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 6.8f, -11.5f);
        public Vector3 LookOffset = new Vector3(0f, 1.0f, 0f);
        public float FollowSharpness = 5f;

        Vector3 shakeOffset;
        float shakeTime, shakeAmp;
        bool fixedShot;
        Vector3 fixedPos, fixedLook;
        float fixedFov;
        Vector3 currentLook;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.012f, 0.035f);
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            MainCamera = cam;
            Instance = rig;
            return rig;
        }

        public void Snap()
        {
            if (fixedShot)
            {
                transform.position = fixedPos;
                currentLook = fixedLook;
            }
            else if (Target != null)
            {
                transform.position = Target.position + Offset;
                currentLook = Target.position + LookOffset;
            }
            transform.LookAt(currentLook);
            if (PostFX.Instance) PostFX.Instance.SetFocusDistance(Vector3.Distance(transform.position, currentLook), true);
        }

        /// <summary>Use a fixed camera shot (battles, cutscenes).</summary>
        public void SetFixedShot(Vector3 position, Vector3 lookAt, float fov = 32f, bool snap = false)
        {
            fixedShot = true;
            fixedPos = position;
            fixedLook = lookAt;
            fixedFov = fov;
            if (snap) { Cam.fieldOfView = fov; Snap(); }
        }

        public void Follow(Transform target, bool snap = true)
        {
            fixedShot = false;
            Target = target;
            if (snap) { Cam.fieldOfView = 32f; Snap(); }
        }

        public void Shake(float amplitude, float duration)
        {
            shakeAmp = Mathf.Max(shakeAmp, amplitude);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-FollowSharpness * dt);
            Vector3 desiredPos, desiredLook;
            if (fixedShot)
            {
                desiredPos = fixedPos;
                desiredLook = fixedLook;
                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fixedFov, k);
            }
            else if (Target != null)
            {
                desiredPos = Target.position + Offset;
                desiredLook = Target.position + LookOffset;
                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, 32f, k);
            }
            else return;

            Vector3 basePos = transform.position - shakeOffset;
            basePos = Vector3.Lerp(basePos, desiredPos, k);
            currentLook = Vector3.Lerp(currentLook, desiredLook, k);

            if (shakeTime > 0)
            {
                shakeTime -= dt;
                float a = shakeAmp * Mathf.Clamp01(shakeTime * 3f);
                shakeOffset = new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0);
                if (shakeTime <= 0) shakeAmp = 0;
            }
            else shakeOffset = Vector3.zero;

            transform.position = basePos + shakeOffset;
            transform.LookAt(currentLook + shakeOffset * 0.5f);
            if (PostFX.Instance) PostFX.Instance.SetFocusDistance(Vector3.Distance(transform.position, currentLook));
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; MainCamera = null; }
        }
    }
}
