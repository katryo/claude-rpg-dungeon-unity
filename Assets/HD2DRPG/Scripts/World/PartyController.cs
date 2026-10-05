using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Field movement: the leader walks on the castle grid with sliding collision, the other two
    /// members follow the leader's breadcrumb trail (classic JRPG caravan).
    /// </summary>
    public class PartyController : MonoBehaviour
    {
        public const float WalkSpeed = 3.4f;
        public const float RunSpeed = 5.6f;
        public const float Radius = 0.3f;
        const float FollowSpacing = 0.85f;

        public CastleMap Map;
        public readonly List<SpriteActor> Actors = new List<SpriteActor>();
        public SpriteActor Leader => Actors.Count > 0 ? Actors[0] : null;
        public bool InputEnabled = true;
        public Vector3 LastMoveDir = Vector3.forward;

        readonly List<Vector3> trail = new List<Vector3>();
        float stepTimer;

        public static SpriteActor CreateMemberActor(PartyMember m, Transform parent)
        {
            string p = m.Def.SpritePrefix;
            var a = SpriteActor.Create(m.Name, p + "_stand", parent);
            a.AddAnim("idle", p + "_stand");
            a.AddAnim("walk", p + "_walk1", p + "_stand", p + "_walk2", p + "_stand");
            string act = PixelArt.Has(p + "_attack") ? p + "_attack" : p + "_cast";
            a.AddAnim("attack", act);
            a.AddAnim("cast", PixelArt.Has(p + "_cast") ? p + "_cast" : act);
            a.Play("idle");
            return a;
        }

        public void Init(CastleMap map, PartyState state, Vector3 start)
        {
            Map = map;
            foreach (var a in Actors) if (a) Destroy(a.gameObject);
            Actors.Clear();
            foreach (var m in state.Members)
            {
                var a = CreateMemberActor(m, transform);
                Actors.Add(a);
            }
            Teleport(start, Vector3.forward);
        }

        public void Teleport(Vector3 pos, Vector3 facingDir)
        {
            trail.Clear();
            for (int i = 0; i < 64; i++) trail.Add(pos - facingDir * i * 0.1f);
            for (int i = 0; i < Actors.Count; i++)
            {
                Actors[i].transform.position = pos - facingDir * FollowSpacing * i;
                Actors[i].Play("idle");
            }
            LastMoveDir = facingDir;
        }

        void Update()
        {
            if (Leader == null || Map == null) return;
            Vector2 input = InputEnabled ? GameInput.Move : Vector2.zero;
            Vector3 move = new Vector3(input.x, 0, input.y);
            bool moving = move.sqrMagnitude > 0.01f;
            float speed = GameInput.Run ? RunSpeed : WalkSpeed;
            var lt = Leader.transform;

            if (moving)
            {
                Vector3 delta = move * speed * Time.deltaTime;
                Vector3 p = lt.position;
                Vector3 nx = p + new Vector3(delta.x, 0, 0);
                if (Map.CanStand(nx, Radius)) p = nx;
                Vector3 nz = p + new Vector3(0, 0, delta.z);
                if (Map.CanStand(nz, Radius)) p = nz;
                if ((p - lt.position).sqrMagnitude > 1e-6f)
                {
                    lt.position = p;
                    LastMoveDir = move.normalized;
                    RecordTrail(p);
                }
                if (Mathf.Abs(move.x) > 0.1f) Leader.FacingRight = move.x > 0;
                stepTimer -= Time.deltaTime * speed;
                if (stepTimer <= 0) { stepTimer = 1.6f; AudioManager.Play("step", 0.25f, Random.Range(0.85f, 1.15f)); }
            }

            Leader.Play(moving ? "walk" : "idle", GameInput.Run ? 11f : 8f);

            // followers
            for (int i = 1; i < Actors.Count; i++)
            {
                var f = Actors[i];
                Vector3 target = TrailPoint(i * FollowSpacing);
                Vector3 cur = f.transform.position;
                Vector3 d = target - cur;
                bool fMoving = d.sqrMagnitude > 0.0004f;
                f.transform.position = Vector3.MoveTowards(cur, target, speed * 1.25f * Time.deltaTime);
                if (Mathf.Abs(d.x) > 0.01f) f.FacingRight = d.x > 0;
                f.Play(fMoving && moving ? "walk" : "idle", GameInput.Run ? 11f : 8f);
            }
            Cutaway.Focus = lt;
        }

        void RecordTrail(Vector3 p)
        {
            if (trail.Count == 0 || (trail[0] - p).sqrMagnitude > 0.0025f)
            {
                trail.Insert(0, p);
                if (trail.Count > 400) trail.RemoveAt(trail.Count - 1);
            }
        }

        Vector3 TrailPoint(float distance)
        {
            float acc = 0;
            for (int i = 1; i < trail.Count; i++)
            {
                float seg = Vector3.Distance(trail[i - 1], trail[i]);
                if (acc + seg >= distance)
                {
                    float t = (distance - acc) / Mathf.Max(seg, 1e-5f);
                    return Vector3.Lerp(trail[i - 1], trail[i], t);
                }
                acc += seg;
            }
            return trail.Count > 0 ? trail[trail.Count - 1] : Leader.transform.position;
        }

        public void SetVisible(bool v)
        {
            foreach (var a in Actors) if (a) a.gameObject.SetActive(v);
        }
    }

    /// <summary>Treasure chest that awards items, equipment or gold.</summary>
    public class Chest : MonoBehaviour
    {
        public Vector2Int Tile;
        public string[] Contents;
        public bool Opened;
        Material mat;

        public string FlagKey => "chest_" + Tile.x + "_" + Tile.y;

        public static Chest Create(Transform parent, Vector3 pos, Vector2Int tile, string[] contents)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Chest";
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Chest>();
            c.Tile = tile;
            c.Contents = contents;
            var s = PixelArt.WorldSize("chest_closed") * 1.35f;
            go.transform.position = pos + Vector3.up * s.y / 2;
            go.transform.localScale = new Vector3(s.x, s.y, 1);
            c.mat = Mats.Sprite(PixelArt.Tex("chest_closed"), 0.35f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = c.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            go.AddComponent<Billboard>();
            // a faint golden glint so chests read in the dark
            EnvKit.Ambient(go.transform, pos + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 1.5f, 0.06f, 1.2f, new Vector3(0, 0.3f, 0), 0.3f);
            return c;
        }

        public void SetOpened()
        {
            Opened = true;
            var t = PixelArt.Tex("chest_open");
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", t);
            if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", t);
            var ps = GetComponentInChildren<ParticleSystem>();
            if (ps) ps.Stop();
        }

        public void SetClosed()
        {
            Opened = false;
            var t = PixelArt.Tex("chest_closed");
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", t);
            if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", t);
            var ps = GetComponentInChildren<ParticleSystem>();
            if (ps) ps.Play();
        }

        public Vector3 GroundPos => new Vector3(transform.position.x, 0, transform.position.z);
    }

    public class SaveCrystal : MonoBehaviour
    {
        public Vector2Int Tile;
        public Vector3 GroundPos => new Vector3(transform.position.x, 0, transform.position.z);
    }

    /// <summary>Visible wandering enemy on the field. Touching it starts a battle.</summary>
    public class EnemySymbol : MonoBehaviour
    {
        public string EncounterId;
        public bool Wanders = true;
        public bool IsBoss;
        public SpriteActor Actor;
        public float Cooldown;
        CastleMap map;
        Vector3 home;
        Vector3 wanderTarget;
        float wanderTimer;
        ParticleSystem aura;

        public static EnemySymbol Create(Transform parent, CastleMap map, Vector3 pos, string encounterId, string sprite,
                                         bool wanders, float scale, Color tint)
        {
            var go = new GameObject("Symbol_" + encounterId);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var s = go.AddComponent<EnemySymbol>();
            s.map = map;
            s.EncounterId = encounterId;
            s.Wanders = wanders;
            s.home = pos;
            s.wanderTarget = pos;
            s.Actor = SpriteActor.Create("Sprite", sprite, go.transform, scale);
            s.Actor.transform.localPosition = Vector3.zero;
            s.Actor.FacingRight = false;
            s.Actor.Floating = sprite == "wraith" || sprite == "gargoyle";
            if (tint != Color.white) s.Actor.SetTint(tint);
            s.aura = EnvKit.Ambient(go.transform, pos + Vector3.up * 0.6f, new Color(0.6f, 0.2f, 1f), 8f, 0.12f, 1.2f,
                                    new Vector3(0, 0.7f, 0), 0.45f, false);
            return s;
        }

        public bool Defeated { get; private set; }

        /// <summary>Puts the symbol back at its spawn point (used when retrying from a save crystal).</summary>
        public void Restore(bool defeated)
        {
            Defeated = defeated;
            transform.position = home;
            wanderTarget = home;
            Cooldown = 0;
            gameObject.SetActive(!defeated);
            enabled = !defeated;
            if (!defeated)
            {
                Actor.ResetVisual();
                Actor.gameObject.SetActive(true);
                if (aura) aura.Play();
            }
        }

        public void Defeat()
        {
            Defeated = true;
            if (aura) aura.Stop();
            Actor.Dissolve();
            FX.Burst(transform.position + Vector3.up * 0.8f, new Color(0.7f, 0.4f, 1f), 40, 3f, 0.25f, 1f, -0.3f);
            enabled = false;
        }

        void Update()
        {
            if (Game.I == null || Game.I.Mode != GameMode.Field) return;
            var party = Game.I.Party;
            if (party == null || party.Leader == null) return;
            Vector3 lp = party.Leader.transform.position;
            Vector3 me = transform.position;
            float dist = Vector3.Distance(new Vector3(lp.x, 0, lp.z), new Vector3(me.x, 0, me.z));

            if (Cooldown > 0)
            {
                Cooldown -= Time.deltaTime;
                Actor.gameObject.SetActive(Mathf.Repeat(Cooldown, 0.2f) > 0.1f || Cooldown <= 0);
                return;
            }

            if (IsBoss)
            {
                Actor.FacingRight = lp.x > me.x;
                if (dist < 3.4f) Game.I.TriggerBoss(this);
                return;
            }

            if (dist < 0.85f)
            {
                Game.I.TriggerEncounter(this);
                return;
            }

            if (!Wanders)
            {
                Actor.FacingRight = lp.x > me.x;
                return;
            }

            Vector3 target;
            float speed;
            if (dist < 4.2f)
            {
                target = lp;
                speed = 2.5f;
            }
            else
            {
                wanderTimer -= Time.deltaTime;
                if (wanderTimer <= 0)
                {
                    wanderTimer = Random.Range(1.5f, 3f);
                    wanderTarget = home + new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2f, 2f));
                }
                target = wanderTarget;
                speed = 1.1f;
            }
            Vector3 d = target - me;
            d.y = 0;
            if (d.magnitude > 0.05f)
            {
                Vector3 step = d.normalized * speed * Time.deltaTime;
                Vector3 p = me;
                if (map.CanStand(p + new Vector3(step.x, 0, 0), 0.3f)) p.x += step.x;
                if (map.CanStand(p + new Vector3(0, 0, step.z), 0.3f)) p.z += step.z;
                transform.position = p;
                if (Mathf.Abs(d.x) > 0.05f) Actor.FacingRight = d.x > 0;
            }
        }
    }
}
