using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Treasure chest. Contents entries are "item:&lt;id&gt;", "equip:&lt;id&gt;" or "gold:&lt;amount&gt;";
    /// the flag key (from Tile) records that it has been opened.
    /// </summary>
    [SelectionBase]
    public class Chest : MonoBehaviour
    {
        public Vector2Int Tile;
        public string[] Contents = { "item:potion" };
        public bool Opened;
        [SerializeField] MeshRenderer sprite;
        [SerializeField] ParticleSystem glint;
        Material mat;

        [Tooltip("Save-flag id; regenerated automatically for duplicated chests.")]
        [SerializeField] string uniqueId;

        public string FlagKey => "chest_" + (string.IsNullOrEmpty(uniqueId) ? Tile.x + "_" + Tile.y : uniqueId);

#if UNITY_EDITOR
        // Duplicating an object in the editor copies its id; give copies a fresh one.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            bool clash = string.IsNullOrEmpty(uniqueId);
            if (!clash)
                foreach (var o in FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (o != this && o.uniqueId == uniqueId) { clash = true; break; }
            if (clash)
            {
                uniqueId = System.Guid.NewGuid().ToString("N").Substring(0, 12);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        public Vector3 GroundPos => new Vector3(transform.position.x, 0, transform.position.z);

        Material Mat
        {
            get
            {
                if (mat == null && sprite != null) mat = Application.isPlaying ? sprite.material : sprite.sharedMaterial;
                return mat;
            }
        }

        public static Chest Create(Transform parent, Vector3 pos, Vector2Int tile, string[] contents)
        {
            var go = new GameObject("Chest");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<Chest>();
            c.Tile = tile;
            c.uniqueId = tile.x + "_" + tile.y;
            c.Contents = contents;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(0.9f, 1f, 0.9f);

            var q = Util.Quad("Sprite");
            q.transform.SetParent(go.transform, false);
            var s = PixelArt.WorldSize("chest_closed") * 1.35f;
            q.transform.localPosition = Vector3.up * s.y / 2;
            q.transform.localScale = new Vector3(s.x, s.y, 1);
            c.mat = Mats.Sprite(PixelArt.Tex("chest_closed"), 0.35f);
            c.sprite = q.GetComponent<MeshRenderer>();
            c.sprite.sharedMaterial = c.mat;
            c.sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            q.AddComponent<Billboard>();
            // a faint golden glint so chests read in the dark
            c.glint = EnvKit.Ambient(go.transform, pos + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 1.5f, 0.06f, 1.2f, new Vector3(0, 0.3f, 0), 0.3f);
            return c;
        }

        void SetTexture(string id)
        {
            var m = Mat;
            if (m == null) return;
            var t = PixelArt.Tex(id);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", t);
        }

        public void SetOpened()
        {
            Opened = true;
            SetTexture("chest_open");
            if (glint) glint.Stop();
        }

        public void SetClosed()
        {
            Opened = false;
            SetTexture("chest_closed");
            if (glint) glint.Play();
        }

        void OnDestroy()
        {
            if (Application.isPlaying && mat != null && sprite != null && mat != sprite.sharedMaterial) Destroy(mat);
        }
    }
}
