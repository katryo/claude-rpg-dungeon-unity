using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    /// <summary>
    /// A 2D pixel-art sprite living in the 3D world: an upright, lit, shadow-casting quad that
    /// yaws to face the camera (tilted back slightly, as in HD-2D titles), with frame animation,
    /// facing flip, idle breathing, hit flashes and a soft ground shadow.
    /// </summary>
    [SelectionBase]
    public class SpriteActor : MonoBehaviour
    {
        [System.Serializable]
        public class Anim
        {
            public string Name;
            public string[] Frames;
        }

        [Tooltip("Sprite id from Resources/HD2D/sprites.txt shown when idle.")]
        public string IdleFrame;
        public List<Anim> Anims = new List<Anim>();
        public float Scale = 1f;
        public bool FacingRight = true;
        public bool Floating;
        public float FloatHeight = 0.35f;
        public float SelfLight = 0.28f;
        /// <summary>Vertical squash used for the knocked-out (kneeling) pose.</summary>
        public float KneelScale = 1f;

        [SerializeField, HideInInspector] Transform quad;
        [SerializeField, HideInInspector] Transform visualRoot;
        [SerializeField, HideInInspector] MeshRenderer quadRenderer;
        [SerializeField, HideInInspector] Transform shadow;
        Material mat;
        string currentAnim;
        string[] frames;
        float fps = 6f;
        float animTime;
        bool loop = true;
        string shownFrame;
        float flashTimer, flashDuration;
        Color flashColor;
        float bobPhase;
        float squash = 1f;
        Color tint = Color.white;
        float dissolve = -1;

        public Transform Visual => visualRoot;
        public float Height => PixelArt.WorldSize(IdleFrame).y * Scale;

        public static SpriteActor Create(string name, string idleFrame, Transform parent, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<SpriteActor>();
            a.IdleFrame = idleFrame;
            a.Scale = scale;
            a.Build();
            return a;
        }

        void Awake()
        {
            // When loaded from a saved scene, work on a per-instance copy of the baked material.
            if (Application.isPlaying && quadRenderer != null && mat == null)
            {
                mat = quadRenderer.material;
                shownFrame = null;
                bobPhase = Random.value * 10f;
            }
        }

        void Build()
        {
            visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(transform, false);

            var q = new GameObject("Sprite");
            q.transform.SetParent(visualRoot, false);
            quad = q.transform;
            Vector2 size = PixelArt.WorldSize(IdleFrame) * Scale;
            q.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.SpriteQuad(size.x, size.y);
            quadRenderer = q.AddComponent<MeshRenderer>();
            mat = Mats.Sprite(PixelArt.Tex(IdleFrame), SelfLight);
            quadRenderer.sharedMaterial = mat;
            quadRenderer.shadowCastingMode = ShadowCastingMode.On;
            quadRenderer.receiveShadows = false;

            // soft blob shadow to ground the sprite
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(sh.GetComponent<Collider>());
            sh.name = "BlobShadow";
            sh.transform.SetParent(transform, false);
            sh.transform.localRotation = Quaternion.Euler(90, 0, 0);
            sh.transform.localPosition = new Vector3(0, 0.02f, 0);
            float sw = Mathf.Max(0.7f, size.x * 0.8f);
            sh.transform.localScale = new Vector3(sw, sw * 0.45f, 1);
            var shMat = BlobShadowMaterial();
            var shr = sh.GetComponent<MeshRenderer>();
            shr.sharedMaterial = shMat;
            shr.shadowCastingMode = ShadowCastingMode.Off;
            shr.receiveShadows = false;
            shadow = sh.transform;

            SetFrame(IdleFrame);
            bobPhase = Random.value * 10f;
        }

        static Material blobMat;
        static Material BlobShadowMaterial()
        {
            if (blobMat != null) return blobMat;
            // Multiplicative-looking dark blob: transparent alpha-blended black soft dot.
            blobMat = new Material(Mats.Additive("blob", ProcTex.SoftDot));
            if (blobMat.HasProperty("_SrcBlend")) blobMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (blobMat.HasProperty("_DstBlend")) blobMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (blobMat.HasProperty("_Blend")) blobMat.SetFloat("_Blend", 0f);
            Mats.SetMainColor(blobMat, new Color(0, 0, 0, 0.55f));
            blobMat.renderQueue = (int)RenderQueue.Transparent - 10;
            return blobMat;
        }

        public void SetTint(Color c)
        {
            tint = c;
            Mats.SetMainColor(mat, c);
        }

        public void SetShadowVisible(bool v)
        {
            if (shadow) shadow.gameObject.SetActive(v);
        }

        public void AddAnim(string name, params string[] frameIds)
        {
            var a = Anims.Find(x => x.Name == name);
            if (a == null) Anims.Add(a = new Anim { Name = name });
            a.Frames = frameIds;
        }

        public void Play(string anim, float framesPerSecond = 6f, bool looping = true)
        {
            if (currentAnim == anim && looping) return;
            var f = Anims.Find(x => x.Name == anim)?.Frames;
            if (f == null || f.Length == 0)
            {
                f = new[] { IdleFrame };
            }
            currentAnim = anim;
            frames = f;
            fps = framesPerSecond;
            loop = looping;
            animTime = 0;
            SetFrame(frames[0]);
        }

        public void SetFrame(string id)
        {
            if (shownFrame == id || mat == null) return;
            shownFrame = id;
            var tex = PixelArt.Tex(id);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
            // frames of one character may differ in width: keep quad pixel-exact
            Vector2 size = PixelArt.WorldSize(id) * Scale;
            quad.localScale = new Vector3(size.x / (PixelArt.WorldSize(IdleFrame).x * Scale), size.y / (PixelArt.WorldSize(IdleFrame).y * Scale), 1);
        }

        public void Flash(Color c, float duration = 0.18f)
        {
            flashColor = c;
            flashTimer = flashDuration = duration;
        }

        public void Squash(float amount) => squash = amount;

        /// <summary>Starts a death dissolve (shrinks into the ground while flashing).</summary>
        public void Dissolve() => dissolve = 0f;

        public bool Dissolved => dissolve >= 1f;

        public void ResetVisual()
        {
            dissolve = -1f;
            visualRoot.localScale = Vector3.one;
            gameObject.SetActive(true);
            SetShadowVisible(true);
        }

        void Update()
        {
            if (frames != null && frames.Length > 1)
            {
                animTime += Time.deltaTime * fps;
                int idx = (int)animTime;
                if (loop) idx %= frames.Length;
                else idx = Mathf.Min(idx, frames.Length - 1);
                SetFrame(frames[idx]);
            }

            // emission: base self-light + flash
            Color em = Color.white * SelfLight;
            if (flashTimer > 0)
            {
                flashTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(flashTimer / flashDuration);
                em = Color.Lerp(em, flashColor * 3f, k);
            }
            if (dissolve >= 0f && dissolve < 1f)
            {
                dissolve += Time.deltaTime * 1.4f;
                em = Color.Lerp(new Color(2.5f, 1.5f, 3f), Color.white * 4f, dissolve);
                visualRoot.localScale = new Vector3(1f + dissolve * 0.3f, Mathf.Max(0.001f, 1f - dissolve), 1f);
                if (dissolve >= 1f) { gameObject.SetActive(false); }
            }
            if (mat != null && mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", em);

            squash = Mathf.MoveTowards(squash, 1f, Time.deltaTime * 3f);
        }

        void LateUpdate()
        {
            var cam = CameraRig.MainCamera;
            if (cam == null || quad == null) return;
            Vector3 e = cam.transform.rotation.eulerAngles;
            float pitch = e.x > 180 ? e.x - 360 : e.x;
            quad.rotation = Quaternion.Euler(pitch * 0.45f, e.y, 0f);
            var ls = quad.localScale;
            ls.x = Mathf.Abs(ls.x) * (FacingRight ? 1 : -1);
            quad.localScale = ls;

            bobPhase += Time.deltaTime;
            float breathe = 1f + Mathf.Sin(bobPhase * 2.4f) * 0.012f;
            float y = 0;
            if (Floating) y = FloatHeight + Mathf.Sin(bobPhase * 1.8f) * 0.12f;
            if (dissolve < 0f)
                visualRoot.localScale = new Vector3(1f / Mathf.Sqrt(squash), breathe * squash * KneelScale, 1f);
            visualRoot.localPosition = new Vector3(0, y, 0);
        }

        void OnDestroy()
        {
            if (Application.isPlaying && mat != null) Destroy(mat);
        }
    }
}
